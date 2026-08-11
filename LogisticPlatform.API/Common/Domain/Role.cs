namespace LogisticPlatform.API.Common.Domain;

internal sealed class Role
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private Role() { }

    public Role(string name)
    {
        Id = Guid.NewGuid();
        Name = name.ToUpperInvariant();
    }
}
