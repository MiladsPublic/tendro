using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Samba.ApiServer.Modern.Services;

public interface IHealthCheckService
{
    Task<HealthReport> CheckHealthAsync();
}

/// <summary>Phase 1 Service Interfaces and Implementations</summary>
/// 
/// <summary>System information service</summary>
public interface ISystemService
{
    Task<SystemInformation> GetSystemInfoAsync();
}

public class SystemService : ISystemService
{
    private readonly ILogger<SystemService> _logger;
    private readonly IWebHostEnvironment _env;

    public SystemService(ILogger<SystemService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async Task<SystemInformation> GetSystemInfoAsync()
    {
        _logger.LogInformation("Retrieving system information");
        
        return await Task.FromResult(new SystemInformation
        {
            Environment = _env.EnvironmentName,
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            OSVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            RuntimeVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            UtcNow = DateTimeOffset.UtcNow
        });
    }
}

public class SystemInformation
{
    public required string Environment { get; set; }
    public required string MachineName { get; set; }
    public int ProcessorCount { get; set; }
    public required string OSVersion { get; set; }
    public required string RuntimeVersion { get; set; }
    public DateTimeOffset UtcNow { get; set; }
}

/// <summary>Health status and metrics service</summary>
public interface IHealthService
{
    Task<HealthStatus> GetStatusAsync();
    Task<SystemMetrics> GetMetricsAsync();
}

public class HealthService : IHealthService
{
    private readonly ILogger<HealthService> _logger;
    private readonly IHealthCheckService _healthCheckService;
    private readonly DateTime _startTime = DateTime.UtcNow;
    private long _requestCount = 0;
    private long _totalLatencyMs = 0;

    public HealthService(ILogger<HealthService> logger, IHealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    public async Task<HealthStatus> GetStatusAsync()
    {
        _logger.LogInformation("Health check initiated");
        
        var report = await _healthCheckService.CheckHealthAsync();
        var components = new Dictionary<string, Samba.ApiServer.Modern.Contracts.ComponentHealth>();
        
        foreach (var entry in report.Entries)
        {
            components[entry.Key] = new Samba.ApiServer.Modern.Contracts.ComponentHealth(
                Status: entry.Value.Status.ToString().ToLower(),
                Message: entry.Value.Description
            );
        }

        var isHealthy = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy;
        var isReady = report.Status != Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy;

        return new HealthStatus
        {
            IsHealthy = isHealthy,
            IsReady = isReady,
            Components = components,
            Uptime = DateTime.UtcNow - _startTime
        };
    }

    public async Task<SystemMetrics> GetMetricsAsync()
    {
        _logger.LogDebug("Collecting system metrics");
        
        return await Task.FromResult(new SystemMetrics
        {
            RequestsTotal = Interlocked.Read(ref _requestCount),
            RequestsPerSecond = CalculateRPS(),
            AverageLatencyMs = _requestCount > 0 ? ((decimal)_totalLatencyMs) / _requestCount : 0,
            ErrorRate = 0m, // Would be populated from request pipeline
            UpstreamHealthy = true, // Would check actual dependencies
            CollectedAt = DateTimeOffset.UtcNow
        });
    }

    private decimal CalculateRPS()
    {
        var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
        return elapsed > 0 ? (decimal)(Interlocked.Read(ref _requestCount) / elapsed) : 0;
    }

    internal void RecordRequest(decimal latencyMs)
    {
        Interlocked.Increment(ref _requestCount);
        Interlocked.Add(ref _totalLatencyMs, (long)latencyMs);
    }
}

/// <summary>Adapter for built-in ASP.NET Core HealthCheckService</summary>
public class AspNetCoreHealthCheckService : IHealthCheckService
{
    private readonly Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService _healthCheckService;

    public AspNetCoreHealthCheckService(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    public async Task<HealthReport> CheckHealthAsync()
    {
        return await _healthCheckService.CheckHealthAsync();
    }
}

public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public bool IsReady { get; set; }
    public required IReadOnlyDictionary<string, Samba.ApiServer.Modern.Contracts.ComponentHealth> Components { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class SystemMetrics
{
    public long RequestsTotal { get; set; }
    public decimal RequestsPerSecond { get; set; }
    public decimal AverageLatencyMs { get; set; }
    public decimal ErrorRate { get; set; }
    public bool UpstreamHealthy { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
}

/// <summary>Request correlation ID tracking</summary>
public interface IRequestCorrelationService
{
    string GetOrCreateCorrelationId(HttpContext context);
    void SetCorrelationId(HttpContext context, string correlationId);
}

public class RequestCorrelationService : IRequestCorrelationService
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string CorrelationIdKey = "CorrelationId";

    public string GetOrCreateCorrelationId(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[CorrelationIdKey] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId.ToString();

        return correlationId.ToString();
    }

    public void SetCorrelationId(HttpContext context, string correlationId)
    {
        context.Items[CorrelationIdKey] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;
    }
}

/// <summary>Authentication/Authorization service (Phase 1 foundation)</summary>
public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(string username, string password);
    Task<bool> ValidateTokenAsync(string token);
}

public class BasicAuthenticationService : IAuthenticationService
{
    private readonly ILogger<BasicAuthenticationService> _logger;

    public BasicAuthenticationService(ILogger<BasicAuthenticationService> logger)
    {
        _logger = logger;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
    {
        _logger.LogInformation("Authentication attempt for user: {Username}", username);
        
        // Phase 1: Basic in-memory authentication (temporary)
        // Phase 2: Will integrate with Samba.Domain.IUserService
        if (username == "admin" && password == "admin")
        {
            var token = GenerateToken(username, 1);
            return new AuthenticationResult
            {
                IsSuccess = true,
                Token = token,
                UserId = 1
            };
        }

        return new AuthenticationResult { IsSuccess = false };
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        // Phase 1: Basic validation
        // Phase 2: Will check JWT or session store
        return await Task.FromResult(!string.IsNullOrEmpty(token));
    }

    private string GenerateToken(string username, int userId)
    {
        // Phase 1: Simple token format
        // Phase 2: Will use JWT Bearer tokens
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{userId}:{DateTimeOffset.UtcNow}"));
    }
}

public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public string? Token { get; set; }
    public int UserId { get; set; }
}

// Health Check Implementations
public class DatabaseHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: Placeholder
        // Phase 2: Will check actual database connection
        return await Task.FromResult(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database connection OK")
        );
    }
}

public class ServiceHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: Placeholder
        // Phase 2: Will check dependent services
        return await Task.FromResult(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Services OK")
        );
    }
}
