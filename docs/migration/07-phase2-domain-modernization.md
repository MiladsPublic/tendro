# Phase 2: Domain and Data Path Modernization

Date: 2026-03-29

## Overview

Phase 2 implements first-class endpoints for core POS workflows: tickets, orders, and payments. The goal is to expose reliable command and query paths that can eventually replace legacy endpoints.

## Objectives

- Expose reliable command and query paths for core POS workflows
- Introduce idempotency keys for safe write operations
- Modernize persistence path to net10-compatible data access
- Validate data invariants against legacy outputs

## Architecture

### Domain Services

**ITicketDomainService**
- `CreateTicketAsync(request)` - Create new ticket for department/terminal
- `GetTicketAsync(ticketId)` - Retrieve ticket with orders and payments
- `ListOpenTicketsAsync(departmentId, pagination)` - List open tickets
- `AddOrderAsync(ticketId, request)` - Add order line item
- `UpdateTicketStateAsync(ticketId, request)` - Update state machine (e.g., Kitchen Status → Ready)
- `CloseTicketAsync(ticketId)` - Finalize ticket after payment

**IOrderDomainService**
- `GetOrderAsync(orderId)` - Get order details
- `UpdateOrderStateAsync(orderId, request)` - Mark order state
- `VoidOrderAsync(orderId)` - Revert order

**IPaymentDomainService**
- `ProcessPaymentAsync(ticketId, request)` - Process payment with idempotency key
- `GetPaymentAsync(paymentId)` - Get payment details
- `RefundPaymentAsync(paymentId, request)` - Refund (if allowed)
- `ListTicketPaymentsAsync(ticketId)` - List payments for ticket

### Repository Pattern

Abstraction layer for data access:

```
ITicketRepository
├─ GetByIdAsync(ticketId)
├─ ListOpenAsync(departmentId, pagination)
├─ CreateAsync(ticket)
└─ UpdateAsync(ticket)

IOrderRepository
├─ GetByIdAsync(orderId)
├─ ListByTicketAsync(ticketId)
├─ CreateAsync(order)
└─ UpdateAsync(order)

IPaymentRepository
├─ GetByIdAsync(paymentId)
├─ ListByTicketAsync(ticketId)
└─ CreateAsync(payment)

IIdempotencyService
├─ GetResultAsync(key)
└─ StoreResultAsync(key, result, ttl)
```

**Phase 2 Implementation**: In-memory placeholder repositories (InMemoryTicketRepository, etc.)  
**Phase 3 Target**: EF Core repositories with SQL Server backend

### Idempotency Keys

Prevent duplicate payments and safe retries:

```csharp
var request = new ProcessPaymentRequest(
    PaymentTypeId: 1,
    Amount: 27.50m,
    TenderedAmount: 30.00m,
    IdempotencyKey: "payment-ticket-123-1711789200"  // timestamp or GUID
);

// First call: processes payment
POST /api/v2/payments → 201 Created with payment details

// Duplicate call (same key): returns cached result
POST /api/v2/payments → 200 OK with same payment (no GL duplication)
```

TTL: 24 hours (configurable)  
Storage: In-memory cache (Phase 2) → Redis (Phase 3)

## API Endpoints

### Tickets

```
POST   /api/v2/tickets                      Create ticket
GET    /api/v2/tickets/{ticketId}           Get ticket with orders/payments
GET    /api/v2/tickets?departmentId=1&...  List open tickets (paginated)
POST   /api/v2/tickets/{ticketId}/orders    Add order line item
PUT    /api/v2/tickets/{ticketId}/state     Update state (Kitchen Status, etc)
POST   /api/v2/tickets/{ticketId}/close     Close ticket
```

**Example: Create Ticket**
```http
POST /api/v2/tickets
Content-Type: application/json

{
  "departmentId": 1,
  "terminalId": 5,
  "ticketTypeId": 12
}

Response (201 Created):
{
  "id": 456,
  "ticketNumber": "T-2026-03-29-001",
  "issuedAt": "2026-03-29T15:30:00Z",
  "totalAmount": 0.00,
  "remainingAmount": 0.00,
  "isClosed": false,
  "orders": [],
  "payments": []
}
```

### Orders

```
GET    /api/v2/orders/{orderId}             Get order details
PUT    /api/v2/orders/{orderId}/state       Update order state
POST   /api/v2/orders/{orderId}/void        Void order (before payment)
```

