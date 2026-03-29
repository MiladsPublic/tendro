using Samba.ApiServer.Modern.Contracts;
using Samba.ApiServer.Modern.Data;
using System.Globalization;

namespace Samba.ApiServer.Modern.Services;

/// <summary>
/// Phase 2: Domain Service Interfaces
/// Core business logic for tickets, orders, and payments
/// </summary>
/// 
/// <summary>Ticket domain operations (create, read, update, close)</summary>
public interface ITicketDomainService
{
    /// <summary>Create a new ticket for the given terminal/department</summary>
    Task<TicketDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default);

    /// <summary>Get ticket by ID with orders and payments</summary>
    Task<TicketDto?> GetTicketAsync(int ticketId, CancellationToken ct = default);

    /// <summary>List open tickets for the given department/terminal</summary>
    Task<PagedResponse<TicketDto>> ListOpenTicketsAsync(int departmentId, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>Add order line item to ticket</summary>
    Task<TicketDto> AddOrderAsync(int ticketId, AddOrderRequest request, CancellationToken ct = default);

    /// <summary>Update ticket state (e.g., "Kitchen Status" -> "Ready")</summary>
    Task<TicketDto> UpdateTicketStateAsync(int ticketId, UpdateTicketStateRequest request, CancellationToken ct = default);

    /// <summary>Close ticket after payment</summary>
    Task<TicketDto> CloseTicketAsync(int ticketId, CancellationToken ct = default);
}

/// <summary>Order domain operations</summary>
public interface IOrderDomainService
{
    /// <summary>Get order details</summary>
    Task<OrderDto?> GetOrderAsync(int orderId, CancellationToken ct = default);

    /// <summary>Mark order state (e.g., completed, ready for pickup)</summary>
    Task<OrderDto> UpdateOrderStateAsync(int orderId, UpdateOrderStateRequest request, CancellationToken ct = default);

    /// <summary>Void order (revert if payment hasn't been applied)</summary>
    Task<OrderDto> VoidOrderAsync(int orderId, CancellationToken ct = default);
}

/// <summary>Payment domain operations</summary>
public interface IPaymentDomainService
{
    /// <summary>Process payment with idempotency key</summary>
    Task<PaymentDto> ProcessPaymentAsync(int ticketId, ProcessPaymentRequest request, CancellationToken ct = default);

    /// <summary>Get payment details</summary>
    Task<PaymentDto?> GetPaymentAsync(int paymentId, CancellationToken ct = default);

    /// <summary>Refund payment (if allowed by business rules)</summary>
    Task<PaymentDto> RefundPaymentAsync(int paymentId, RefundPaymentRequest request, CancellationToken ct = default);

    /// <summary>List payments for ticket</summary>
    Task<IReadOnlyList<PaymentDto>> ListTicketPaymentsAsync(int ticketId, CancellationToken ct = default);
}

// ============================================================
// Request DTOs (Phase 2)
// ============================================================

/// <summary>Create ticket request</summary>
public sealed record CreateTicketRequest(
    int DepartmentId,
    int TerminalId,
    int? TicketTypeId = null);

/// <summary>Add order to ticket request</summary>
public sealed record AddOrderRequest(
    int MenuItemId,
    decimal Quantity,
    string? PortionName = null,
    IReadOnlyDictionary<string, string>? Tags = null);

/// <summary>Update ticket state request</summary>
public sealed record UpdateTicketStateRequest(
    string StateName,
    string StateValue);

/// <summary>Update order state request</summary>
public sealed record UpdateOrderStateRequest(
    string StateName,
    string StateValue);

/// <summary>Process payment request with idempotency key</summary>
public sealed record ProcessPaymentRequest(
    int PaymentTypeId,
    decimal Amount,
    decimal? TenderedAmount = null,
    string IdempotencyKey = "");

/// <summary>Refund payment request</summary>
public sealed record RefundPaymentRequest(
    string Reason,
    bool PrintReceipt = true);

// ============================================================
// Phase 2 Domain Service Implementations (Stub)
// Will integrate with Samba.Domain and database in Phase 2
// ============================================================

/// <summary>Placeholder for ticket domain logic (Phase 2 DB integration pending)</summary>
public class TicketDomainService : ITicketDomainService
{
    private readonly ILogger<TicketDomainService> _logger;
    private readonly ITicketRepository _ticketRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IMenuCatalogService _menuCatalogService;

    public TicketDomainService(
        ILogger<TicketDomainService> logger,
        ITicketRepository ticketRepo,
        IOrderRepository orderRepo,
        IMenuCatalogService menuCatalogService)
    {
        _logger = logger;
        _ticketRepo = ticketRepo;
        _orderRepo = orderRepo;
        _menuCatalogService = menuCatalogService;
    }

