using System;

namespace LogisticPlatform.API.Common.Domain;

internal sealed class LoginAudit
{
    private LoginAudit() { }

    public LoginAudit(Guid userId, string ipAddress, string userAgent, string status)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        LoginDateTime = DateTimeOffset.UtcNow;
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "UNKNOWN" : ipAddress;
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? "UNKNOWN" : userAgent;
        Status = string.IsNullOrWhiteSpace(status) ? "SUCCESS" : status.ToUpperInvariant();
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset LoginDateTime { get; private set; }
    public string IpAddress { get; private set; } = null!;
    public string UserAgent { get; private set; } = null!;
    public string Status { get; private set; } = null!;

    public User User { get; private set; } = null!;
}