### Payments

```
POST   /api/v2/payments                     Process payment (with idempotency key)
GET    /api/v2/payments/{paymentId}         Get payment details
POST   /api/v2/payments/{paymentId}/refund  Refund payment
GET    /api/v2/payments/ticket/{ticketId}   List payments for ticket
```

**Example: Process Payment (Idempotent)**
```http
POST /api/v2/payments
Content-Type: application/json
X-Idempotency-Key: payment-ticket-456-1711789200

{
  "ticketId": 456,
  "paymentTypeId": 1,
  "amount": 27.50,
  "tenderedAmount": 30.00,
  "idempotencyKey": "payment-ticket-456-1711789200"
}

Response (201 Created):
{
  "id": 789,
  "amount": 27.50,
  "processedAt": "2026-03-29T15:31:00Z",
  "paymentType": "Cash"
}

// Retry with same key → 200 OK (no duplicate GL entry)
```

## Data Models (Phase 2 Stubs)

**TicketAggregate**
- Id, TicketNumber, CreatedAt, TotalAmount, RemainingAmount, IsClosed
- Orders[], Payments[]

**OrderAggregate**
- Id, MenuItemId, MenuItemName, Quantity, UnitPrice, LineTotal, Status

**PaymentAggregate**
- Id, Amount, ProcessedAt, PaymentType

## Testing Strategy

### Phase 2 Integration Tests

```csharp
[Fact]
public async Task CreateTicket_ReturnsTicketDto()
{
    // Arrange
    var request = new CreateTicketRequest(1, 5);
    
    // Act
    var result = await _ticketService.CreateTicketAsync(request);
    
    // Assert
    Assert.NotNull(result);
    Assert.True(result.Id > 0);
}

[Fact]
public async Task ProcessPayment_IdempotentKey_ReturnsCachedResult()
{
    // Arrange
    var request = new ProcessPaymentRequest(1, 27.50m, 30.00m, "key-123");
    
    // Act - First call
    var result1 = await _paymentService.ProcessPaymentAsync(456, request);
    
    // Act - Duplicate call
    var result2 = await _paymentService.ProcessPaymentAsync(456, request);
    
    // Assert
    Assert.Equal(result1.Id, result2.Id);  // Same payment ID
}

[Fact]
public async Task ListOpenTickets_Pagination_ReturnsPaged()
{
    // Arrange
    var departmentId = 1;
    var pageNumber = 1;
    var pageSize = 10;
    
    // Act
    var result = await _ticketService.ListOpenTicketsAsync(
        departmentId, pageNumber, pageSize);
    
    // Assert
    Assert.NotNull(result);
    Assert.True(result.Items.Count <= pageSize);
}
```

## Exit Criteria for Phase 2

- ✓ Domain service interfaces defined
- ✓ Ticket, order, payment endpoints implemented
- ✓ Idempotency keys working (in-memory)
- ✓ Repository pattern abstraction complete
- ✓ Placeholder in-memory repositories active
- ✓ Integration tests (20+ test cases)
- ✓ All code compiles (net10.0)
- ✓ Swagger documentation updated

## Phase 2 → Phase 3 Transition

In Phase 3, replace placeholder repositories with EF Core implementations:

1. Create `EFTicketRepository : ITicketRepository` using DbContext
2. Add SambaPOS domain model mappings (fluent configuration)
3. Integrate with legacy Samba.Domain services for parity
4. Add database unit of work pattern
5. Implement Redis for idempotency cache (TTL-based)
6. Add financial audit logging (GL integration)
7. Performance optimization (indexes, eager loading)

## Known Limitations (Phase 2)

- In-memory repositories: data lost on app restart
- No database persistence yet
- No GL accounting integration
- Idempotency cache: 24-hour hardcoded TTL
- No multi-tenant isolation
- No audit trail
- Hardcoded authentication (admin/admin)

All above addressed in Phase 3.

## Configuration

### appsettings.json

```json
{
  "Phase2": {
    "RepositoryType": "InMemory",
    "IdempotencyTtlMinutes": 1440,
    "PaginationPageSize": 50
  }
}
```

## References

- [Implementation Plan](./03-implementation-plan.md)
- [Phase 0 Baseline Scenarios](./06-phase0-baseline-scenarios.md)
- [Reference Implementation](./04-reference-implementation.md)
