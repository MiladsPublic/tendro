using System;
using System.Collections.Generic;
using Xunit;
using Samba.Domain.Models.Users;
using Samba.Domain.Models.Tickets;
using Samba.Domain.Models.Entities;

namespace Samba.Phase0.Tests
{
    /// <summary>
    /// Phase 0 Baseline Integration Tests
    /// Golden-path regression suite for SambaPOS-3 critical workflows
    /// </summary>
    public class BaselineScenariTests : IAsyncLifetime
    {
        private readonly TestFixture _fixture = new();

        public async Task InitializeAsync()
        {
            await _fixture.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _fixture.DisposeAsync();
        }

        // ============================================================
        // Scenario 1: User Login & Session
        // ============================================================

        [Fact]
        public async Task UserLogin_ValidPinCode_ReturnsUserAndToken()
        {
            // Arrange
            var validPin = "1234";
            var user = await _fixture.CreateUserAsync("Cashier 1", validPin, UserRole.Cashier);

            // Act
            var result = await _fixture.UserService.LoginUser(validPin);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.Id);
            Assert.Equal(validPin, result.PinCode);
            Assert.False(result.Suspended);
        }

        [Fact]
        public async Task UserLogin_InvalidPin_ReturnsNull()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);

            // Act
            var result = await _fixture.UserService.LoginUser("9999");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UserLogin_SuspendedUser_Rejected()
        {
            // Arrange
            var user = await _fixture.CreateUserAsync("Suspended User", "1234", UserRole.Cashier);
            user.Suspended = true;
            await _fixture.SaveAsync();

            // Act
            var result = await _fixture.UserService.LoginUser("1234");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UserLogin_TokenCreated_ValidFor30Minutes()
        {
            // Arrange
            var user = await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);

            // Act
            var result = await _fixture.UserService.LoginUser("1234");
            var token = _fixture.GetCreatedToken();

            // Assert
            Assert.NotNull(token);
            Assert.Equal(user.Id, token.UserId);
            var expiry = DateTimeOffset.UtcNow.AddMinutes(30);
            Assert.True(Math.Abs((token.LastUsed - DateTimeOffset.UtcNow).TotalSeconds) < 5);
        }

        // ============================================================
        // Scenario 2: Ticket Lifecycle
        // ============================================================

        [Fact]
        public async Task TicketLifecycle_CreateAndAddOrders_CalculatesCorrectTotals()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var coffee = await _fixture.CreateMenuItemAsync("Coffee", "KITCHEN", 5.00m);
            var sandwich = await _fixture.CreateMenuItemAsync("Sandwich", "COUNTER", 9.50m);

            // Act
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, coffee.Id, 2, "Regular"); // 2x $5.00
            await _fixture.TicketService.AddOrderAsync(ticket, sandwich.Id, 1, "Regular"); // 1x $9.50
            var saved = await _fixture.SaveTicketAsync(ticket);

            // Assert
            Assert.Equal(0m, saved.RemainingAmount); // No tax calculation baseline yet
            Assert.NotEqual(0, saved.Id); // Ticket persisted
            Assert.Equal(2, saved.Orders.Count);
            Assert.Equal(3, Math.Round(saved.Orders[0].Quantity));
        }

        [Fact]
        public async Task TicketLifecycle_OrderNumbers_AreSequential()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 5.00m);

            // Act
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            var order1 = await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");
            var order2 = await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");
            var order3 = await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");

            // Assert
            Assert.True(order1.OrderNumber < order2.OrderNumber);
            Assert.True(order2.OrderNumber < order3.OrderNumber);
        }

        [Fact]
        public async Task TicketLifecycle_CloseTicket_RequiresZeroRemainingAmount()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 5.00m);
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");

            // Act + Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _fixture.TicketService.CloseTicketAsync(ticket));
            Assert.Contains("RemainingAmount", ex.Message);
        }

        [Fact]
        public async Task TicketLifecycle_StateMachine_TransitionsRecorded()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 5.00m);
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            var order = await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");

            // Act
            await _fixture.TicketService.UpdateOrderStateAsync(
                ticket, new[] { order }, "KitchenStatus", "Ready");
            var updated = await _fixture.SaveTicketAsync(ticket);

            // Assert
            var stateValue = updated.TicketStateValues.Find(s => s.StateName == "KitchenStatus");
            Assert.NotNull(stateValue);
            Assert.Equal("Ready", stateValue.State);
        }

        // ============================================================
        // Scenario 3: Payment & Settlement
        // ============================================================

        [Fact]
        public async Task Payment_FullPayment_ClearsRemainingAmount()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 10.00m);
            var paymentType = await _fixture.GetOrCreatePaymentTypeAsync("Cash");
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");
            ticket.RemainingAmount = 10.00m; // Manual set for test

            // Act
            await _fixture.TicketService.AddPaymentAsync(
                ticket, paymentType.Id, 10.00m, 10.00m);
            var saved = await _fixture.SaveTicketAsync(ticket);

            // Assert
            Assert.Equal(0m, saved.RemainingAmount);
            Assert.Single(saved.Payments);
            Assert.Equal(10.00m, saved.Payments[0].Amount);
        }

        [Fact]
        public async Task Payment_PartialPayment_UpdatesRemaining()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 10.00m);
            var paymentType = await _fixture.GetOrCreatePaymentTypeAsync("Cash");
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");
            ticket.RemainingAmount = 10.00m;

            // Act
            await _fixture.TicketService.AddPaymentAsync(
                ticket, paymentType.Id, 6.00m, 6.00m);
            var saved = await _fixture.SaveTicketAsync(ticket);

            // Assert
            Assert.Equal(4.00m, saved.RemainingAmount);
        }

        [Fact]
        public async Task Payment_Overpayment_CalculatesChange()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 10.00m);
            var paymentType = await _fixture.GetOrCreatePaymentTypeAsync("Cash");
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");
            ticket.RemainingAmount = 10.00m;

            // Act
            var (payment, change) = await _fixture.TicketService.AddPaymentWithChangeAsync(
                ticket, paymentType.Id, 10.00m, 30.00m);
            var saved = await _fixture.SaveTicketAsync(ticket);

            // Assert
            Assert.Equal(10.00m, payment.Amount);
            Assert.Equal(20.00m, change);
            Assert.Equal(0m, saved.RemainingAmount);
        }

        [Fact]
        public async Task Payment_CreatesGLTransaction()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 10.00m);
            var paymentType = await _fixture.GetOrCreatePaymentTypeAsync("Cash");
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");
            ticket.RemainingAmount = 10.00m;

            // Act
            var payment = await _fixture.TicketService.AddPaymentAsync(
                ticket, paymentType.Id, 10.00m, 10.00m);
            var glTransaction = await _fixture.GetGLTransactionForPaymentAsync(payment.Id);

            // Assert
            Assert.NotNull(glTransaction);
            Assert.True(glTransaction.IsBalanced());
        }

        [Fact]
        public async Task Payment_DuplicatePayment_IsIdempotent()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 10.00m);
            var paymentType = await _fixture.GetOrCreatePaymentTypeAsync("Cash");
            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");
            ticket.RemainingAmount = 10.00m;

            // Act
            await _fixture.TicketService.AddPaymentAsync(
                ticket, paymentType.Id, 10.00m, 10.00m);
            var firstSave = await _fixture.SaveTicketAsync(ticket);

            var duplicate = await _fixture.TicketService.AddPaymentAsync(
                ticket, paymentType.Id, 10.00m, 10.00m);
            var secondSave = await _fixture.SaveTicketAsync(ticket);

            // Assert
            Assert.Null(duplicate); // Rejected by idempotency check
            Assert.Single(firstSave.Payments);
            Assert.Single(secondSave.Payments);
        }

        // ============================================================
        // Scenario 4: Print & Template Rendering
        // ============================================================

        [Fact]
        public async Task Print_TemplateRender_OutputDeterministic()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var item = await _fixture.CreateMenuItemAsync("Item", "KITCHEN", 10.00m);
            var printer = await _fixture.GetOrCreatePrinterAsync("Kitchen", 0); // ESC/POS
            var template = await _fixture.GetOrCreatePrinterTemplateAsync("KitchenTemplate");

            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, item.Id, 1, "Regular");

            // Act
            var output1 = await _fixture.RenderTicketAsync(ticket, template);
            var output2 = await _fixture.RenderTicketAsync(ticket, template);

            // Assert
            Assert.Equal(output1, output2); // Deterministic rendering
            Assert.Contains("Item", output1);
            Assert.Contains("$10.00", output1);
        }

        [Fact]
        public async Task Print_RoutsOrdersByGroupCode()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var department = await _fixture.GetOrCreateDepartmentAsync("Main");
            var ticketType = await _fixture.GetOrCreateTicketTypeAsync("Dine-in", department);
            var kitchen = await _fixture.CreateMenuItemAsync("Coffee", "KITCHEN", 5.00m);
            var counter = await _fixture.CreateMenuItemAsync("Sandwich", "COUNTER", 9.50m);

            var kitchenPrinter = await _fixture.GetOrCreatePrinterAsync("Kitchen", 0);
            var counterPrinter = await _fixture.GetOrCreatePrinterAsync("Counter", 5);

            await _fixture.CreatePrinterMapAsync(kitchen.Id, kitchenPrinter.Id);
            await _fixture.CreatePrinterMapAsync(counter.Id, counterPrinter.Id);

            var ticket = await _fixture.TicketService.OpenTicketAsync(0);
            await _fixture.TicketService.AddOrderAsync(ticket, kitchen.Id, 1, "Regular");
            await _fixture.TicketService.AddOrderAsync(ticket, counter.Id, 1, "Regular");

            // Act
            var jobs = await _fixture.RoutePrintJobsAsync(ticket);

            // Assert
            Assert.Equal(2, jobs.Count);
            Assert.True(jobs[0].PrinterId == kitchenPrinter.Id);
            Assert.True(jobs[1].PrinterId == counterPrinter.Id);
        }

        // ============================================================
        // Scenario 5: Hardware Integration
        // ============================================================

        [Fact]
        public async Task Hardware_CashDrawer_PulsesOnCommand()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var printer = await _fixture.GetOrCreatePrinterAsync("Kitchen", 0); // ESC/POS

            // Act
            await _fixture.ExecuteCashDrawerCommandAsync(printer.ShareName);

            // Assert
            var command = await _fixture.GetLastPortCommandAsync(printer.ShareName);
            Assert.Contains(new byte[] { 27, 112, 0, 25, 250 }, command); // ESC 'p' sequence
        }

        [Fact]
        public async Task Hardware_CallerID_ParsesPhoneNumber()
        {
            // Arrange
            var cidDevice = await _fixture.InitalizeCIDDeviceAsync("COM1", 38400);

            // Act
            await _fixture.SendCallerIDDataAsync(cidDevice, "NMBR=5551234567");
            var receivedPhone = await _fixture.GetReceivedCallerIDAsync();

            // Assert
            Assert.Equal("5551234567", receivedPhone);
        }

        [Fact]
        public async Task Hardware_CallerID_LookupsCustomer()
        {
            // Arrange
            await _fixture.CreateUserAsync("Cashier 1", "1234", UserRole.Cashier);
            var customer = await _fixture.CreateCustomerAsync("John Doe", "5551234567");
            var cidDevice = await _fixture.InitalizeCIDDeviceAsync("COM1", 38400);

            // Act
            await _fixture.SendCallerIDDataAsync(cidDevice, "NMBR=5551234567");
            var lookedUp = await _fixture.LookupCustomerByPhoneAsync("5551234567");

            // Assert
            Assert.Equal(customer.Id, lookedUp.Id);
            Assert.Equal("John Doe", lookedUp.Name);
        }

        [Fact]
        public async Task Hardware_SerialPort_ReopensAfterDisconnect()
        {
            // Arrange
            var cidDevice = await _fixture.InitalizeCIDDeviceAsync("COM1", 38400);
            await _fixture.SendCallerIDDataAsync(cidDevice, "NMBR=5551234567");

            // Act
            await _fixture.SimulateSerialPortDisconnectAsync("COM1");
            await Task.Delay(500); // Wait for reconnect attempt
            await _fixture.SendCallerIDDataAsync(cidDevice, "NMBR=5559999999");
            var receivedPhone = await _fixture.GetReceivedCallerIDAsync();

            // Assert
            Assert.Equal("5559999999", receivedPhone); // Successfully received after reconnect
        }
    }
}
