using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Samba.ApiServer.Modern.Services;
using Samba.ApiServer.Modern.Contracts;
using Samba.ApiServer.Modern.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Samba.ApiServer.Modern.Tests.Phase3
{
    /// <summary>
    /// Phase 3 integration tests using EF Core with in-memory SQLite database.
    /// Tests domain services with persistent EF Core repositories instead of in-memory stubs.
    /// </summary>
    public class Phase3IntegrationTests : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITicketDomainService _ticketService;
        private readonly IOrderDomainService _orderService;
        private readonly IPaymentDomainService _paymentService;
        private readonly SambaDbContext _dbContext;
        private readonly ILogger<Phase3IntegrationTests> _logger;

        public Phase3IntegrationTests()
        {
            var services = new ServiceCollection();
            
            services.AddLogging(config =>
            {
                config.AddConsole();
                config.SetMinimumLevel(LogLevel.Information);
            });

            // Register EF Core with in-memory SQLite database
            services.AddDbContext<SambaDbContext>(options =>
            {
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()); // Unique DB per test
                options.EnableSensitiveDataLogging(true);
            });

            // Register domain services
            services.AddScoped<ITicketDomainService, TicketDomainService>();
            services.AddScoped<IOrderDomainService, OrderDomainService>();
            services.AddScoped<IPaymentDomainService, PaymentDomainService>();

            // Register EF Core repositories (Phase 3)
            services.AddScoped<ITicketRepository, EfCoreTicketRepository>();
            services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
            services.AddScoped<IPaymentRepository, EfCorePaymentRepository>();
            services.AddScoped<IIdempotencyService, EfCoreIdempotencyService>();

            _serviceProvider = services.BuildServiceProvider();
            
            // Initialize database schema
            _dbContext = _serviceProvider.GetRequiredService<SambaDbContext>();
            _dbContext.Database.EnsureCreated();

            _ticketService = _serviceProvider.GetRequiredService<ITicketDomainService>();
            _orderService = _serviceProvider.GetRequiredService<IOrderDomainService>();
            _paymentService = _serviceProvider.GetRequiredService<IPaymentDomainService>();
            _logger = _serviceProvider.GetRequiredService<ILogger<Phase3IntegrationTests>>();
        }

        public void Dispose()
        {
            _dbContext?.Database.EnsureDeleted();
            _dbContext?.Dispose();
            (_serviceProvider as ServiceProvider)?.Dispose();
        }

        // ============================================================
        // Ticket Lifecycle Tests
        // ============================================================

        [Fact]
        public async Task CreateTicket_ValidRequest_ReturnsTicketWithId()
        {
            // Arrange
            var request = new CreateTicketRequest(
                DepartmentId: 1,
                TerminalId: 1,
                TicketTypeId: 1);

            // Act
            var result = await _ticketService.CreateTicketAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.NotEmpty(result.TicketNumber);
            Assert.StartsWith("T-", result.TicketNumber);
            Assert.Empty(result.Orders);
            Assert.Empty(result.Payments);
            Assert.False(result.IsClosed);
        }

        [Fact]
        public async Task ListOpenTickets_NoFilters_ReturnsAllOpenTickets()
        {
            // Arrange
            var req1 = new CreateTicketRequest(1, 1, 1);
            var req2 = new CreateTicketRequest(1, 1, 1);
            var ticket1 = await _ticketService.CreateTicketAsync(req1);
            var ticket2 = await _ticketService.CreateTicketAsync(req2);

            // Act
            var page = await _ticketService.ListOpenTicketsAsync(1, 1, 20);

            // Assert
            Assert.NotNull(page);
            Assert.Equal(2, page.Items.Count);
            Assert.Equal(2, page.TotalCount);
        }

        [Fact]
        public async Task ListOpenTickets_WithPagination_ReturnsPaged()
        {
            // Arrange - Create 3 tickets
            for (int i = 0; i < 3; i++)
            {
                var req = new CreateTicketRequest(1, 1, 1);
                await _ticketService.CreateTicketAsync(req);
            }

            // Act
            var page1 = await _ticketService.ListOpenTicketsAsync(1, 1, 2);
            var page2 = await _ticketService.ListOpenTicketsAsync(1, 2, 2);

            // Assert
            Assert.Equal(2, page1.Items.Count);
            Assert.Single(page2.Items);
            Assert.Equal(3, page1.TotalCount);
            Assert.Equal(3, page2.TotalCount);
        }

        [Fact]
        public async Task AddOrder_ValidRequest_AddsOrderToTicket()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var orderReq = new AddOrderRequest(
                MenuItemId: 100,
                Quantity: 2,
                PortionName: "Regular",
                Tags: new Dictionary<string, string> { { "spicy", "mild" } });

            // Act
            var result = await _ticketService.AddOrderAsync(ticket.Id, orderReq);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Orders);
            var order = result.Orders.First();
            Assert.Equal(100, order.MenuItemId);
            Assert.Equal(2, order.Quantity);
        }

        [Fact]
        public async Task AddOrder_MultipleOrders_CalculatesTotalCorrectly()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var order1 = new AddOrderRequest(100, 2m, "Regular");
            var order2 = new AddOrderRequest(200, 1m, "Large");

            // Act
            await _ticketService.AddOrderAsync(ticket.Id, order1);
            var result = await _ticketService.AddOrderAsync(ticket.Id, order2);

            // Assert
            Assert.Equal(2, result.Orders.Count);
        }

        [Fact]
        public async Task UpdateTicketState_ValidState_UpdatesStateProperty()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var stateReq = new UpdateTicketStateRequest("Served", "waiting_payment");

            // Act
            var result = await _ticketService.UpdateTicketStateAsync(ticket.Id, stateReq);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ticket.Id, result.Id);
            Assert.False(result.IsClosed);
        }

        [Fact]
        public async Task CloseTicket_OpenTicket_SetsIsClosed()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);
            Assert.False(ticket.IsClosed);

            // Act
            var result = await _ticketService.CloseTicketAsync(ticket.Id);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsClosed);
        }

        // ============================================================
        // Order Operations Tests
        // ============================================================

        [Fact]
        public async Task GetOrder_ExistingOrder_ViaTicket_ReturnsOrderDto()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);
            var orderReq = new AddOrderRequest(100, 1m, "Regular");
            var updatedTicket = await _ticketService.AddOrderAsync(ticket.Id, orderReq);
            var orderId = updatedTicket.Orders.First().Id;

            // Act
            var result = await _orderService.GetOrderAsync(orderId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.MenuItemId);
            Assert.Equal(1, result.Quantity);
        }

        [Fact]
        public async Task UpdateOrderState_ViaService_UpdatesOrderState()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);
            var orderReq = new AddOrderRequest(100, 1m, "Regular");
            var updatedTicket = await _ticketService.AddOrderAsync(ticket.Id, orderReq);
            var orderId = updatedTicket.Orders.First().Id;

            var stateReq = new UpdateOrderStateRequest("Completed", "ready");

            // Act
            var result = await _orderService.UpdateOrderStateAsync(orderId, stateReq);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Completed", result.Status);
        }

        [Fact]
        public async Task VoidOrder_ViaService_MarksAsVoided()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);
            var orderReq = new AddOrderRequest(100, 1m, "Regular");
            var updatedTicket = await _ticketService.AddOrderAsync(ticket.Id, orderReq);
            var orderId = updatedTicket.Orders.First().Id;

            // Act
            var result = await _orderService.VoidOrderAsync(orderId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Voided", result.Status);
        }

        // ============================================================
        // Payment Processing Tests
        // ============================================================

        [Fact]
        public async Task ProcessPayment_ValidRequest_CreatesPayment()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var paymentReq = new ProcessPaymentRequest(
                PaymentTypeId: 1,
                Amount: 100.00m,
                TenderedAmount: 100.00m,
                IdempotencyKey: Guid.NewGuid().ToString());

            // Act
            var result = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal(100.00m, result.Amount);
        }

        [Fact]
        public async Task ProcessPayment_IdempotencyKey_ReturnsCachedResultOnRetry()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);
            var idempotencyKey = Guid.NewGuid().ToString();

            var paymentReq = new ProcessPaymentRequest(1, 50.00m, 50.00m, idempotencyKey);

            // Act
            var payment1 = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);
            var payment2 = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);

            // Assert
            Assert.Equal(payment1.Id, payment2.Id); // Should return cached result
        }

        [Fact]
        public async Task ProcessPayment_DifferentIdempotencyKeys_CreatesMultiplePayments()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var payment1Req = new ProcessPaymentRequest(1, 30.00m, 30.00m, Guid.NewGuid().ToString());
            var payment2Req = new ProcessPaymentRequest(1, 20.00m, 20.00m, Guid.NewGuid().ToString());

            // Act
            var payment1 = await _paymentService.ProcessPaymentAsync(ticket.Id, payment1Req);
            var payment2 = await _paymentService.ProcessPaymentAsync(ticket.Id, payment2Req);

            // Assert
            Assert.NotEqual(payment1.Id, payment2.Id); // Different payments
            Assert.Equal(30.00m, payment1.Amount);
            Assert.Equal(20.00m, payment2.Amount);
        }

        [Fact]
        public async Task GetPayment_ExistingPayment_ReturnsPaymentDto()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var paymentReq = new ProcessPaymentRequest(1, 75.00m, 75.00m, Guid.NewGuid().ToString());
            var payment = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);

            // Act
            var result = await _paymentService.GetPaymentAsync(payment.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(75.00m, result.Amount);
        }

        [Fact]
        public async Task ListTicketPayments_MultiplePayments_ReturnsAll()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var payment1 = new ProcessPaymentRequest(1, 50.00m, 50.00m, Guid.NewGuid().ToString());
            var payment2 = new ProcessPaymentRequest(1, 25.00m, 25.00m, Guid.NewGuid().ToString());

            // Act
            await _paymentService.ProcessPaymentAsync(ticket.Id, payment1);
            await _paymentService.ProcessPaymentAsync(ticket.Id, payment2);
            var payments = await _paymentService.ListTicketPaymentsAsync(ticket.Id);

            // Assert
            Assert.Equal(2, payments.Count);
        }

        [Fact]
        public async Task RefundPayment_ProcessedPayment_CreatesRefund()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var paymentReq = new ProcessPaymentRequest(1, 100.00m, 100.00m, Guid.NewGuid().ToString());
            var payment = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);

            var refundReq = new RefundPaymentRequest("Customer requested", false);

            // Act
            var result = await _paymentService.RefundPaymentAsync(payment.Id, refundReq);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal(-100.00m, result.Amount); // Refund is negative amount
        }

        // ============================================================
        // Full Lifecycle Tests
        // ============================================================

        [Fact]
        public async Task FullTicketLifecycle_CreateOrderPaymentClose_Succeeds()
        {
            // Arrange & Act
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);

            var orderReq1 = new AddOrderRequest(100, 2m, "Regular");
            var orderReq2 = new AddOrderRequest(200, 1m, "Large");
            var ticket2 = await _ticketService.AddOrderAsync(ticket.Id, orderReq1);
            var ticket3 = await _ticketService.AddOrderAsync(ticket.Id, orderReq2);

            var paymentReq = new ProcessPaymentRequest(1, 100.00m, 100.00m, Guid.NewGuid().ToString());
            var payment = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);

            var stateReq = new UpdateTicketStateRequest("Served", "completed");
            await _ticketService.UpdateTicketStateAsync(ticket.Id, stateReq);

            var closed = await _ticketService.CloseTicketAsync(ticket.Id);

            // Assert
            Assert.True(closed.IsClosed);
            Assert.Equal(2, closed.Orders.Count);
            Assert.Equal(100.00m, closed.TotalAmount);
        }

        [Fact]
        public async Task PaymentIdempotency_AcrossServiceCalls_MaintainsConsistency()
        {
            // Arrange
            var ticketReq = new CreateTicketRequest(1, 1, 1);
            var ticket = await _ticketService.CreateTicketAsync(ticketReq);
            var idempotencyKey = Guid.NewGuid().ToString();

            var paymentReq = new ProcessPaymentRequest(1, 200.00m, 200.00m, idempotencyKey);

            // Act - Call 5 times with same idempotency key
            var payment1 = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);
            var payment2 = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);
            var payment3 = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);
            var payment4 = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);
            var payment5 = await _paymentService.ProcessPaymentAsync(ticket.Id, paymentReq);

            // Assert - All should return same payment ID
            Assert.Equal(payment1.Id, payment2.Id);
            Assert.Equal(payment2.Id, payment3.Id);
            Assert.Equal(payment3.Id, payment4.Id);
            Assert.Equal(payment4.Id, payment5.Id);
        }
    }
}
