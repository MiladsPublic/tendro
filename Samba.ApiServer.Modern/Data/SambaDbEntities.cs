namespace Samba.ApiServer.Modern.Data;

/// <summary>
/// EF Core entity for Ticket aggregate persistence.
/// </summary>
public class TicketEntity
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public int TerminalId { get; set; }
    public int TicketTypeId { get; set; }
    public string StateName { get; set; } = "Open";
    public string? StateValue { get; set; }
    public bool IsClosed { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public ICollection<OrderEntity> Orders { get; set; } = new List<OrderEntity>();
    public ICollection<PaymentEntity> Payments { get; set; } = new List<PaymentEntity>();
}

/// <summary>
/// EF Core entity for Order (line item) persistence.
/// </summary>
public class OrderEntity
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int MenuItemId { get; set; }
    public string? PortionName { get; set; }
    public string? Tags { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Computed property for line total
    public decimal LineTotal => (Quantity * UnitPrice) - (DiscountAmount ?? 0m);

    // Navigation property
    public TicketEntity? Ticket { get; set; }
}

/// <summary>
/// EF Core entity for Payment persistence.
/// </summary>
public class PaymentEntity
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int PaymentTypeId { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? TenderedAmount { get; set; }
    public decimal? ChangeAmount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation property
    public TicketEntity? Ticket { get; set; }
}

/// <summary>
/// Idempotency record for duplicate-safe payment writes with TTL support.
/// </summary>
public class IdempotencyRecord
{
    public int Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Terminal queue event for offline replay tracking.
/// </summary>
public class TerminalQueueEventEntity
{
    public long Id { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public string? CorrelationId { get; set; }
    public string? ReplayOutcome { get; set; }
    public string? ConflictReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReplayedAtUtc { get; set; }
}
