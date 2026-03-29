using Microsoft.EntityFrameworkCore;
using Samba.ApiServer.Modern.Contracts;
using Samba.ApiServer.Modern.Services;

namespace Samba.ApiServer.Modern.Data;

/// <summary>
/// EF Core-backed ticket repository implementation.
/// Replaces InMemoryTicketRepository for persistent storage.
/// </summary>
public class EfCoreTicketRepository : ITicketRepository
{
    private readonly SambaDbContext _context;

    public EfCoreTicketRepository(SambaDbContext context)
    {
        _context = context;
    }

    public async Task<TicketAggregate?> GetByIdAsync(int ticketId, CancellationToken ct = default)
    {
        var entity = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Orders)
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<(IEnumerable<TicketAggregate> items, long totalCount)> ListOpenAsync(int departmentId, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = _context.Tickets
            .Where(t => !t.IsClosed && t.DepartmentId == departmentId)
            .OrderByDescending(t => t.CreatedAtUtc);

        var totalCount = await query.CountAsync(ct);
        var entities = await query
            .AsNoTracking()
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.Orders)
            .Include(t => t.Payments)
            .ToListAsync(ct);

        var aggregates = entities.Select(MapToDomain).ToList();
        return (aggregates, totalCount);
    }

    public async Task<TicketAggregate> CreateAsync(TicketAggregate ticket, CancellationToken ct = default)
    {
        var entity = MapToEntity(ticket);
        _context.Tickets.Add(entity);
        await _context.SaveChangesAsync(ct);
        ticket.Id = entity.Id;
        return ticket;
    }

    public async Task<TicketAggregate> UpdateAsync(TicketAggregate ticket, CancellationToken ct = default)
    {
        var entity = MapToEntity(ticket);
        _context.Tickets.Update(entity);
        await _context.SaveChangesAsync(ct);
        return ticket;
    }

    private TicketAggregate MapToDomain(TicketEntity entity)
    {
        return new TicketAggregate
        {
            Id = entity.Id,
            TicketNumber = entity.TicketNumber,
            CreatedAt = entity.CreatedAtUtc,
            IsClosed = entity.IsClosed,
            TotalAmount = entity.TotalAmount,
            RemainingAmount = entity.TotalAmount - (entity.Payments?.Sum(p => p.Amount) ?? 0m),
            Orders = entity.Orders?.Select(o => new OrderAggregate
            {
                Id = o.Id,
                TicketId = o.TicketId,
                MenuItemId = o.MenuItemId,
                MenuItemName = o.PortionName ?? "Item",
                Quantity = o.Quantity,
                UnitPrice = o.UnitPrice,
                Status = o.Status
            }).ToList() ?? new List<OrderAggregate>(),
            Payments = entity.Payments?.Select(p => new PaymentAggregate
            {
                Id = p.Id,
                TicketId = p.TicketId,
                Amount = p.Amount,
                ProcessedAt = p.CreatedAtUtc,
                PaymentType = p.PaymentType
            }).ToList() ?? new List<PaymentAggregate>()
        };
    }

    private TicketEntity MapToEntity(TicketAggregate aggregate)
    {
        return new TicketEntity
        {
            Id = aggregate.Id,
            TicketNumber = aggregate.TicketNumber,
            DepartmentId = 1,
            TerminalId = 1,
            TicketTypeId = 1,
            StateName = "Open",
            StateValue = null,
            IsClosed = aggregate.IsClosed,
            TotalAmount = aggregate.TotalAmount,
            CreatedAtUtc = aggregate.CreatedAt,
            UpdatedAtUtc = DateTime.UtcNow,
            Orders = aggregate.Orders?.Select(o => new OrderEntity
            {
                Id = o.Id,
                TicketId = aggregate.Id,
                MenuItemId = o.MenuItemId,
                PortionName = o.MenuItemName,
                Tags = null,
                Quantity = o.Quantity,
                UnitPrice = o.UnitPrice,
                DiscountAmount = null,
                TaxAmount = null,
                Status = o.Status,
                Note = null,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }).ToList() ?? new List<OrderEntity>(),
            Payments = aggregate.Payments?.Select(p => new PaymentEntity
            {
                Id = p.Id,
                TicketId = aggregate.Id,
                PaymentTypeId = 1,
                PaymentType = p.PaymentType,
                Amount = p.Amount,
                TenderedAmount = null,
                ChangeAmount = null,
                ReferenceNumber = null,
                Reason = null,
                CreatedAtUtc = p.ProcessedAt,
                UpdatedAtUtc = DateTime.UtcNow
            }).ToList() ?? new List<PaymentEntity>()
        };
    }
}

public class EfCoreOrderRepository : IOrderRepository
{
    private readonly SambaDbContext _context;

    public EfCoreOrderRepository(SambaDbContext context)
    {
        _context = context;
    }

