using System;
using System.Collections.Generic;

namespace LogisticPlatform.API.Common.Domain;

public class MfaConfiguration : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsEnabled { get; set; }
    public string Provider { get; set; } = "totp";
    public ICollection<string> SecretBackupCodes { get; } = [];
    public string SecretKeyHash { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}