    public async Task<TicketDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating ticket for department {DepartmentId}", request.DepartmentId);
        
        var ticketNumber = "T-" + DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss-fff");
        var ticket = new TicketAggregate
        {
            TicketNumber = ticketNumber,
            CreatedAt = DateTime.UtcNow,
            TotalAmount = 0m,
            RemainingAmount = 0m,
            IsClosed = false,
            Orders = new List<OrderAggregate>(),
            Payments = new List<PaymentAggregate>()
        };
        
        await _ticketRepo.CreateAsync(ticket, ct);
        _logger.LogInformation("Created ticket {TicketNumber}", ticketNumber);
        return ticket.ToDto();
    }

    public async Task<TicketDto?> GetTicketAsync(int ticketId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving ticket {TicketId}", ticketId);
        var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
        return ticket?.ToDto();
    }

    public async Task<PagedResponse<TicketDto>> ListOpenTicketsAsync(int departmentId, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        _logger.LogDebug("Listing open tickets for department {DepartmentId}", departmentId);
        var (items, totalCount) = await _ticketRepo.ListOpenAsync(departmentId, pageNumber, pageSize, ct);
        return new PagedResponse<TicketDto>(
            Items: items.Select(t => t.ToDto()).ToList(),
            PageNumber: pageNumber,
            PageSize: pageSize,
            TotalCount: totalCount,
            TotalPages: (int)Math.Ceiling((double)totalCount / pageSize)
        );
    }

    public async Task<TicketDto> AddOrderAsync(int ticketId, AddOrderRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding order to ticket {TicketId}", ticketId);
        
        var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
        if (ticket == null)
        {
            _logger.LogWarning("Ticket {TicketId} not found", ticketId);
            throw new KeyNotFoundException($"Ticket {ticketId} not found");
        }

        var catalog = _menuCatalogService.Resolve(request.MenuItemId, request.PortionName, request.Tags);
        var order = new OrderAggregate
        {
            TicketId = ticketId,
            MenuItemId = request.MenuItemId,
            MenuItemName = catalog.MenuItemName,
            Quantity = request.Quantity,
            UnitPrice = catalog.UnitPrice,
            Status = "Pending"
        };

        var orders = ((IList<OrderAggregate>)ticket.Orders).ToList();
        orders.Add(order);

        ticket.Orders = orders;
        ticket.TotalAmount = orders.Sum(o => o.LineTotal);
        ticket.RemainingAmount = ticket.TotalAmount;

        await _ticketRepo.UpdateAsync(ticket, ct);
        _logger.LogInformation("Added order {OrderId} to ticket {TicketId}", order.Id, ticketId);
        return ticket.ToDto();
    }

    public async Task<TicketDto> UpdateTicketStateAsync(int ticketId, UpdateTicketStateRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating ticket {TicketId} state to {StateName}={StateValue}", ticketId, request.StateName, request.StateValue);
        
        var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
        if (ticket == null)
        {
            _logger.LogWarning("Ticket {TicketId} not found", ticketId);
            throw new KeyNotFoundException($"Ticket {ticketId} not found");
        }

        // State update would be stored in Phase 3 database
        // For Phase 2, just log it
        await _ticketRepo.UpdateAsync(ticket, ct);
        _logger.LogInformation("Updated ticket {TicketId} state", ticketId);
        return ticket.ToDto();
    }

    public async Task<TicketDto> CloseTicketAsync(int ticketId, CancellationToken ct = default)
    {
        _logger.LogInformation("Closing ticket {TicketId}", ticketId);
        
        var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
        if (ticket == null)
        {
            _logger.LogWarning("Ticket {TicketId} not found", ticketId);
            throw new KeyNotFoundException($"Ticket {ticketId} not found");
        }

        ticket.IsClosed = true;
        await _ticketRepo.UpdateAsync(ticket, ct);
        _logger.LogInformation("Closed ticket {TicketId}", ticketId);
        return ticket.ToDto();
    }
}

/// <summary>Placeholder for order domain logic</summary>
public class OrderDomainService : IOrderDomainService
{
    private readonly ILogger<OrderDomainService> _logger;
    private readonly IOrderRepository _orderRepo;

    public OrderDomainService(ILogger<OrderDomainService> logger, IOrderRepository orderRepo)
    {
        _logger = logger;
        _orderRepo = orderRepo;
    }

    public async Task<OrderDto?> GetOrderAsync(int orderId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving order {OrderId}", orderId);
        var order = await _orderRepo.GetByIdAsync(orderId, ct);
        return order?.ToDto();
    }

