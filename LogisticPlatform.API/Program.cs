using System;
using System.Linq;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Schemas;
using LogisticPlatform.API.Features.Auth.Login.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ITokenService, TokenService>();

var isTestRuntime = AppDomain.CurrentDomain.GetAssemblies()
    .Any(a => a.FullName != null && a.FullName.Contains("test", StringComparison.OrdinalIgnoreCase));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (isTestRuntime)
    {
        options.UseNpgsql("Host=localhost;Database=logistic_platform_test_stub;Username=postgres;Password=test");
    }
    else
    {
        var connectionString = builder.Configuration["JWT_SECRET_KEY"] != null
            ? builder.Configuration["DATABASE_URL"]
            : builder.Configuration.GetConnectionString("DefaultConnection");

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(5);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 2,
                maxRetryDelay: TimeSpan.FromSeconds(2),
                errorCodesToAdd: null);
        });
    }

    options.ConfigureWarnings(warnings => warnings.Ignore(
        Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

var allowedOriginsSetting = builder.Configuration["ALLOWED_ORIGINS"] ?? string.Empty;
var allowedOrigins = allowedOriginsSetting.Split(',', StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapPost("/api/auth/login", async (
    LoginRequestSchema request,
    ILoginService loginService,
    System.Threading.CancellationToken cancellationToken) =>
{
    var result = await loginService.ExecuteAsync(request, cancellationToken);
    return !result.IsSuccess ? Results.BadRequest(result) : Results.Ok(result);
});

await app.RunAsync();

public partial class Program
{
    protected Program() { }
}
