using System;

namespace LogisticPlatform.API.Common.Domain;

internal sealed class PasswordResetToken : BaseEntity
{
    public DateTime ExpiresAt { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsConsumed { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public Guid UserId { get; set; }
}
