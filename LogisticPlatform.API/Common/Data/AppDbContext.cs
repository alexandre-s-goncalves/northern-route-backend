using LogisticPlatform.API.Common.Domain;
using Microsoft.EntityFrameworkCore;

namespace LogisticPlatform.API.Common.Data;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        var adminRoleId = new Guid("e7b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d");
        var userRoleId = new Guid("b8f2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d");

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasData(
                new { Id = adminRoleId, Name = "ADMIN" },
                new { Id = userRoleId, Name = "USER" }
            );
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
            entity.Property(e => e.PasswordHash).IsRequired();

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasOne(e => e.Role)
                  .WithMany()
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(
                new
                {
                    Id = new Guid("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"),
                    Name = "Alexandre Santos",
                    Email = "ale@ale.com",
                    PasswordHash = "Password123",
                    RoleId = adminRoleId
                },
                new
                {
                    Id = new Guid("c2b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"),
                    Name = "John Doe Operator",
                    Email = "operator@northernroute.com",
                    PasswordHash = "Operator123",
                    RoleId = userRoleId
                }
            );
        });
    }
}
