using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Samba.ApiServer.Modern.Middleware;
using Samba.ApiServer.Modern.Services;
using Samba.ApiServer.Modern.Contracts;
using Samba.ApiServer.Modern.Endpoints;
using Samba.ApiServer.Modern.Data;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Phase 1: Foundation Configuration
// ============================================================

// 1. Structured Logging
builder.Logging
    .ClearProviders()
    .AddConsole()
    .AddDebug()
    .AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });

// Note: Logging initialization deferred until after app.Build()

// 2. Core Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v2", new() 
    { 
        Title = "SambaPOS Modern API (v2)",
        Version = "2.0.0",
        Description = "Modern ASP.NET Core REST API for SambaPOS-3 migration",
        Contact = new() { Name = "SambaPOS Team" }
    });
    
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// 3. Health Checks with Metadata
builder.Services
    .AddHealthChecks()
    .AddCheck("database", new DatabaseHealthCheck(), tags: new[] { "ready", "live" })
    .AddCheck("services", new ServiceHealthCheck(), tags: new[] { "ready", "live" });

// 4. API Services (Phase 1 foundation)
builder.Services.AddScoped<ISystemService, SystemService>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IRequestCorrelationService, RequestCorrelationService>();
builder.Services.AddScoped<IAuthenticationService, BasicAuthenticationService>();
builder.Services.AddScoped<IHealthCheckService, AspNetCoreHealthCheckService>();

// Phase 2: Domain Services
builder.Services.AddScoped<ITicketDomainService, TicketDomainService>();
builder.Services.AddScoped<IOrderDomainService, OrderDomainService>();
builder.Services.AddScoped<IPaymentDomainService, PaymentDomainService>();
builder.Services.AddSingleton<IMenuCatalogService, MenuCatalogService>();
builder.Services.AddSingleton<IPrintService, PrintService>();
builder.Services.AddScoped<ITerminalAgentService, TerminalAgentService>();

// Phase 3: EF Core Database Integration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(local);Database=SambaPOS;Trusted_Connection=true;";

builder.Services.AddDbContext<SambaDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly("Samba.ApiServer.Modern");
        sqlOptions.CommandTimeout(30);
        sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
    })
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

// Phase 3: EF Core Repositories (replace in-memory implementations)
builder.Services.AddScoped<ITicketRepository, EfCoreTicketRepository>();
builder.Services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
builder.Services.AddScoped<IPaymentRepository, EfCorePaymentRepository>();
builder.Services.AddScoped<IIdempotencyService, EfCoreIdempotencyService>();

// 5. CORS for web clients (Phase 1)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowModernClient", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// 6. Exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ============================================================
// Build Application
// ============================================================

var app = builder.Build();

var logger2 = app.Services.GetRequiredService<ILogger<Program>>();

// ============================================================
// Phase 1: Middleware Pipeline
// ============================================================

// Exception handling
app.UseExceptionHandler();

// Structured request/response logging
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// Request correlation IDs
app.UseMiddleware<RequestCorrelationMiddleware>();

// CORS (enable web clients)
app.UseCors("AllowModernClient");

// Swagger documentation (all environments for ease of testing)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v2/swagger.json", "SambaPOS Modern API v2.0");
    options.RoutePrefix = "api-docs";
    options.DefaultModelsExpandDepth(2);
});

logger2.LogInformation("Middleware pipeline configured");

// ============================================================
// Phase 1: API Routes (v2, domain-oriented)
// ============================================================

// System & Health Group
var systemGroup = app.MapGroup("/api/v2/system")
    .WithTags("System");

systemGroup.MapGet("/health", HandleHealthCheck)
    .WithName("GetHealth")
    .WithSummary("Get system health status");

systemGroup.MapGet("/info", HandleSystemInfo)
    .WithName("GetSystemInfo")
    .WithSummary("Get system information and version");

systemGroup.MapGet("/health/ready", HandleReadinessProbe)
    .WithName("GetReadinessProbe")
    .WithSummary("Kubernetes-style readiness probe");

systemGroup.MapGet("/health/live", () => Results.Ok())
    .WithName("GetLivenessProbe")
    .WithSummary("Kubernetes-style liveness probe");

// Authentication Group (Phase 1)
var authGroup = app.MapGroup("/api/v2/auth")
    .WithTags("Authentication");

authGroup.MapPost("/login", HandleLogin)
    .WithName("Login")
    .WithSummary("Authenticate user and return session token");

authGroup.MapPost("/logout", (Delegate)HandleLogout)
    .WithName("Logout")
    .WithSummary("Invalidate user session")
    .RequireAuthorization();

