using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace LogisticPlatform.API.Common.Security;

public sealed class EmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly SecureSocketOptions _secureSocketOptions;

    public EmailService(IOptions<SmtpOptions> options)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)), SecureSocketOptions.StartTls)
    {
    }

    internal EmailService(SmtpOptions options, SecureSocketOptions secureSocketOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _secureSocketOptions = secureSocketOptions;
    }

    public async Task SendMfaCodeEmailAsync(string toEmail, string userName, string securityCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityCode);

        using var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        email.To.Add(new MailboxAddress(userName, toEmail));
        email.Subject = $"{securityCode} é seu código de verificação NorthernRoute";

        var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 12px;'>
                <h2 style='color: #0f172a; font-size: 20px; font-weight: bold; margin-bottom: 16px;'>Verificação de Acesso - NorthernRoute</h2>
                <p style='color: #334155; font-size: 14px; line-height: 1.5;'>Olá, <strong>{userName}</strong>,</p>
                <p style='color: #334155; font-size: 14px; line-height: 1.5;'>Um pedido de login foi iniciado para a sua conta de operador logístico. Use o código de uso único (OTP) abaixo para concluir a sua autenticação de dois fatores:</p>
                <div style='background-color: #f8fafc; border: 1px dashed #cbd5e1; padding: 16px; text-align: center; margin: 24px 0; border-radius: 8px;'>
                    <span style='font-size: 32px; font-weight: 900; letter-spacing: 4px; color: #0f172a;'>{securityCode}</span>
                </div>
                <p style='color: #64748b; font-size: 12px; line-height: 1.5; margin-top: 24px; border-top: 1px solid #e2e8f0; padding-top: 16px;'>Este código é válido por exatamente <strong>15 minutos</strong>. Caso não tenha solicitado este acesso, altere imediatamente a sua senha de segurança.</p>
            </div>";

        email.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, _secureSocketOptions);
        await client.AuthenticateAsync(_options.Username, _options.Password);
        await client.SendAsync(email);
        await client.DisconnectAsync(true);
    }
}
