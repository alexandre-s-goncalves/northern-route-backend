using System;
using System.IO;
using System.Linq;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Services;
using LogisticPlatform.API.Features.Auth.Mfa.Services;
using LogisticPlatform.API.Features.Auth.Refresh.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

var envFileCandidates = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "LogisticPlatform.API", ".env"),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.env"))
};
var envFilePath = envFileCandidates.FirstOrDefault(File.Exists);
if (envFilePath is not null)
{
    DotNetEnv.Env.NoClobber().Load(envFilePath);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IDeviceDetectorService, DeviceDetectorService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IRefreshService, RefreshService>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.Configure<SmtpOptions>(options =>
{
    var configuredOptions = SmtpOptions.FromConfiguration(builder.Configuration);
    options.FromAddress = configuredOptions.FromAddress;
    options.FromName = configuredOptions.FromName;
    options.Host = configuredOptions.Host;
    options.Password = configuredOptions.Password;
    options.Port = configuredOptions.Port;
    options.Username = configuredOptions.Username;
});
builder.Services.AddScoped<IEmailService, EmailService>();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.RegisterModules();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health_Check")
    .WithSummary("Checks whether the API is running")
    .Produces(StatusCodes.Status200OK);

await app.RunAsync();

public partial class Program
{
    protected Program() { }
}
