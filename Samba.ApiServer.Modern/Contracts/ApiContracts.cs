namespace Samba.ApiServer.Modern.Contracts;

/// <summary>
/// Phase 1: Standard API response contracts for domain operations
/// </summary>

/// <summary>Health status response with component details</summary>
public sealed record HealthResponse(
    string Status,
    DateTimeOffset UtcNow,
    IReadOnlyDictionary<string, ComponentHealth> Components,
    TimeSpan Uptime);

public sealed record ComponentHealth(
    string Status,
    string? Message = null);

/// <summary>System information and version metadata</summary>
public sealed record SystemInfoResponse(
    string Service,
    string Version,
    string Environment,
    string Framework,
    DateTime BuildDate,
    string MachineName,
    DateTimeOffset UtcNow);

/// <summary>Metrics for monitoring and observability</summary>
public sealed record SystemMetricsResponse(
    long RequestsTotal,
    decimal RequestsPerSecond,
    decimal AverageLatencyMs,
    decimal ErrorRate,
    bool UpstreamDependenciesHealthy,
    DateTimeOffset Timestamp);

/// <summary>Standardized error response (RFC 7807 Problem Details)</summary>
public sealed record ErrorResponse(
    string Error,
    string Message,
    string? TraceId = null,
    IReadOnlyDictionary<string, object>? Details = null);

/// <summary>Login request contract</summary>
public sealed record LoginRequest(
    string Username,
    string Password);

/// <summary>Login response with bearer token</summary>
public sealed record LoginResponse(
    string Token,
    int ExpiresIn,
    string TokenType,
    UserInfo User);

/// <summary>Authenticated user context</summary>
public sealed record UserInfo(
    string Username,
    int UserId);

// Domain Model Request/Response Templates (for Phase 2)

/// <summary>Template for ticket read operations</summary>
public sealed record TicketDto(
    int Id,
    string TicketNumber,
    DateTime IssuedAt,
    decimal TotalAmount,
    decimal RemainingAmount,
    bool IsClosed,
    IReadOnlyList<OrderDto> Orders,
    IReadOnlyList<PaymentDto> Payments);

/// <summary>Template for order line items</summary>
public sealed record OrderDto(
    int Id,
    int MenuItemId,
    string MenuItemName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Status);

/// <summary>Template for payment records</summary>
public sealed record PaymentDto(
    int Id,
    decimal Amount,
    DateTime ProcessedAt,
    string PaymentType);

/// <summary>Ticket reprint request contract.</summary>
public sealed record ReprintTicketRequest(
    int TicketId,
    string? Reason = null,
    string? RequestedBy = null);

/// <summary>Print job response contract.</summary>
public sealed record PrintJobDto(
    long JobId,
    int TicketId,
    string JobType,
    string Status,
    DateTime CreatedAtUtc,
    string? Reason = null,
    string? RequestedBy = null);

/// <summary>Terminal agent heartbeat request payload.</summary>
public sealed record TerminalHeartbeatRequest(
    string TerminalId,
    string StationName,
    bool Online,
    int PendingQueueCount,
    string? AgentVersion = null);

/// <summary>Terminal agent heartbeat status payload.</summary>
public sealed record TerminalHeartbeatDto(
    string TerminalId,
    string StationName,
    bool Online,
    int PendingQueueCount,
    string? AgentVersion,
    DateTime LastSeenUtc);

/// <summary>Terminal offline queue event request payload.</summary>
public sealed record TerminalQueueEventRequest(
    string TerminalId,
    string EventType,
    string PayloadJson,
    string? CorrelationId = null);

/// <summary>Terminal offline queue event status payload.</summary>
public sealed record TerminalQueueEventDto(
    long EventId,
    string TerminalId,
    string EventType,
    string PayloadJson,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ReplayedAtUtc = null,
    string? CorrelationId = null,
    string? ReplayOutcome = null,
    string? ConflictReason = null);

/// <summary>Terminal queue replay execution result.</summary>
public sealed record TerminalQueueReplayResultDto(
    string TerminalId,
    int Requested,
    int Replayed,
    int Remaining,
    DateTime ExecutedAtUtc);

/// <summary>Standard pagination response wrapper</summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount,
    int TotalPages) where T : class;
