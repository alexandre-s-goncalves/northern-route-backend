using System.Threading.Tasks;
using LogisticPlatform.API.Common.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogisticPlatform.Tests;

public sealed class DatabaseTestFixture : IAsyncLifetime
{
    internal AppDbContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var testOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistic_platform_test_{Guid.NewGuid():N}")
            .Options;

        Context = new AppDbContext(testOptions);
        await Context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (Context != null)
        {
            await Context.DisposeAsync();
        }
    }
}
