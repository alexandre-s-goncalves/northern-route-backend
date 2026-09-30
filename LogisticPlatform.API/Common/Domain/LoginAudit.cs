using System;

namespace LogisticPlatform.API.Common.Domain;

internal sealed class LoginAudit : BaseEntity
{
    private LoginAudit() { }

    public Guid? DeviceSessionId { get; private set; }
    public Guid Id { get; private set; }
    public string IpAddress { get; private set; } = null!;
    public DateTime LoginDateTime { get; private set; }
    public string Status { get; private set; } = null!;
    public User? User { get; }
    public string UserAgent { get; private set; } = null!;
    public Guid? UserId { get; private set; }

    public LoginAudit(Guid? userId, Guid? deviceSessionId, string ipAddress, string userAgent, string status)
    {
        DeviceSessionId = deviceSessionId;
        Id = Guid.NewGuid();
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "UNKNOWN" : ipAddress;
        LoginDateTime = DateTime.UtcNow;
        Status = string.IsNullOrWhiteSpace(status) ? "SUCCESS" : status.ToUpperInvariant();
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? "UNKNOWN" : userAgent;
        UserId = userId;
    }

}
