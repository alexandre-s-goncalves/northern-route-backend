namespace LogisticPlatform.API.Common.Domain;

internal sealed class User
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public Guid RoleId { get; private set; }
    public Role? Role { get; }

    private User() { }

    public User(string name, string email, string passwordHash, Guid roleId)
    {
        Id = Guid.NewGuid();
        Name = name;
        Email = email.ToUpperInvariant();
        PasswordHash = passwordHash;
        RoleId = roleId;
    }
}
