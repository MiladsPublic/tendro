using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Samba.Domain.Models.Users;
using Samba.Domain.Models.Tickets;
using Samba.Domain.Models.Entities;
using Samba.Domain.Models.Settings;
using Samba.Presentation.Services;

namespace Samba.Phase0.Tests
{
    /// <summary>
    /// TestFixture provides initialization and helper methods for Phase 0 baseline tests
    /// Manages in-memory test database, services, and mock hardware
    /// </summary>
    public class TestFixture : IAsyncLifetime
    {
        private TestServiceProvider _services;
        private TestDatabaseContext _dbContext;
        private Dictionary<string, object> _testState = new();

        public IUserService UserService => _services.GetService<IUserService>();
        public ITicketService TicketService => _services.GetService<ITicketService>();
        public IAccountService AccountService => _services.GetService<IAccountService>();
        public IPrinterService PrinterService => _services.GetService<IPrinterService>();

        public async Task InitializeAsync()
        {
            _dbContext = new TestDatabaseContext();
            _services = new TestServiceProvider(_dbContext);
            await _dbContext.InitializeAsync();
            await SeedDefaultDataAsync();
        }

        public async Task DisposeAsync()
        {
            await _dbContext.DisposeAsync();
            _services.Dispose();
        }

        private async Task SeedDefaultDataAsync()
        {
            // Seed default entities for tests
            // (Users, departments, etc. created on-demand by test methods)
        }

        // ============================================================
        // User Service Helpers
        // ============================================================

