using System.Diagnostics;
using Samba.ApiServer.Modern.Services;
using Samba.ApiServer.Modern.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace Samba.ApiServer.Modern.Middleware;

/// <summary>Global exception handler for standardized error responses</summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception: {ExceptionType}: {Message}",
            exception.GetType().Name, exception.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var error = new ErrorResponse(
            Error: "InternalServerError",
            Message: "An unexpected error occurred",
            TraceId: context.TraceIdentifier
        );

        await context.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }
}

/// 
/// <summary>Middleware for correlating requests across the system</summary>
public class RequestCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationMiddleware> _logger;

    public RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRequestCorrelationService correlationService)
    {
        var correlationId = correlationService.GetOrCreateCorrelationId(context);
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestId"] = context.TraceIdentifier
        }))
        {
            _logger.LogDebug("Request correlation ID: {CorrelationId}", correlationId);
            await _next(context);
        }
    }
}

/// <summary>Middleware for detailed request and response logging</summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            // Log incoming request
            var requestBody = await ReadRequestBodyAsync(context.Request);
            LogRequest(context, requestBody);

            // Capture response
            using (var memoryStream = new MemoryStream())
            {
                context.Response.Body = memoryStream;

                await _next(context);

                // Log outgoing response
                var responseBody = await ReadResponseBodyAsync(memoryStream);
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);

                stopwatch.Stop();
                LogResponse(context, responseBody, stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Exception during request processing - {Method} {Path} ({Elapsed}ms)",
                context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private void LogRequest(HttpContext context, string body)
    {
        var request = context.Request;
        
        _logger.LogInformation(
            "HTTP Request: {Method} {Path} - Headers: {HeaderCount}, Body Size: {BodySize}",
            request.Method,
            request.Path,
            request.Headers.Count,
            body.Length
        );

        if (!string.IsNullOrEmpty(body) && body.Length < 500)
        {
            _logger.LogDebug("Request Body: {Body}", body);
        }
    }

    private void LogResponse(HttpContext context, string body, TimeSpan elapsed)
    {
        var response = context.Response;
        
        _logger.LogInformation(
            "HTTP Response: {StatusCode} - Elapsed: {ElapsedMs}ms, Body Size: {BodySize}",
            response.StatusCode,
            elapsed.TotalMilliseconds,
            body.Length
        );

        if  (!string.IsNullOrEmpty(body) && body.Length < 500 && response.StatusCode >= 400)
        {
            _logger.LogDebug("Response Body: {Body}", body);
        }
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using (var reader = new StreamReader(request.Body, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }
    }

    private async Task<string> ReadResponseBodyAsync(MemoryStream memoryStream)
    {
        memoryStream.Position = 0;
        using (var reader = new StreamReader(memoryStream, leaveOpen: true))
        {
            return await reader.ReadToEndAsync();
        }
    }
}

/// <summary>Exception handling middleware with structured logging</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {ExceptionType}: {Message}",
                ex.GetType().Name, ex.Message);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var error = new
            {
                error = "InternalServerError",
                message = "An unexpected error occurred",
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(error);
        }
    }
}

/// <summary>Performance monitoring middleware for metrics collection</summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
    private readonly IHealthService _healthService;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMonitoringMiddleware> logger,
        IHealthService healthService)
    {
        _next = next;
        _logger = logger;
        _healthService = healthService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Record metrics (implementation depends on IHealthService)
            if (_healthService is HealthService hs)
            {
                hs.RecordRequest((decimal)stopwatch.Elapsed.TotalMilliseconds);
            }

            // Log slow requests
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                _logger.LogWarning(
                    "Slow request: {Method} {Path} took {ElapsedMs}ms",
                    context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
