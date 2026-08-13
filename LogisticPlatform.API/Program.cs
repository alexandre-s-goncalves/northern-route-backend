
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using Scalar.AspNetCore;
using Microsoft.Extensions.Hosting;

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

builder.Services.AddEndpointsApiExplorer();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("NorthernRoute Docs")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

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
