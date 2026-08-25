using DotNetEnv;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using LogisticPlatform.API.Common.Security;
using LogisticPlatform.API.Features.Auth.Login.Contracts;
using LogisticPlatform.API.Features.Auth.Login.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var environment = builder.Environment.EnvironmentName;

var envFile = environment switch
{
    var env when env == Environments.Staging => ".env.qa",
    var env when env == Environments.Production => ".env.prod",
    _ => ".env"
};

Env.Load(envFile);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPlatformPolicy", policy =>
    {
        var allowedOriginsSetting = builder.Configuration["ALLOWED_ORIGINS"] ?? string.Empty;
        var origins = allowedOriginsSetting.Split(',', StringSplitOptions.RemoveEmptyEntries);

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
});

builder.Services.AddEndpointsApiExplorer();

var isInMemoryTest = builder.Configuration["UseInMemoryTestDatabase"] == "true";

if (isInMemoryTest)
{
    builder.Services.AddDbContext<AppDbContext>();
}
else
{
    var connectionString = builder.Configuration["JWT_SECRET_KEY"] != null
        ? builder.Configuration["DATABASE_URL"]
        : builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(5);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 2,
                maxRetryDelay: TimeSpan.FromSeconds(2),
                errorCodesToAdd: null);
        }));
}

builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "NorthernRoute Logistics Platform API";
        document.Info.Version = "v1.0.0";
        document.Info.Description = "Enterprise offline-first logistics API engineered for remote supply chain synchronization using .NET 9 and PostgreSQL.";
        document.Info.Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Alexandre Gonçalves",
            Email = "alexandre.sgoncalves@outlook.com"
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseCors("CorsPlatformPolicy");

app.Use((context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.Headers.Append("Access-Control-Allow-Origin", context.Request.Headers.Origin);
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
        context.Response.Headers.Append("Access-Control-Allow-Methods", "DELETE, GET, OPTIONS, PATCH, POST, PUT");
        context.Response.StatusCode = 200;
        return Task.CompletedTask;
    }
    return next();
});

app.UseHttpsRedirection();

app.MapGet("/api/health", () =>
{
    var statusInfo = new { Status = "Online", Message = "Platform API is running successfully!" };
    var result = ResultSchema<object>.Success(statusInfo);
    return Results.Ok(result);
});

app.RegisterModules();

await app.RunAsync();

public partial class Program
{
    protected Program() { }
}
