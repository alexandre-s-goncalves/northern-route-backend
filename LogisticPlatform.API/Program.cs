using System;
using System.IO;
using System.Linq;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Data.Seeding;
using LogisticPlatform.API.Common.Domain;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Services;
using LogisticPlatform.API.Features.Auth.Mfa.Services;
using LogisticPlatform.API.Features.Auth.Refresh.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

var envFileRoots = new[]
{
    Directory.GetCurrentDirectory(),
    Path.Combine(Directory.GetCurrentDirectory(), "LogisticPlatform.API"),
    AppContext.BaseDirectory,
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../")),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"))
};
var envFileCandidates = envFileRoots
    .Where(root => !string.IsNullOrWhiteSpace(root))
    .Select(root => Path.Combine(root, ".env"))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Append(Path.Combine(Directory.GetCurrentDirectory(), "../.env"));
var envFilePath = envFileCandidates.FirstOrDefault(File.Exists);
if (envFilePath is not null)
{
    DotNetEnv.Env.NoClobber().Load(envFilePath);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<DevelopmentDataSeeder>();
builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IDeviceDetectorService, DeviceDetectorService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IRefreshService, RefreshService>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();

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

builder.Services.AddAppDbContext(builder.Configuration, builder.Environment);

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

    var developmentDataSeeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
    await developmentDataSeeder.SeedAsync(app.Environment.IsDevelopment());
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
