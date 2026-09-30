using System;

namespace LogisticPlatform.API.Common.Domain;

public class UserDeviceSession : BaseEntity
{
    public string BrowserName { get; set; } = string.Empty;
    public string BrowserVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IpAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public string LocationCity { get; set; } = string.Empty;
    public string LocationCountry { get; set; } = string.Empty;
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}