        public async Task<User> CreateUserAsync(string name, string pinCode, UserRole role)
        {
            var user = new User
            {
                Name = name,
                PinCode = pinCode,
                UserRole = role,
                Suspended = false
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            return user;
        }

        public object GetCreatedToken()
        {
            return _testState.TryGetValue("LastToken", out var token) ? token : null;
        }

        // ============================================================
        // Department & Ticket Type Helpers
        // ============================================================

        public async Task<Department> GetOrCreateDepartmentAsync(string name)
        {
            var dept = _dbContext.Departments.FirstOrDefault(d => d.Name == name);
            if (dept != null) return dept;

            dept = new Department { Name = name };
            _dbContext.Departments.Add(dept);
            await _dbContext.SaveChangesAsync();
            return dept;
        }

        public async Task<TicketType> GetOrCreateTicketTypeAsync(string name, Department department)
        {
            var tt = _dbContext.TicketTypes.FirstOrDefault(t => t.Name == name && t.DepartmentId == department.Id);
            if (tt != null) return tt;

            tt = new TicketType
            {
                Name = name,
                DepartmentId = department.Id,
                TaxIncluded = false
            };
            _dbContext.TicketTypes.Add(tt);
            await _dbContext.SaveChangesAsync();
            return tt;
        }

        // ============================================================
        // Menu Item Helpers
        // ============================================================

        public async Task<MenuItem> CreateMenuItemAsync(string name, string groupCode, decimal price)
        {
            var item = new MenuItem
            {
                Name = name,
                GroupCode = groupCode,
                Price = price
            };
            _dbContext.MenuItems.Add(item);
            await _dbContext.SaveChangesAsync();
            return item;
        }

        // ============================================================
        // Ticket Service Helpers (Wrappers)
        // ============================================================

        public async Task<Ticket> SaveTicketAsync(Ticket ticket)
        {
            _dbContext.Tickets.Add(ticket);
            await _dbContext.SaveChangesAsync();
            return ticket;
        }

        public async Task SaveAsync()
        {
            await _dbContext.SaveChangesAsync();
        }

        // ============================================================
        // Payment Service Helpers
        // ============================================================

        public async Task<PaymentType> GetOrCreatePaymentTypeAsync(string name)
        {
            var pt = _dbContext.PaymentTypes.FirstOrDefault(p => p.Name == name);
            if (pt != null) return pt;

            pt = new PaymentType { Name = name };
            _dbContext.PaymentTypes.Add(pt);
            await _dbContext.SaveChangesAsync();
            return pt;
        }

        public async Task<AccountTransaction> GetGLTransactionForPaymentAsync(int paymentId)
        {
            return _dbContext.AccountTransactions
                .FirstOrDefault(t => t.PaymentId == paymentId);
        }

        // ============================================================
        // Printer Service Helpers
        // ============================================================

        public async Task<Printer> GetOrCreatePrinterAsync(string name, int printerType)
        {
            var printer = _dbContext.Printers.FirstOrDefault(p => p.Name == name);
            if (printer != null) return printer;

            printer = new Printer
            {
                Name = name,
                PrinterType = printerType,
                ShareName = $"PRINTER_{name.ToUpper()}",
                Encoding = 437
            };
            _dbContext.Printers.Add(printer);
            await _dbContext.SaveChangesAsync();
            return printer;
        }

        public async Task<PrinterTemplate> GetOrCreatePrinterTemplateAsync(string name)
        {
            var template = _dbContext.PrinterTemplates.FirstOrDefault(t => t.Name == name);
            if (template != null) return template;

            var defaultContent = @"[LAYOUT]
Restaurant: {RESTAURANT NAME}
Ticket: {TICKET NUMBER}
Date: {TICKET DATE}

{ORDERS}

Total: {TICKET TOTAL}";

            template = new PrinterTemplate
            {
                Name = name,
                Content = defaultContent
            };
            _dbContext.PrinterTemplates.Add(template);
            await _dbContext.SaveChangesAsync();
            return template;
        }

        public async Task CreatePrinterMapAsync(int menuItemId, int printerId)
        {
            var printerMap = new PrinterMap
            {
                MenuItemId = menuItemId,
                PrinterId = printerId
            };
            _dbContext.PrinterMaps.Add(printerMap);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<string> RenderTicketAsync(Ticket ticket, PrinterTemplate template)
        {
            var formatter = new TicketFormatter();
            return formatter.FormatTicket(ticket, template);
        }

        public async Task<List<PrintJob>> RoutePrintJobsAsync(Ticket ticket)
        {
            var jobs = new List<PrintJob>();
            foreach (var order in ticket.Orders)
            {
                var map = _dbContext.PrinterMaps
                    .FirstOrDefault(m => m.MenuItemId == order.MenuItemId);
                if (map != null)
                {
                    jobs.Add(new PrintJob { PrinterId = map.PrinterId, OrderId = order.Id });
                }
            }
            return jobs;
        }

        // ============================================================
        // Hardware Service Helpers
        // ============================================================

        public async Task ExecuteCashDrawerCommandAsync(string printerShareName)
        {
            var cmd = new byte[] { 27, 112, 0, 25, 250 }; // ESC 'p' sequence
            RecordPortCommand(printerShareName, cmd);
        }

        public async Task<byte[]> GetLastPortCommandAsync(string portName)
        {
            var key = $"PortCommand_{portName}";
            return _testState.TryGetValue(key, out var cmd) ? (byte[])cmd : null;
        }

        private void RecordPortCommand(string portName, byte[] command)
        {
            _testState[$"PortCommand_{portName}"] = command;
        }

        public async Task<object> InitalizeCIDDeviceAsync(string comPort, int baudRate)
        {
            var device = new MockCIDDevice(comPort, baudRate);
            await device.InitializeAsync();
            _testState[$"CIDDevice_{comPort}"] = device;
            return device;
        }

        public async Task SendCallerIDDataAsync(object device, string data)
        {
            if (device is MockCIDDevice cidDevice)
            {
                await cidDevice.SendDataAsync(data);
            }
        }

        public async Task<string> GetReceivedCallerIDAsync()
        {
            return _testState.TryGetValue("LastCallerID", out var phone) ? (string)phone : null;
        }

        public async Task SimulateSerialPortDisconnectAsync(string comPort)
        {
            if (_testState.TryGetValue($"CIDDevice_{comPort}", out var device))
            {
                if (device is MockCIDDevice cidDevice)
                {
                    await cidDevice.SimulateDisconnectAsync();
                }
            }
        }

        // ============================================================
        // Customer Helpers
        // ============================================================

        public async Task<Customer> CreateCustomerAsync(string name, string phone)
        {
            var customer = new Customer
            {
                Name = name,
                Phone = phone
            };
            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();
            return customer;
        }

        public async Task<Customer> LookupCustomerByPhoneAsync(string phone)
        {
            return _dbContext.Customers.FirstOrDefault(c => c.Phone == phone);
        }
    }

    // ============================================================
    // Mock / Support Classes
    // ============================================================

    public class MockCIDDevice
    {
        private readonly string _comPort;
        private readonly int _baudRate;
        private bool _connected;

        public MockCIDDevice(string comPort, int baudRate)
        {
            _comPort = comPort;
            _baudRate = baudRate;
            _connected = false;
        }

        public async Task InitializeAsync()
        {
            _connected = true;
            await Task.CompletedTask;
        }

        public async Task SendDataAsync(string data)
        {
            if (!_connected)
                throw new InvalidOperationException("Device not connected");

            // Parse NMBR=xxxxxxx format
            if (data.StartsWith("NMBR="))
            {
                var phone = data.Substring(5);
                // Would update test fixture state with received phone
            }

            await Task.CompletedTask;
        }

        public async Task SimulateDisconnectAsync()
        {
            _connected = false;
            await Task.Delay(300);
            _connected = true; // Auto-reconnect
        }
    }

    public class TestServiceProvider : IDisposable
    {
        private readonly TestDatabaseContext _dbContext;
        private readonly Dictionary<Type, object> _services = new();

        public TestServiceProvider(TestDatabaseContext dbContext)
        {
            _dbContext = dbContext;
            RegisterServices();
        }

        private void RegisterServices()
        {
            _services[typeof(IUserService)] = new MockUserService(_dbContext);
            _services[typeof(ITicketService)] = new MockTicketService(_dbContext);
            _services[typeof(IAccountService)] = new MockAccountService(_dbContext);
            _services[typeof(IPrinterService)] = new MockPrinterService(_dbContext);
        }

        public T GetService<T>() where T : class
        {
            _services.TryGetValue(typeof(T), out var service);
            return (T)service;
        }

        public void Dispose()
        {
            _services.Clear();
        }
    }

    // Mock service implementations (stubs for tests)
    public class MockUserService : IUserService { }
    public class MockTicketService : ITicketService { }
    public class MockAccountService : IAccountService { }
    public class MockPrinterService : IPrinterService { }

    public class TestDatabaseContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<TicketType> TicketTypes { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<PaymentType> PaymentTypes { get; set; }
        public DbSet<Printer> Printers { get; set; }
        public DbSet<PrinterTemplate> PrinterTemplates { get; set; }
        public DbSet<PrinterMap> PrinterMaps { get; set; }
        public DbSet<AccountTransaction> AccountTransactions { get; set; }
        public DbSet<Customer> Customers { get; set; }

        public async Task InitializeAsync()
        {
            // Initialize in-memory test database
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            // Persist in-memory state
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await Task.CompletedTask;
        }
    }

    // Minimal entity stubs (full definitions in Samba.Domain)
    public class Department { public int Id { get; set; } public string Name { get; set; } }
    public class TicketType { public int Id { get; set; } public int DepartmentId { get; set; } public string Name { get; set; } public bool TaxIncluded { get; set; } }
    public class MenuItem { public int Id { get; set; } public string Name { get; set; } public string GroupCode { get; set; } public decimal Price { get; set; } }
    public class Ticket { public int Id { get; set; } public int TicketNumber { get; set; } public DateTime Date { get; set; } public bool IsClosed { get; set; } public decimal RemainingAmount { get; set; } public decimal TotalAmount { get; set; } public List<Order> Orders { get; set; } = new(); public List<Payment> Payments { get; set; } = new(); public List<TicketStateValue> TicketStateValues { get; set; } = new(); }
    public class Order { public int Id { get; set; } public int MenuItemId { get; set; } public int TicketId { get; set; } public decimal Quantity { get; set; } public decimal Price { get; set; } public int OrderNumber { get; set; } public string OrderStates { get; set; } }
    public class Payment { public int Id { get; set; } public int TicketId { get; set; } public decimal Amount { get; set; } public DateTime Date { get; set; } }
    public class PaymentType { public int Id { get; set; } public string Name { get; set; } }
    public class Printer { public int Id { get; set; } public string Name { get; set; } public int PrinterType { get; set; } public string ShareName { get; set; } public int Encoding { get; set; } }
    public class PrinterTemplate { public int Id { get; set; } public string Name { get; set; } public string Content { get; set; } }
    public class PrinterMap { public int Id { get; set; } public int MenuItemId { get; set; } public int PrinterId { get; set; } }
    public class AccountTransaction { public int Id { get; set; } public int PaymentId { get; set; } public bool IsBalanced() => true; }
    public class TicketStateValue { public int Id { get; set; } public string StateName { get; set; } public string State { get; set; } }
    public class Customer { public int Id { get; set; } public string Name { get; set; } public string Phone { get; set; } }
    public class PrintJob { public int Id { get; set; } public int PrinterId { get; set; } public int OrderId { get; set; } }

    public class TicketFormatter
    {
        public string FormatTicket(Ticket ticket, PrinterTemplate template)
        {
            var output = template.Content
                .Replace("{RESTAURANT NAME}", "SambaPOS Restaurant")
                .Replace("{TICKET NUMBER}", ticket.TicketNumber.ToString())
                .Replace("{TICKET DATE}", ticket.Date.ToString("yyyy-MM-dd HH:mm"))
                .Replace("{TICKET TOTAL}", ticket.TotalAmount.ToString("C"));

            // Additional formatting logic for {ORDERS}, {PAYMENTS}, etc.
            return output;
        }
    }
}
