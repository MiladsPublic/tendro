using Samba.ApiServer.Modern.Services;
using Samba.ApiServer.Modern.Contracts;

namespace Samba.ApiServer.Modern.Data;

/// <summary>
/// Phase 2: Placeholder Repository Implementations
/// Will be replaced with EF Core implementations in Phase 2 production code
/// </summary>
/// 
/// <summary>In-memory ticket repository (Phase 2 placeholder)</summary>
public class InMemoryTicketRepository : ITicketRepository
{
    private readonly Dictionary<int, TicketAggregate> _tickets = new();
    private int _nextId = 1;

    public async Task<TicketAggregate?> GetByIdAsync(int ticketId, CancellationToken ct = default)
    {
        await Task.Delay(10, ct); // Simulate I/O
        _tickets.TryGetValue(ticketId, out var ticket);
        return ticket;
    }

    public async Task<(IEnumerable<TicketAggregate> items, long totalCount)> ListOpenAsync(
        int departmentId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        var items = _tickets.Values
            .Where(t => !t.IsClosed)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);
        return (items, _tickets.Values.Count(t => !t.IsClosed));
    }

    public async Task<TicketAggregate> CreateAsync(TicketAggregate ticket, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        ticket.Id = _nextId++;
        _tickets[ticket.Id] = ticket;
        return ticket;
    }

    public async Task<TicketAggregate> UpdateAsync(TicketAggregate ticket, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        _tickets[ticket.Id] = ticket;
        return ticket;
    }
}

/// <summary>In-memory order repository (Phase 2 placeholder)</summary>
public class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<int, OrderAggregate> _orders = new();
    private int _nextId = 1;

    public async Task<OrderAggregate?> GetByIdAsync(int orderId, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        _orders.TryGetValue(orderId, out var order);
        return order;
    }

    public async Task<IEnumerable<OrderAggregate>> ListByTicketAsync(int ticketId, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        // Phase 2: Will implement ticket relationship
        return _orders.Values;
    }

    public async Task<OrderAggregate> CreateAsync(OrderAggregate order, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        order.Id = _nextId++;
        _orders[order.Id] = order;
        return order;
    }

    public async Task<OrderAggregate> UpdateAsync(OrderAggregate order, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        _orders[order.Id] = order;
        return order;
    }
}

/// <summary>In-memory payment repository (Phase 2 placeholder)</summary>
public class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly Dictionary<int, PaymentAggregate> _payments = new();
    private int _nextId = 1;

    public async Task<PaymentAggregate?> GetByIdAsync(int paymentId, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        _payments.TryGetValue(paymentId, out var payment);
        return payment;
    }

    public async Task<IEnumerable<PaymentAggregate>> ListByTicketAsync(int ticketId, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        // Phase 2: Will implement ticket relationship
        return _payments.Values;
    }

    public async Task<PaymentAggregate> CreateAsync(PaymentAggregate payment, CancellationToken ct = default)
    {
        await Task.Delay(5, ct);
        payment.Id = _nextId++;
        _payments[payment.Id] = payment;
        return payment;
    }
}

/// <summary>In-memory idempotency service (Phase 2 placeholder)</summary>
public class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly Dictionary<string, (PaymentDto result, DateTime expiresAt)> _cache = new();

    public async Task<PaymentDto?> GetResultAsync(string idempotencyKey, CancellationToken ct = default)
    {
        await Task.Delay(1, ct);
        
        if (_cache.TryGetValue(idempotencyKey, out var entry))
        {
            if (entry.expiresAt > DateTime.UtcNow)
                return entry.result;
            
            _cache.Remove(idempotencyKey);
        }
        
        return null;
    }

    public async Task StoreResultAsync(string idempotencyKey, PaymentDto result, TimeSpan ttl, CancellationToken ct = default)
    {
        await Task.Delay(1, ct);
        _cache[idempotencyKey] = (result, DateTime.UtcNow + ttl);
    }
}
