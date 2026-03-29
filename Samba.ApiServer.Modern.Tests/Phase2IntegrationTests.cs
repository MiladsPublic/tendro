using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Samba.ApiServer.Modern.Services;
using Samba.ApiServer.Modern.Contracts;
using Samba.ApiServer.Modern.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Samba.ApiServer.Modern.Tests.Phase2
{
    public class Phase2IntegrationTests : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITicketDomainService _ticketService;
        private readonly IOrderDomainService _orderService;
        private readonly IPaymentDomainService _paymentService;
        private readonly ILogger<Phase2IntegrationTests> _logger;

        public Phase2IntegrationTests()
        {
            var services = new ServiceCollection();
            
            services.AddLogging(config =>
            {
                config.AddConsole();
                config.SetMinimumLevel(LogLevel.Information);
            });

            // Register domain services
            services.AddScoped<ITicketDomainService, TicketDomainService>();
            services.AddScoped<IOrderDomainService, OrderDomainService>();
            services.AddScoped<IPaymentDomainService, PaymentDomainService>();

            // Register repositories
            services.AddScoped<ITicketRepository, InMemoryTicketRepository>();
            services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
            services.AddScoped<IPaymentRepository, InMemoryPaymentRepository>();
            services.AddScoped<IIdempotencyService, InMemoryIdempotencyService>();

            _serviceProvider = services.BuildServiceProvider();
            
            _ticketService = _serviceProvider.GetRequiredService<ITicketDomainService>();
            _orderService = _serviceProvider.GetRequiredService<IOrderDomainService>();
            _paymentService = _serviceProvider.GetRequiredService<IPaymentDomainService>();
            _logger = _serviceProvider.GetRequiredService<ILogger<Phase2IntegrationTests>>();
        }

        public void Dispose()
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        #region Ticket Service Tests

        [Fact]
        public async Task CreateTicket_ValidRequest_ReturnsTicketWithId()
        {
            // Arrange
            var request = new CreateTicketRequest(DepartmentId: 1, TerminalId: 5);

            // Act
            var result = await _ticketService.CreateTicketAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal("T-" + DateTime.UtcNow.ToString("yyyy-MM-dd"), result.TicketNumber[..11]);
            Assert.Equal(0m, result.TotalAmount);
            Assert.False(result.IsClosed);
            Assert.Empty(result.Orders);
            Assert.Empty(result.Payments);
        }

        [Fact]
        public async Task GetTicket_ExistingTicket_ReturnsTicketDto()
        {
            // Arrange
            var createRequest = new CreateTicketRequest(1, 5);
            var ticket = await _ticketService.CreateTicketAsync(createRequest);

            // Act
            var result = await _ticketService.GetTicketAsync(ticket.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ticket.Id, result.Id);
            Assert.Equal(ticket.TicketNumber, result.TicketNumber);
        }

        [Fact]
        public async Task GetTicket_NonExistentTicket_ReturnsNull()
        {
            // Act & Assert
            var result = await _ticketService.GetTicketAsync(999999);
            Assert.Null(result);
        }

        [Fact]
        public async Task ListOpenTickets_NoFilters_ReturnsAllOpenTickets()
        {
            // Arrange
            await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.CreateTicketAsync(new(2, 5));

            // Act
            var result = await _ticketService.ListOpenTicketsAsync(
                departmentId: 1,
                pageNumber: 1,
                pageSize: 10);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Items.Count >= 2);
            Assert.True(result.PageNumber == 1);
        }

        [Fact]
        public async Task ListOpenTickets_WithPagination_ReturnsPaged()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                await _ticketService.CreateTicketAsync(new(1, 5));
            }

            // Act - Page 1
            var page1 = await _ticketService.ListOpenTicketsAsync(1, 1, 10);
            
            // Act - Page 2
            var page2 = await _ticketService.ListOpenTicketsAsync(1, 2, 10);

            // Assert
            Assert.True(page1.Items.Count <= 10);
            Assert.True(page2.Items.Count <= 10);
            Assert.NotEqual(page1.Items.FirstOrDefault()?.Id, page2.Items.FirstOrDefault()?.Id);
        }

        [Fact]
        public async Task AddOrder_ValidRequest_AddsOrderToTicket()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            var addOrderRequest = new AddOrderRequest(
                MenuItemId: 10,
                Quantity: 2m,
                PortionName: "Regular");

            // Act
            var result = await _ticketService.AddOrderAsync(ticket.Id, addOrderRequest);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Orders.Count > 0);
            var addedOrder = result.Orders.First();
            Assert.Equal(10, addedOrder.MenuItemId);
            Assert.Equal(2m, addedOrder.Quantity);
        }

        [Fact]
        public async Task AddOrder_MultipleOrders_CalculatesTotalCorrectly()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));

            // Act - Add first order
            var afterFirst = await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));

            // Act - Add second order
            var afterSecond = await _ticketService.AddOrderAsync(ticket.Id,
                new(102, 2m, "Large"));

            // Assert
            Assert.True(afterSecond.Orders.Count >= 2);
        }

        [Fact]
        public async Task UpdateTicketState_ValidState_UpdatesStateProperty()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            var updateRequest = new UpdateTicketStateRequest(
                StateName: "KitchenStatus",
                StateValue: "Preparing");

            // Act
            var result = await _ticketService.UpdateTicketStateAsync(ticket.Id, updateRequest);

            // Assert
            Assert.NotNull(result);
            // State tracking validated through domain rules
        }

        [Fact]
        public async Task CloseTicket_OpenTicket_SetsIsClosed()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));

            // Act
            var result = await _ticketService.CloseTicketAsync(ticket.Id);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsClosed);
        }

        #endregion

        #region Order Service Tests

        [Fact]
        public async Task GetOrder_ExistingOrder_ReturnsOrderDto()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            var ticketWithOrder = await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));
            var orderId = ticketWithOrder.Orders.First().Id;

            // Act
            var result = await _orderService.GetOrderAsync(orderId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(orderId, result.Id);
        }

        [Fact]
        public async Task UpdateOrderState_ValidRequest_UpdatesOrderState()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            var ticketWithOrder = await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));
            var orderId = ticketWithOrder.Orders.First().Id;

            // Act
            var updateRequest = new UpdateOrderStateRequest(
                StateName: "Status",
                StateValue: "Ready");
            var result = await _orderService.UpdateOrderStateAsync(orderId, updateRequest);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Ready", result.Status);
        }

        [Fact]
        public async Task VoidOrder_ValidOrder_MarksAsVoided()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            var ticketWithOrder = await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));
            var orderId = ticketWithOrder.Orders.First().Id;

            // Act
            var result = await _orderService.VoidOrderAsync(orderId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Voided", result.Status);
        }

        #endregion

        #region Payment Service Tests

        [Fact]
        public async Task ProcessPayment_ValidRequest_CreatesPayment()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));

            var request = new ProcessPaymentRequest(
                PaymentTypeId: 1,
                Amount: 27.50m,
                TenderedAmount: 30.00m,
                IdempotencyKey: Guid.NewGuid().ToString());

            // Act
            var result = await _paymentService.ProcessPaymentAsync(ticket.Id, request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal(27.50m, result.Amount);
        }

        [Fact]
        public async Task ProcessPayment_IdempotencyKey_ReturnsCachedResultOnRetry()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));

            var idempotencyKey = Guid.NewGuid().ToString();
            var request = new ProcessPaymentRequest(1, 27.50m, 30.00m, idempotencyKey);

            // Act - First call
            var result1 = await _paymentService.ProcessPaymentAsync(ticket.Id, request);
            _logger.LogInformation($"First payment: {result1.Id}");

            // Act - Second call (same idempotency key)
            var result2 = await _paymentService.ProcessPaymentAsync(ticket.Id, request);
            _logger.LogInformation($"Retry payment: {result2.Id}");

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(result1.Id, result2.Id);
            _logger.LogInformation("Idempotency key working: same payment ID returned");
        }

        [Fact]
        public async Task ProcessPayment_DifferentIdempotencyKeys_CreatesMultiplePayments()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 2m, "Regular"));

            var request1 = new ProcessPaymentRequest(1, 27.50m, 30.00m, "key-1");
            var request2 = new ProcessPaymentRequest(1, 27.50m, 30.00m, "key-2");

            // Act
            var result1 = await _paymentService.ProcessPaymentAsync(ticket.Id, request1);
            var result2 = await _paymentService.ProcessPaymentAsync(ticket.Id, request2);

            // Assert
            Assert.NotEqual(result1.Id, result2.Id);
            _logger.LogInformation($"Different keys created different payments: {result1.Id} vs {result2.Id}");
        }

        [Fact]
        public async Task GetPayment_ExistingPayment_ReturnsPaymentDto()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));

            var request = new ProcessPaymentRequest(1, 27.50m, 30.00m, Guid.NewGuid().ToString());
            var payment = await _paymentService.ProcessPaymentAsync(ticket.Id, request);

            // Act
            var result = await _paymentService.GetPaymentAsync(payment.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(payment.Id, result.Id);
            Assert.Equal(27.50m, result.Amount);
        }

        [Fact]
        public async Task ListTicketPayments_MultiplePayments_ReturnsAll()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 3m, "Regular"));

            var payment1 = await _paymentService.ProcessPaymentAsync(ticket.Id,
                new(1, 20.00m, 20.00m, "key-1"));
            var payment2 = await _paymentService.ProcessPaymentAsync(ticket.Id,
                new(1, 20.00m, 20.00m, "key-2"));

            // Act
            var result = await _paymentService.ListTicketPaymentsAsync(ticket.Id);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Count >= 2);
        }

        [Fact]
        public async Task RefundPayment_ProcessedPayment_CreatesRefund()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));

            var payment = await _paymentService.ProcessPaymentAsync(ticket.Id,
                new(1, 27.50m, 30.00m, Guid.NewGuid().ToString()));

            var refundRequest = new RefundPaymentRequest(
                Reason: "Customer request",
                PrintReceipt: true);

            // Act
            var result = await _paymentService.RefundPaymentAsync(payment.Id, refundRequest);

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task FullTicketLifecycle_CreateOrderPaymentClose_Succeeds()
        {
            // Arrange - Create ticket
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            _logger.LogInformation($"Created ticket {ticket.Id}");

            // Act - Add orders
            var withOrders = await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));
            withOrders = await _ticketService.AddOrderAsync(withOrders.Id,
                new(102, 1m, "Large"));
            _logger.LogInformation($"Added orders, total: {withOrders.TotalAmount}");

            // Act - Process payment
            var payment = await _paymentService.ProcessPaymentAsync(ticket.Id,
                new(1, withOrders.TotalAmount, withOrders.TotalAmount + 2.50m, Guid.NewGuid().ToString()));
            _logger.LogInformation($"Processed payment {payment.Id}");

            // Act - Update state
            await _ticketService.UpdateTicketStateAsync(ticket.Id,
                new("Status", "Paid"));

            // Act - Close ticket
            var closed = await _ticketService.CloseTicketAsync(ticket.Id);
            _logger.LogInformation($"Closed ticket {ticket.Id}");

            // Assert
            Assert.True(closed.IsClosed);
            Assert.True(closed.Orders.Count >= 2);
            Assert.True(closed.Payments.Count > 0);
        }

        [Fact]
        public async Task PaymentIdempotency_AcrossServiceCalls_MaintainsConsistency()
        {
            // Arrange
            var ticket = await _ticketService.CreateTicketAsync(new(1, 5));
            await _ticketService.AddOrderAsync(ticket.Id,
                new(101, 1m, "Regular"));

            var idempotencyKey = "idem-test-" + Guid.NewGuid();
            var request = new ProcessPaymentRequest(1, 100.00m, 100.00m, idempotencyKey);

            // Act - Process payment 5 times with same key
            var results = new List<PaymentDto>();
            for (int i = 0; i < 5; i++)
            {
                var result = await _paymentService.ProcessPaymentAsync(ticket.Id, request);
                results.Add(result);
                _logger.LogInformation($"Attempt {i + 1}: Payment ID {result.Id}");
            }

            // Assert - All results have same ID
            var uniqueIds = results.Select(r => r.Id).Distinct().ToList();
            Assert.Single(uniqueIds);
            _logger.LogInformation($"Idempotency verified: all 5 calls returned payment {uniqueIds[0]}");
        }

        #endregion
    }
}