    public async Task<OrderDto> UpdateOrderStateAsync(int orderId, UpdateOrderStateRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating order {OrderId} state to {StateName}={StateValue}", orderId, request.StateName, request.StateValue);
        
        var order = await _orderRepo.GetByIdAsync(orderId, ct);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            throw new KeyNotFoundException($"Order {orderId} not found");
        }

        order.Status = request.StateValue;
        await _orderRepo.UpdateAsync(order, ct);
        _logger.LogInformation("Updated order {OrderId} state", orderId);
        return order.ToDto();
    }

    public async Task<OrderDto> VoidOrderAsync(int orderId, CancellationToken ct = default)
    {
        _logger.LogInformation("Voiding order {OrderId}", orderId);
        
        var order = await _orderRepo.GetByIdAsync(orderId, ct);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            throw new KeyNotFoundException($"Order {orderId} not found");
        }

        order.Status = "Voided";
        await _orderRepo.UpdateAsync(order, ct);
        _logger.LogInformation("Voided order {OrderId}", orderId);
        return order.ToDto();
    }
}

/// <summary>Placeholder for payment domain logic</summary>
public class PaymentDomainService : IPaymentDomainService
{
    private readonly ILogger<PaymentDomainService> _logger;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IIdempotencyService _idempotencyService;

    public PaymentDomainService(
        ILogger<PaymentDomainService> logger,
        IPaymentRepository paymentRepo,
        IIdempotencyService idempotencyService)
    {
        _logger = logger;
        _paymentRepo = paymentRepo;
        _idempotencyService = idempotencyService;
    }

    public async Task<PaymentDto> ProcessPaymentAsync(int ticketId, ProcessPaymentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing payment for ticket {TicketId} with idempotency key {IdempotencyKey}",
            ticketId, request.IdempotencyKey);

        // Check idempotency cache first
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var cached = await _idempotencyService.GetResultAsync(request.IdempotencyKey, ct);
            if (cached != null)
            {
                _logger.LogInformation("Idempotent payment hit cache for key {IdempotencyKey}", request.IdempotencyKey);
                return cached;
            }
        }

        // Create new payment
        var payment = new PaymentAggregate
        {
            TicketId = ticketId,
            Amount = request.Amount,
            ProcessedAt = DateTime.UtcNow,
            PaymentType = request.PaymentTypeId == 1 ? "Cash" : "Card"
        };

        await _paymentRepo.CreateAsync(payment, ct);

        // Cache result if idempotency key provided
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            await _idempotencyService.StoreResultAsync(request.IdempotencyKey, payment.ToDto(), TimeSpan.FromHours(24), ct);
        }

        _logger.LogInformation("Created payment {PaymentId} for ticket {TicketId}", payment.Id, ticketId);
        return payment.ToDto();
    }

    public async Task<PaymentDto?> GetPaymentAsync(int paymentId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving payment {PaymentId}", paymentId);
        var payment = await _paymentRepo.GetByIdAsync(paymentId, ct);
        return payment?.ToDto();
    }

    public async Task<PaymentDto> RefundPaymentAsync(int paymentId, RefundPaymentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Refunding payment {PaymentId} - Reason: {Reason}", paymentId, request.Reason);
        
        var payment = await _paymentRepo.GetByIdAsync(paymentId, ct);
        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found", paymentId);
            throw new KeyNotFoundException($"Payment {paymentId} not found");
        }

        // Create refund payment record
        var refund = new PaymentAggregate
        {
            TicketId = payment.TicketId,
            Amount = -payment.Amount, // Negative to indicate refund
            ProcessedAt = DateTime.UtcNow,
            PaymentType = "Refund"
        };
        
        await _paymentRepo.CreateAsync(refund, ct);
        _logger.LogInformation("Refunded payment {PaymentId}", paymentId);
        return refund.ToDto();
    }

    public async Task<IReadOnlyList<PaymentDto>> ListTicketPaymentsAsync(int ticketId, CancellationToken ct = default)
    {
        _logger.LogDebug("Listing payments for ticket {TicketId}", ticketId);
        var payments = await _paymentRepo.ListByTicketAsync(ticketId, ct);
        return payments.Select(p => p.ToDto()).ToList();
    }
}

// ============================================================
// Repository Interfaces (Phase 2 Data Access)
// ============================================================

