using Microsoft.EntityFrameworkCore;
using LogisticPlatform.API.Common;
using LogisticPlatform.API.Common.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

await app.RunAsync();