// Health metrics endpoint for monitoring  
systemGroup.MapGet("/metrics", HandleMetrics)
    .WithName("GetMetrics")
    .WithSummary("Get system metrics (requests, latency, etc)");

// ============================================================
// Phase 2: Domain Endpoints (Tickets, Orders, Payments)
// ============================================================

app.MapTicketEndpoints();
app.MapPaymentEndpoints();
app.MapOrderEndpoints();
app.MapPrintEndpoints();
app.MapTerminalAgentEndpoints();

// Fallback 404 handler
app.MapFallback((HttpContext ctx) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("404 Not Found: {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
    
    return Results.NotFound(new ErrorResponse(
        Error: "NotFound",
        Message: $"The requested endpoint {ctx.Request.Method} {ctx.Request.Path} does not exist",
        TraceId: ctx.TraceIdentifier
    ));
});

logger2.LogInformation("API routes configured (v2)");

// ============================================================
// Phase 3: Database Initialization
// ============================================================

try
{
    logger2.LogInformation("Applying database migrations...");
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SambaDbContext>();
        await dbContext.Database.MigrateAsync();
        logger2.LogInformation("Database migrations applied successfully");
    }
}
catch (Exception ex)
{
    logger2.LogError(ex, "Failed to apply database migrations");
    throw;
}

// ============================================================
// Start Application
// ============================================================

logger2.LogInformation("Starting SambaPOS Modern API");
await app.RunAsync();

// ============================================================
// Endpoint Handlers (Domain-Oriented)
// ============================================================

async Task<IResult> HandleHealthCheck(
    [FromServices] IHealthService healthService,
    [FromServices] ILogger<Program> logger)
{
    try
    {
        var status = await healthService.GetStatusAsync();
        var response = new HealthResponse(
            Status: status.IsHealthy ? "ok" : "degraded",
            UtcNow: DateTimeOffset.UtcNow,
            Components: status.Components,
            Uptime: status.Uptime
        );
        
        logger.LogInformation("Health check: {Status}", response.Status);
        return status.IsHealthy 
            ? Results.Ok(response) 
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Health check failed");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

async Task<IResult> HandleSystemInfo(
    [FromServices] ISystemService systemService,
    [FromServices] IWebHostEnvironment env)
{
    var info = await systemService.GetSystemInfoAsync();
    return Results.Ok(new SystemInfoResponse(
        Service: "Samba.ApiServer.Modern",
        Version: "2.0.0",
        Environment: env.EnvironmentName,
        Framework: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        BuildDate: GetBuildDate(),
        MachineName: Environment.MachineName,
        UtcNow: DateTimeOffset.UtcNow
    ));
}

async Task<IResult> HandleReadinessProbe(
    [FromServices] IHealthService healthService)
{
    var status = await healthService.GetStatusAsync();
    return status.IsReady 
        ? Results.Ok() 
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}

async Task<IResult> HandleLogin(
    [FromBody] LoginRequest request,
    [FromServices] IAuthenticationService authService,
    [FromServices] ILogger<Program> logger)
{
    try
    {
        var result = await authService.AuthenticateAsync(request.Username, request.Password);
        if (result.IsSuccess)
        {
            logger.LogInformation("User {Username} logged in", request.Username);
            return Results.Ok(new LoginResponse(
                Token: result.Token ?? string.Empty,
                ExpiresIn: 1800,
                TokenType: "Bearer",
                User: new UserInfo(request.Username, result.UserId)
            ));
        }
        
        logger.LogWarning("Failed login attempt for user {Username}", request.Username);
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Login error");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

async Task<IResult> HandleLogout(HttpContext ctx)
{
    var userId = ctx.User.FindFirst("UserId")?.Value;
    ctx.Session.Clear();
    return Results.NoContent();
}

async Task<IResult> HandleMetrics(
    [FromServices] IHealthService healthService,
    [FromServices] ILogger<Program> logger)
{
    try
    {
        var metrics = await healthService.GetMetricsAsync();
        return Results.Ok(new SystemMetricsResponse(
            RequestsTotal: metrics.RequestsTotal,
            RequestsPerSecond: metrics.RequestsPerSecond,
            AverageLatencyMs: metrics.AverageLatencyMs,
            ErrorRate: metrics.ErrorRate,
            UpstreamDependenciesHealthy: metrics.UpstreamHealthy,
            Timestamp: DateTimeOffset.UtcNow
        ));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Metrics collection failed");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

static DateTime GetBuildDate()
{
    var assembly = Assembly.GetExecutingAssembly();
    var attr = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>();
    return File.GetLastWriteTimeUtc(assembly.Location);
}
