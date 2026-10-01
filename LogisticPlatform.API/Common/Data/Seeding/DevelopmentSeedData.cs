using System.Collections.Generic;

namespace LogisticPlatform.API.Common.Data.Seeding;

internal sealed record DevelopmentUserSeed(string Name, string Email, string Password, string RoleName);

internal static class DevelopmentSeedData
{
    public static IReadOnlyList<string> Roles { get; } =
    [
        "ADMIN",
        "USER"
    ];

    public static IReadOnlyList<DevelopmentUserSeed> Users { get; } =
    [
        new DevelopmentUserSeed("Alexandre Santos", "ale@ale.com", "Password123", "ADMIN"),
        new DevelopmentUserSeed("John Doe Operator", "operator@northernroute.com", "Operator123", "USER")
    ];
}
