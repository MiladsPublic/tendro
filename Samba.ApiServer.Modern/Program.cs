using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow)))
    .WithName("GetHealth")
    .WithTags("System");

app.MapGet("/api/v2/system/info", ([FromServices] IWebHostEnvironment env) =>
    Results.Ok(new SystemInfoResponse(
        Service: "Samba.ApiServer.Modern",
        Environment: env.EnvironmentName,
        Framework: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        MachineName: Environment.MachineName,
        UtcNow: DateTimeOffset.UtcNow)))
    .WithName("GetSystemInfo")
    .WithTags("System");

app.Run();

internal sealed record HealthResponse(string Status, DateTimeOffset UtcNow);

internal sealed record SystemInfoResponse(
    string Service,
    string Environment,
    string Framework,
    string MachineName,
    DateTimeOffset UtcNow);
