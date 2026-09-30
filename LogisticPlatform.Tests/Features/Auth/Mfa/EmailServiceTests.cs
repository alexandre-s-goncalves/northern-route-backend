using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LogisticPlatform.API.Common.Security;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit;

namespace LogisticPlatform.Tests.Features.Auth.Mfa;

public sealed class EmailServiceTests
{
    [Fact]
    public async Task SendMfaCodeEmailAsync_ShouldSendHtmlMessageThroughSmtp()
    {
        using var server = new LocalSmtpServer();
        var receiveMessage = server.ReceiveMessageAsync();
        var service = new EmailService(CreateSmtpOptions(server.Port), SecureSocketOptions.None);

        await service.SendMfaCodeEmailAsync("operator@example.com", "Operator One", "123456");

        var rawMessage = await receiveMessage;
        using var messageStream = new MemoryStream(Encoding.ASCII.GetBytes(rawMessage));
        using var message = await MimeMessage.LoadAsync(messageStream);

        Assert.Equal("security@example.com", message.From.Mailboxes.Single().Address);
        Assert.Equal("operator@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("123456 é seu código de verificação NorthernRoute", message.Subject);
        Assert.Contains("Operator One", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("123456", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMfaCodeEmailAsync_ShouldThrow_WhenRecipientIsEmpty()
    {
        var service = new EmailService(new SmtpOptions(), SecureSocketOptions.StartTls);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SendMfaCodeEmailAsync(" ", "Operator One", "123456"));
    }

    [Fact]
    public async Task SendMfaCodeEmailAsync_ShouldThrow_WhenSecurityCodeIsEmpty()
    {
        var service = new EmailService(new SmtpOptions(), SecureSocketOptions.StartTls);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SendMfaCodeEmailAsync("operator@example.com", "Operator One", " "));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new EmailService((IOptions<SmtpOptions>)null!));
    }

    [Fact]
    public void Constructor_ShouldAcceptOptions()
    {
        var service = new EmailService(Options.Create(new SmtpOptions()));

        Assert.IsAssignableFrom<IEmailService>(service);
    }

    private static SmtpOptions CreateSmtpOptions(int port)
    {
        return new SmtpOptions
        {
            FromAddress = "security@example.com",
            FromName = "Security Team",
            Host = "127.0.0.1",
            Password = "smtp-password",
            Port = port,
            Username = "smtp-user"
        };
    }

    private sealed class LocalSmtpServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public LocalSmtpServer()
        {
            _listener.Start();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public async Task<string> ReceiveMessageAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };
            var message = new StringBuilder();
            var readingMessage = false;

            await writer.WriteLineAsync("220 localhost ESMTP ready");

            while (await reader.ReadLineAsync() is { } line)
            {
                if (readingMessage)
                {
                    readingMessage = await HandleMessageLineAsync(line, writer, message);
                    continue;
                }

                readingMessage = await HandleCommandAsync(line, writer);
            }

            return message.ToString();
        }

        private static async Task<bool> HandleMessageLineAsync(
            string line,
            StreamWriter writer,
            StringBuilder message)
        {
            if (line == ".")
            {
                await writer.WriteLineAsync("250 2.0.0 Message accepted");
                return false;
            }

            message.AppendLine(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line);
            return true;
        }

        private static async Task<bool> HandleCommandAsync(string line, StreamWriter writer)
        {
            if (line.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250-AUTH PLAIN");
                await writer.WriteLineAsync("250 SIZE 1000000");
                return false;
            }

            if (line.StartsWith("AUTH ", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                return false;
            }

            if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250 2.1.0 OK");
                return false;
            }

            if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                return true;
            }

            if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 2.0.0 Bye");
                return false;
            }

            throw new InvalidOperationException($"Unexpected SMTP command: {line}");
        }

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}

public sealed class SmtpOptionsTests
{
    [Fact]
    public void FromConfiguration_ShouldUseDefaults_WhenSettingsAreMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        var options = SmtpOptions.FromConfiguration(configuration);

        Assert.Equal("security@northernroute.com", options.FromAddress);
        Assert.Equal("NorthernRoute Security", options.FromName);
        Assert.Equal("localhost", options.Host);
        Assert.Equal(string.Empty, options.Password);
        Assert.Equal(587, options.Port);
        Assert.Equal(string.Empty, options.Username);
    }

    [Fact]
    public void FromConfiguration_ShouldReadProvidedSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("SMTP_FROM_ADDRESS", "mfa@example.com"),
                new("SMTP_FROM_NAME", "MFA Service"),
                new("SMTP_HOST", "smtp.example.com"),
                new("SMTP_PASSWORD", "secret"),
                new("SMTP_PORT", "2525"),
                new("SMTP_USERNAME", "mfa-user")
            ])
            .Build();

        var options = SmtpOptions.FromConfiguration(configuration);

        Assert.Equal("mfa@example.com", options.FromAddress);
        Assert.Equal("MFA Service", options.FromName);
        Assert.Equal("smtp.example.com", options.Host);
        Assert.Equal("secret", options.Password);
        Assert.Equal(2525, options.Port);
        Assert.Equal("mfa-user", options.Username);
    }

    [Fact]
    public void FromConfiguration_ShouldUseDefaultPort_WhenPortIsInvalid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("SMTP_PORT", "not-a-port")])
            .Build();

        var options = SmtpOptions.FromConfiguration(configuration);

        Assert.Equal(587, options.Port);
    }

    [Fact]
    public void FromConfiguration_ShouldThrow_WhenConfigurationIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => SmtpOptions.FromConfiguration(null!));
    }
}