    public async Task<OrderAggregate?> GetByIdAsync(int orderId, CancellationToken ct = default)
    {
        var entity = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IEnumerable<OrderAggregate>> ListByTicketAsync(int ticketId, CancellationToken ct = default)
    {
        var entities = await _context.Orders
            .Where(o => o.TicketId == ticketId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);
        return entities.Select(MapToDomain);
    }

    public async Task<OrderAggregate> CreateAsync(OrderAggregate order, CancellationToken ct = default)
    {
        var entity = MapToEntity(order);
        _context.Orders.Add(entity);
        await _context.SaveChangesAsync(ct);
        order.Id = entity.Id;
        return order;
    }

    public async Task<OrderAggregate> UpdateAsync(OrderAggregate order, CancellationToken ct = default)
    {
        var entity = MapToEntity(order);
        _context.Orders.Update(entity);
        await _context.SaveChangesAsync(ct);
        return order;
    }

    private OrderAggregate MapToDomain(OrderEntity entity)
    {
        return new OrderAggregate
        {
            Id = entity.Id,
            TicketId = entity.TicketId,
            MenuItemId = entity.MenuItemId,
            MenuItemName = entity.PortionName ?? "Item",
            Quantity = entity.Quantity,
            UnitPrice = entity.UnitPrice,
            Status = entity.Status
        };
    }

    private OrderEntity MapToEntity(OrderAggregate aggregate)
    {
        return new OrderEntity
        {
            Id = aggregate.Id,
            TicketId = aggregate.TicketId,
            MenuItemId = aggregate.MenuItemId,
            PortionName = aggregate.MenuItemName,
            Tags = null,
            Quantity = aggregate.Quantity,
            UnitPrice = aggregate.UnitPrice,
            DiscountAmount = null,
            TaxAmount = null,
            Status = aggregate.Status,
            Note = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}

/// <summary>
/// EF Core-backed payment repository with idempotency support.
/// </summary>
public class EfCorePaymentRepository : IPaymentRepository
{
    private readonly SambaDbContext _context;

    public EfCorePaymentRepository(SambaDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentAggregate?> GetByIdAsync(int paymentId, CancellationToken ct = default)
    {
        var entity = await _context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IEnumerable<PaymentAggregate>> ListByTicketAsync(int ticketId, CancellationToken ct = default)
    {
        var entities = await _context.Payments
            .Where(p => p.TicketId == ticketId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);
        return entities.Select(MapToDomain);
    }

    public async Task<PaymentAggregate> CreateAsync(PaymentAggregate payment, CancellationToken ct = default)
    {
        var entity = MapToEntity(payment);
        _context.Payments.Add(entity);
        await _context.SaveChangesAsync(ct);
        payment.Id = entity.Id;
        return payment;
    }

    private PaymentAggregate MapToDomain(PaymentEntity entity)
    {
        return new PaymentAggregate
        {
            Id = entity.Id,
            TicketId = entity.TicketId,
            Amount = entity.Amount,
            ProcessedAt = entity.CreatedAtUtc,
            PaymentType = entity.PaymentType
        };
    }

    private PaymentEntity MapToEntity(PaymentAggregate aggregate)
    {
        return new PaymentEntity
        {
            Id = aggregate.Id,
            TicketId = aggregate.TicketId,
            PaymentTypeId = 1,
            PaymentType = aggregate.PaymentType,
            Amount = aggregate.Amount,
            TenderedAmount = null,
            ChangeAmount = null,
            ReferenceNumber = null,
            Reason = null,
            CreatedAtUtc = aggregate.ProcessedAt,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}

/// <summary>
/// EF Core-backed idempotency service for duplicate-safe operations.
/// </summary>
public class EfCoreIdempotencyService : IIdempotencyService
{
    private readonly SambaDbContext _context;

    public EfCoreIdempotencyService(SambaDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentDto?> GetResultAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var record = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);

        if (record == null)
            return null;

        // Check TTL expiration
        if (DateTime.UtcNow > record.ExpiresAtUtc)
        {
            _context.IdempotencyRecords.Remove(record);
            await _context.SaveChangesAsync(ct);
            return null;
        }

        // Deserialize and return cached result
        return System.Text.Json.JsonSerializer.Deserialize<PaymentDto>(record.ResultJson);
    }

    public async Task StoreResultAsync(string idempotencyKey, PaymentDto result, TimeSpan ttl, CancellationToken ct = default)
    {
        var record = new IdempotencyRecord
        {
            IdempotencyKey = idempotencyKey,
            ResultJson = System.Text.Json.JsonSerializer.Serialize(result),
            ExpiresAtUtc = DateTime.UtcNow.Add(ttl),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.IdempotencyRecords.Add(record);
        await _context.SaveChangesAsync(ct);
    }
}
