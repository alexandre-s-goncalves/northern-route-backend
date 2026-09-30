using System;

namespace LogisticPlatform.API.Common.Domain;

public class RefreshTokenSession : BaseEntity
{
    public Guid DeviceSessionId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsRevoked { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}