/// <summary>Ticket data access abstraction</summary>
public interface ITicketRepository
{
    Task<TicketAggregate?> GetByIdAsync(int ticketId, CancellationToken ct = default);
    Task<(IEnumerable<TicketAggregate> items, long totalCount)> ListOpenAsync(int departmentId, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<TicketAggregate> CreateAsync(TicketAggregate ticket, CancellationToken ct = default);
    Task<TicketAggregate> UpdateAsync(TicketAggregate ticket, CancellationToken ct = default);
}

/// <summary>Order data access abstraction</summary>
public interface IOrderRepository
{
    Task<OrderAggregate?> GetByIdAsync(int orderId, CancellationToken ct = default);
    Task<IEnumerable<OrderAggregate>> ListByTicketAsync(int ticketId, CancellationToken ct = default);
    Task<OrderAggregate> CreateAsync(OrderAggregate order, CancellationToken ct = default);
    Task<OrderAggregate> UpdateAsync(OrderAggregate order, CancellationToken ct = default);
}

/// <summary>Payment data access abstraction</summary>
public interface IPaymentRepository
{
    Task<PaymentAggregate?> GetByIdAsync(int paymentId, CancellationToken ct = default);
    Task<IEnumerable<PaymentAggregate>> ListByTicketAsync(int ticketId, CancellationToken ct = default);
    Task<PaymentAggregate> CreateAsync(PaymentAggregate payment, CancellationToken ct = default);
}

/// <summary>Idempotency key tracking</summary>
public interface IIdempotencyService
{
    Task<PaymentDto?> GetResultAsync(string idempotencyKey, CancellationToken ct = default);
    Task StoreResultAsync(string idempotencyKey, PaymentDto result, TimeSpan ttl, CancellationToken ct = default);
}

/// <summary>Menu catalog lookup abstraction for item naming and pricing.</summary>
public interface IMenuCatalogService
{
    (string MenuItemName, decimal UnitPrice) Resolve(int menuItemId, string? portionName, IReadOnlyDictionary<string, string>? tags);
}

// ============================================================
// Aggregate Root Stubs (Phase 2 Domain Models)
// Will replace with Samba.Domain models
// ============================================================

public class TicketAggregate
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<OrderAggregate> Orders { get; set; } = new List<OrderAggregate>();
    public ICollection<PaymentAggregate> Payments { get; set; } = new List<PaymentAggregate>();
    public decimal TotalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public bool IsClosed { get; set; }

    public TicketDto ToDto() => new(
        Id: Id,
        TicketNumber: TicketNumber,
        IssuedAt: CreatedAt,
        TotalAmount: TotalAmount,
        RemainingAmount: RemainingAmount,
        IsClosed: IsClosed,
        Orders: Orders.Select(o => o.ToDto()).ToList(),
        Payments: Payments.Select(p => p.ToDto()).ToList()
    );
}

public class OrderAggregate
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
    public string Status { get; set; } = "Pending";

    public OrderDto ToDto() => new(
        Id: Id,
        MenuItemId: MenuItemId,
        MenuItemName: MenuItemName,
        Quantity: Quantity,
        UnitPrice: UnitPrice,
        LineTotal: LineTotal,
        Status: Status
    );
}

public class PaymentAggregate
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string PaymentType { get; set; } = string.Empty;

    public PaymentDto ToDto() => new(
        Id: Id,
        Amount: Amount,
        ProcessedAt: ProcessedAt,
        PaymentType: PaymentType
    );
}

public class MenuCatalogService : IMenuCatalogService
{
    private static readonly IReadOnlyDictionary<int, (string Name, decimal Price)> Catalog =
        new Dictionary<int, (string Name, decimal Price)>
        {
            [100] = ("Fireline Burger", 16.50m),
            [101] = ("Skewer Plate", 21.00m),
            [200] = ("Garden Citrus", 10.50m),
            [201] = ("Halloumi Crunch", 13.00m),
            [300] = ("Flat White", 4.20m),
            [301] = ("Cold Brew Tonic", 5.40m),
            [400] = ("Burnt Cheesecake", 8.80m),
            [401] = ("Affogato", 6.50m),
        };

    public (string MenuItemName, decimal UnitPrice) Resolve(int menuItemId, string? portionName, IReadOnlyDictionary<string, string>? tags)
    {
        if (tags != null && tags.TryGetValue("unitPrice", out var rawPrice) &&
            decimal.TryParse(rawPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedPrice) && parsedPrice > 0)
        {
            var tagName = !string.IsNullOrWhiteSpace(portionName) ? portionName! : $"Item {menuItemId}";
            return (tagName, parsedPrice);
        }

        if (Catalog.TryGetValue(menuItemId, out var item))
        {
            return (item.Name, item.Price);
        }

        var fallbackName = !string.IsNullOrWhiteSpace(portionName) ? portionName! : $"Item {menuItemId}";
        return (fallbackName, 1.00m);
    }
}
