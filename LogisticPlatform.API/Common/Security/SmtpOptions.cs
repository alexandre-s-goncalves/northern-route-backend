using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace LogisticPlatform.API.Common.Security;

public sealed record SmtpOptions
{
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;

    internal static SmtpOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var port = int.TryParse(
            configuration["SMTP_PORT"],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var configuredPort)
            ? configuredPort
            : 587;

        return new SmtpOptions
        {
            FromAddress = configuration["SMTP_FROM_ADDRESS"] ?? "security@northernroute.com",
            FromName = configuration["SMTP_FROM_NAME"] ?? "NorthernRoute Security",
            Host = configuration["SMTP_HOST"] ?? "localhost",
            Password = configuration["SMTP_PASSWORD"] ?? string.Empty,
            Port = port,
            Username = configuration["SMTP_USERNAME"] ?? string.Empty
        };
    }
}
