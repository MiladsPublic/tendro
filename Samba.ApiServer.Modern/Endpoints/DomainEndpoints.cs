using Samba.ApiServer.Modern.Contracts;
using Samba.ApiServer.Modern.Services;
using Microsoft.AspNetCore.Mvc;

namespace Samba.ApiServer.Modern.Endpoints;

/// <summary>
/// Phase 2: Domain Endpoints
/// Ticket, Order, and Payment operations
/// </summary>
/// 
public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/tickets")
            .WithTags("Tickets")
            .WithName("Tickets");

        group.MapPost("/", CreateTicket)
            .WithName("CreateTicket")
            .WithSummary("Create new ticket")
            .Accepts<CreateTicketRequest>("application/json")
            .Produces<TicketDto>(StatusCodes.Status201Created);

        group.MapGet("/{ticketId}", GetTicket)
            .WithName("GetTicket")
            .WithSummary("Get ticket by ID")
            .Produces<TicketDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapGet("/", ListOpenTickets)
            .WithName("ListOpenTickets")
            .WithSummary("List open tickets for department")
            .Produces<PagedResponse<TicketDto>>(StatusCodes.Status200OK);

        group.MapPost("/{ticketId}/orders", AddOrder)
            .WithName("AddOrder")
            .WithSummary("Add order line item to ticket")
            .Accepts<AddOrderRequest>("application/json")
            .Produces<TicketDto>(StatusCodes.Status200OK);

        group.MapPut("/{ticketId}/state", UpdateTicketState)
            .WithName("UpdateTicketState")
            .WithSummary("Update ticket state (e.g., Kitchen Status)")
            .Accepts<UpdateTicketStateRequest>("application/json")
            .Produces<TicketDto>(StatusCodes.Status200OK);

        group.MapPost("/{ticketId}/close", CloseTicket)
            .WithName("CloseTicket")
            .WithSummary("Close ticket after payment")
            .Produces<TicketDto>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> CreateTicket(
        [FromBody] CreateTicketRequest request,
        [FromServices] ITicketDomainService ticketService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var ticket = await ticketService.CreateTicketAsync(request, ct);
            logger.LogInformation("Ticket created: {TicketNumber}", ticket.TicketNumber);
            return Results.Created($"/api/v2/tickets/{ticket.Id}", ticket);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating ticket");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetTicket(
        [FromRoute] int ticketId,
        [FromServices] ITicketDomainService ticketService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var ticket = await ticketService.GetTicketAsync(ticketId, ct);
            if (ticket == null)
            {
                logger.LogWarning("Ticket not found: {TicketId}", ticketId);
                return Results.NotFound(new ErrorResponse(
                    Error: "NotFound",
                    Message: $"Ticket {ticketId} not found"
                ));
            }
            return Results.Ok(ticket);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving ticket {TicketId}", ticketId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> ListOpenTickets(
        [FromServices] ITicketDomainService ticketService,
        [FromServices] ILogger<Program> logger,
        [FromQuery] int departmentId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var tickets = await ticketService.ListOpenTicketsAsync(departmentId, pageNumber, pageSize, ct);
            logger.LogInformation("Listed {Count} open tickets for department {DepartmentId}", 
                tickets.Items.Count, departmentId);
            return Results.Ok(tickets);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing tickets");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AddOrder(
        [FromRoute] int ticketId,
        [FromBody] AddOrderRequest request,
        [FromServices] ITicketDomainService ticketService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var ticket = await ticketService.AddOrderAsync(ticketId, request, ct);
            logger.LogInformation("Order added to ticket {TicketId}", ticketId);
            return Results.Ok(ticket);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding order to ticket {TicketId}", ticketId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateTicketState(
        [FromRoute] int ticketId,
        [FromBody] UpdateTicketStateRequest request,
        [FromServices] ITicketDomainService ticketService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var ticket = await ticketService.UpdateTicketStateAsync(ticketId, request, ct);
            logger.LogInformation("Ticket {TicketId} state updated to {State}={Value}", 
                ticketId, request.StateName, request.StateValue);
            return Results.Ok(ticket);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating ticket state");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CloseTicket(
        [FromRoute] int ticketId,
        [FromServices] ITicketDomainService ticketService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var ticket = await ticketService.CloseTicketAsync(ticketId, ct);
            logger.LogInformation("Ticket {TicketId} closed", ticketId);
            return Results.Ok(ticket);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing ticket {TicketId}", ticketId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/payments")
            .WithTags("Payments")
            .WithName("Payments");

        group.MapPost("/", ProcessPayment)
            .WithName("ProcessPayment")
            .WithSummary("Process payment with idempotency guarantee")
            .Accepts<ProcessPaymentRequest>("application/json")
            .Produces<PaymentDto>(StatusCodes.Status201Created);

        group.MapGet("/{paymentId}", GetPayment)
            .WithName("GetPayment")
            .WithSummary("Get payment details")
            .Produces<PaymentDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{paymentId}/refund", RefundPayment)
            .WithName("RefundPayment")
            .WithSummary("Refund payment (if allowed)")
            .Accepts<RefundPaymentRequest>("application/json")
            .Produces<PaymentDto>(StatusCodes.Status200OK);

        group.MapGet("/ticket/{ticketId}", ListTicketPayments)
            .WithName("ListTicketPayments")
            .WithSummary("List all payments for ticket")
            .Produces<IReadOnlyList<PaymentDto>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        [FromQuery] int ticketId,
        [FromServices] IPaymentDomainService paymentService,
        [FromServices] ITicketDomainService ticketService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(request.IdempotencyKey))
            {
                logger.LogWarning("ProcessPayment called without idempotency key");
                return Results.BadRequest(new ErrorResponse(
                    Error: "ValidationError",
                    Message: "IdempotencyKey is required for payment processing"
                ));
            }

            if (ticketId <= 0)
            {
                logger.LogWarning("ProcessPayment called without a valid ticket id");
                return Results.BadRequest(new ErrorResponse(
                    Error: "ValidationError",
                    Message: "ticketId query parameter is required"
                ));
            }

            var ticket = await ticketService.GetTicketAsync(ticketId, ct);
            if (ticket == null)
            {
                logger.LogWarning("ProcessPayment called for missing ticket {TicketId}", ticketId);
                return Results.NotFound(new ErrorResponse(
                    Error: "NotFound",
                    Message: $"Ticket {ticketId} not found"
                ));
            }

            var payment = await paymentService.ProcessPaymentAsync(ticketId, request, ct);
            logger.LogInformation("Processed payment {PaymentId} for ticket {TicketId}", payment.Id, ticketId);
            return Results.Created($"/api/v2/payments/{payment.Id}", payment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing payment");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetPayment(
        [FromRoute] int paymentId,
        [FromServices] IPaymentDomainService paymentService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var payment = await paymentService.GetPaymentAsync(paymentId, ct);
            if (payment == null)
            {
                return Results.NotFound(new ErrorResponse(
                    Error: "NotFound",
                    Message: $"Payment {paymentId} not found"
                ));
            }
            return Results.Ok(payment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving payment");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> RefundPayment(
        [FromRoute] int paymentId,
        [FromBody] RefundPaymentRequest request,
        [FromServices] IPaymentDomainService paymentService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var payment = await paymentService.RefundPaymentAsync(paymentId, request, ct);
            logger.LogInformation("Payment {PaymentId} refunded", paymentId);
            return Results.Ok(payment);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refunding payment");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> ListTicketPayments(
        [FromRoute] int ticketId,
        [FromServices] IPaymentDomainService paymentService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var payments = await paymentService.ListTicketPaymentsAsync(ticketId, ct);
            logger.LogDebug("Listed {Count} payments for ticket {TicketId}", payments.Count, ticketId);
            return Results.Ok(payments);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing ticket payments");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/orders")
            .WithTags("Orders")
            .WithName("Orders");

        group.MapGet("/{orderId}", GetOrder)
            .WithName("GetOrder")
            .WithSummary("Get order details")
            .Produces<OrderDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPut("/{orderId}/state", UpdateOrderState)
            .WithName("UpdateOrderState")
            .WithSummary("Update order state")
            .Accepts<UpdateOrderStateRequest>("application/json")
            .Produces<OrderDto>(StatusCodes.Status200OK);

        group.MapPost("/{orderId}/void", VoidOrder)
            .WithName("VoidOrder")
            .WithSummary("Void order (before payment applied)")
            .Produces<OrderDto>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetOrder(
        [FromRoute] int orderId,
        [FromServices] IOrderDomainService orderService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var order = await orderService.GetOrderAsync(orderId, ct);
            if (order == null)
            {
                return Results.NotFound(new ErrorResponse(
                    Error: "NotFound",
                    Message: $"Order {orderId} not found"
                ));
            }
            return Results.Ok(order);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving order");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateOrderState(
        [FromRoute] int orderId,
        [FromBody] UpdateOrderStateRequest request,
        [FromServices] IOrderDomainService orderService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var order = await orderService.UpdateOrderStateAsync(orderId, request, ct);
            logger.LogInformation("Order {OrderId} state updated to {State}={Value}", 
                orderId, request.StateName, request.StateValue);
            return Results.Ok(order);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating order state");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> VoidOrder(
        [FromRoute] int orderId,
        [FromServices] IOrderDomainService orderService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var order = await orderService.VoidOrderAsync(orderId, ct);
            logger.LogInformation("Order {OrderId} voided", orderId);
            return Results.Ok(order);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error voiding order");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

public static class PrintEndpoints
{
    public static void MapPrintEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/print-jobs")
            .WithTags("Print")
            .WithName("PrintJobs");

        group.MapPost("/reprint", QueueReprint)
            .WithName("QueueTicketReprint")
            .WithSummary("Queue ticket reprint request")
            .Accepts<ReprintTicketRequest>("application/json")
            .Produces<PrintJobDto>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> QueueReprint(
        [FromBody] ReprintTicketRequest request,
        [FromServices] ITicketDomainService ticketService,
        [FromServices] IPrintService printService,
        [FromServices] ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var ticket = await ticketService.GetTicketAsync(request.TicketId, ct);
            if (ticket == null)
            {
                return Results.NotFound(new ErrorResponse(
                    Error: "NotFound",
                    Message: $"Ticket {request.TicketId} not found"
                ));
            }

            var job = await printService.QueueTicketReprintAsync(request, ct);
            logger.LogInformation("Queued reprint job {JobId} for ticket {TicketId}", job.JobId, job.TicketId);
            return Results.Accepted($"/api/v2/print-jobs/{job.JobId}", job);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queueing ticket reprint");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
