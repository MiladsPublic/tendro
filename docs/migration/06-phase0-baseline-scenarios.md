# Phase 0: Baseline Scenarios & Acceptance Criteria

Date: 2026-03-29

## Overview

Phase 0 establishes reproducible golden-path scenarios for the five critical workflows in SambaPOS-3. These scenarios serve as regression gates to ensure that any modernization effort maintains behavioral and financial parity with the baseline.

## Golden-Path Scenarios

### Scenario 1: User Login & Session

**Setup:**
- User account with PIN=1234, Role=Cashier, Suspended=false
- Department and Ticket Type configured
- RuleEventNames event handlers registered

**Steps:**
1. Launch application, login view displayed
2. Input PIN 1234
3. Validate via `IUserService.LoginUser("1234")`
4. ApplicationState.CurrentLoggedInUser populated
5. Token created (30-min TTL)
6. Automation rule triggered (RuleEventNames.UserLoggedIn)
7. Department context loaded, cache reset

**Expected Outcomes:**
- ✓ Current user accessible via ApplicationState
- ✓ Token persisted to disk
- ✓ UI updates reflect logged-in state
- ✓ Post-login automation rules executed
- ✓ Session remains valid for ≥30 min
- ✓ Invalid PIN returns validation error

**Regression Test Inputs:**
```
PIN=1234         → Success, token TTL=30min
PIN=9999         → Failure, error message shown
PIN=""           → Failure, validation error
PIN=null         → Failure, handled gracefully
User.Suspended   → Failure, "account suspended" message
```

**Acceptance Threshold:**
- User can log in within 2 seconds
- Token generation is deterministic for same PIN
- Session persists across app navigation

---

### Scenario 2: Ticket Lifecycle (Create → Order → Close)

**Setup:**
- Logged-in user with Cashier role
- Department and Ticket Type configured with tax rules
- Menu Items: Coffee ($5.00), Sandwich ($9.50), Beverage ($3.00)

**Steps:**
1. Create new ticket: `OpenTicket(0)`
   - Entry: TicketBuilder with department context
   - Auto-calculation: taxes applied from TicketType
   - Ticket held in memory (not persisted)
2. Add orders:
   - `AddOrder(Coffee, qty=2, portion="Regular")` → total $10.00 + tax
   - `AddOrder(Sandwich, qty=1, portion="Regular")` → total $9.50 + tax
   - `AddOrder(Beverage, qty=1, portion="Regular")` → total $3.00 + tax
3. Check state machine transitions (e.g., order state → "Kitchen")
4. Verify RemainingAmount = TotalAmount (before payment)
5. Close ticket after payment processing

**Expected Outcomes:**
- ✓ Ticket.TicketNumber assigned sequentially
- ✓ Ticket.Date set to server time
- ✓ TotalAmount calculated: sum of orders + taxes
- ✓ RemainingAmount = TotalAmount
- ✓ Orders persisted in memory with OrderNumber sequence
- ✓ State machine transitions valid (Order.OrderStates JSON)
- ✓ Ticket.IsClosed = false until closing step
- ✓ Tax calculation matches TicketType rules

**Regression Test Cases:**
```
Test: AddOrder(Coffee, qty=2)
  Expected: order.Price = $5.00, order.Quantity = 2, LineTotal = $10.00

Test: AddOrder then UpdateTicketState(state="Kitchen", value="Ready")
  Expected: TicketStateValue record created with timestamp

Test: AddOrder after empty ticket
  Expected: LastOrderDate updated, TotalAmount > 0

Test: Concurrent AddOrder calls
  Expected: Thread-safe, OrderNumbers unique and sequential

Test: Add order with invalid MenuItem ID
  Expected: Validation error, order not added

Test: Add order with qty=0
  Expected: Validation error or ignored
```

**Acceptance Thresholds:**
- Ticket creation < 100ms
- Tax calculation matches ± 0.01 of expected value
- State machine JSON serialization is reversible
- 100+ concurrent orders in single ticket without corruption

---

### Scenario 3: Payment & Settlement

**Setup:**
- Ticket with $27.50 total (including tax)
- Payment types configured: Cash, Card
- GL accounts configured (Customer → Cash/Card)

**Steps:**
1. Ticket has RemainingAmount = $27.50
2. Process payment:
   - `AddPayment(ticket, PaymentType.Cash, amount=$27.50, tendered=$30.00)`
   - System calculates changeAmount = $30.00 - $27.50 = $2.50
3. Payment service:
   - Creates AccountTransaction linking payment to GL accounts
   - Updates Ticket.LastPaymentDate
   - Recalculates RemainingAmount = $0.00
   - Publishes RuleEventNames.PaymentProcessed event
4. Verify GL entry: Customer account decreased by $27.50, Cash account increased
5. Close ticket (RemainingAmount = $0)

**Expected Outcomes:**
- ✓ Payment.Amount = $27.50 recorded
- ✓ Payment.Date = server time
- ✓ ChangeAmount calculated correctly: $2.50
- ✓ Ticket.RemainingAmount = $0.00 after full payment
- ✓ AccountTransaction created with source=Customer, dest=Cash GL
- ✓ Event published with all amounts/balances
- ✓ Multi-currency support verified (if applicable)
- ✓ Idempotent: re-processing same payment does not double-charge

**Regression Test Cases:**
```
Test: Underpayment ($25.00 on $27.50)
  Expected: RemainingAmount = $2.50, ticket remains open

Test: Overpayment ($30.00 on $27.50)
  Expected: RemainingAmount = $0, change = $2.50

Test: Partial payments (1st $15, then $12.50)
  Expected: Two Payment records, RemainingAmount → $0 on 2nd

Test: Split payment (Cash + Card)
  Expected: Two Payment records, GL accounts updated per type

Test: Payment with invalid PaymentType
  Expected: Validation error, no payment recorded

Test: Payment after ticket closed
  Expected: Business rule validation error

Test: Duplicate payment (same ticket, same amount within 1 sec)
  Expected: Second call rejected or idempotent
```

**Acceptance Thresholds:**
- Payment processed < 200ms
- GL accounting always double-entry balanced ± 0.01
- Change calculation accurate to cent
- Financial reports match DB state within 1 transaction

---

### Scenario 4: Print & Reprint

**Setup:**
- Ticket with orders (Coffee, Sandwich)
- Coffee → GroupCode "KITCHEN" → KitchenPrinterTemplate
- Sandwich → GroupCode "COUNTER" → CounterPrinterTemplate
- Printers configured: Kitchen (thermal ESC/POS), Counter (Windows)

**Steps:**
1. Create ticket with 2 orders (as per Scenario 2)
2. Execute `PrintTicket(ticket, PrintJob.KitchenOutput, orderFilter=new_unpaid)`
   - PrintJob routes orders by GroupCode
   - Resolves PrinterMap: Coffee → Kitchen printer, template
   - Renders PrinterTemplate with order details
   - TicketFormatter.FormatTicket() → text output
3. Print execution:
   - For thermal printer (ESC/POS):
     - Configure paper width, encoding (437/850)
     - Send command sequence to printer
     - Open cash drawer via `LinePrinter.OpenCashDrawer()`
   - For Windows printer:
     - System.Printing queue job
4. Reprint same ticket (no state change):
   - Same flow, orderFilter=all (not just unpaid)
   - Print queue contains both new and old orders

**Expected Outcomes:**
- ✓ Kitchen printer receives Coffee order only
- ✓ Counter printer receives Sandwich order only
- ✓ Template rendered correctly (restaurant name, order details, totals)
- ✓ Cash drawer opens after thermal print
- ✓ Ticket state changes reflect print event (if configured)
- ✓ Reprint generates exact duplicate output
- ✓ Error handling: printer offline → job queued for retry
- ✓ Support for all printer types (ESC/POS, serial, HTML, Windows, plugin)

**Regression Test Cases:**
```
Test: PrintTicket with new orders
  Expected: Template rendered, sent to correct printer queue

Test: Reprint same ticket
  Expected: Duplicate output, no state change on orders

Test: Print with custom template sections
  Expected: [LAYOUT], [ORDERS], [PAYMENTS] rendered correctly

Test: Print with cash drawer integration
  Expected: OpenCashDrawer() command sent to ESC/POS printer

Test: Print to offline printer
  Expected: Error logged, job queued, retry available

Test: Print to serial port (customer display)
  Expected: SerialPort.Write() executes, no blocking

Test: Multi-language ticket (UTF-8 with codepage override)
  Expected: Printer encoding applied, characters rendered correctly

Test: Print large ticket (50+ orders)
  Expected: Template pagination, multiple pages if needed
```

**Acceptance Thresholds:**
- Print queue submission < 500ms
- Template rendering deterministic (same output for same ticket)
- Offline printer handling does not block UI
- Hardware device commands (cash drawer) execute reliably ≥99%
- Print retry succeeds within 60s of reconnect

---

### Scenario 5: Hardware Integration (Cash Drawer & Caller ID)

**Setup:**
- ESC/POS thermal printer connected (USB or network)
- Cash drawer connected to printer (RJ-12 connector)
- Serial port device for customer display or caller ID
- Device configuration files in `{AppPath}/Documents/Devices/`

**Steps:**
1. **Cash Drawer Control:**
   - Payment processed to cash
   - `LinePrinter.OpenCashDrawer()` called
   - Sends ESC 'p' command sequence (27, 112, 0, 25, 250)
   - Drawer pulses open, physical verification (1-2 sec)
2. **Caller ID (CID Device):**
   - SerialPortService.InitializePort("COM1", 38400)
   - CidDevice.InitializeDevice() → SerialPort.DataReceived handler
   - Incoming call: NMBR=5551234567 received
   - Lookup phone number in customer database
   - Display customer name / CID info on POS screen
3. **Customer Display:**
   - PortPrinterJob formats output for 2x20 display
   - Sends via SerialPort (COM2, baud=9600)
   - Display shows "Ticket #123, Total: $27.50"

**Expected Outcomes:**
- ✓ Cash drawer opens within 1 second of command
- ✓ Drawer stays open until manually closed by operator
- ✓ Caller ID phone number parsed and displayed
- ✓ Customer lookup returns matching record or "Unknown"
- ✓ Customer display updated in real-time
- ✓ Device initialization graceful on startup
- ✓ Device finalization closes ports without errors on shutdown
- ✓ Configurable encoding (codepage 437, 850, 1252)

**Regression Test Cases:**
```
Test: OpenCashDrawer via ESC/POS
  Expected: Cash drawer pulses, audible click, opens ≥1 sec

Test: Caller ID reception (phone in NMBR=1234567890 format)
  Expected: Phone parsed, customer looked up, UI updated

Test: Multiple CID devices (redundancy)
  Expected: Both ports monitored, events de-duplicated

Test: Serial port already open
  Expected: Reuse existing connection, no double-open error

Test: Serial port unavailable (COM port doesn't exist)
  Expected: Graceful error, device offline, retry on startup

Test: Device unplug during operation
  Expected: DataReceived exception handled, port reset

Test: Encoding mismatch (codepage 437 vs 1252)
  Expected: Characters rendered per configuration

Test: Cash drawer + Caller ID concurrent (dual serial devices)
  Expected: Both queues processed without blocking
```

**Acceptance Thresholds:**
- Cash drawer opens deterministically on command
- Caller ID lookup < 200ms for 10k customer database
- Serial port recovery automatic within 10 seconds
- Device polling does not exceed 5% CPU on idle
- No port handle leaks over 8-hour operating day

---

## Cross-Scenario Acceptance Criteria

### Performance Baselines

| Workflow | Operation | Target Duration | Tolerance |
|----------|-----------|-----------------|-----------|
| Login | PIN validation + token creation | < 2 sec | ±500ms |
| Ticket | CreateTicket + AddOrder (x3) | < 500ms | ±100ms |
| Payment | AddPayment + GL accounting | < 200ms | ±50ms |
| Print | Template render + queue submit | < 500ms | ±200ms |
| Hardware | Cash drawer open | < 1 sec | ±200ms |
| Caller ID | Phone lookup + display | < 200ms | ±100ms |

### Reliability Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Payment processing success rate | ≥99.95% | Monthly PCI-compliant audit |
| Print job delivery | ≥99.5% | Kitchen receipts matched to tickets |
| Hardware cmd execution | ≥99% | Cash drawer + CID polling |
| Session TTL accuracy | ±1 sec | Token expiry timing across 24h test |

### Data Integrity

| Check | Requirement |
|-------|-------------|
| Ticket state machine | All transitions valid per JSON schema |
| GL accounting | Every payment creates balanced double-entry |
| Tax calculation | ± 0.01 of expected (accounting for rounding) |
| Order numbering | Sequential within ticket, unique across restaurant |
| Idempotency | Duplicate payment submission rejected or no-op |

---

## Test Fixture Initialization

### Database Seeding

```sql
-- User setup
INSERT INTO Users (UserId, Name, PinCode, UserRole, Suspended) 
  VALUES (1, 'Cashier 1', 1234, 2, 0);  -- Role: Cashier=2

-- Department + Ticket Type
INSERT INTO Departments (DepartmentId, Name) 
  VALUES (1, 'Main Department');
INSERT INTO TicketTypes (TicketTypeId, Name, DepartmentId) 
  VALUES (1, 'Dine-in', 1);

-- Menu Items
INSERT INTO MenuItems (MenuItemId, Name, GroupCode, Price) 
  VALUES 
    (1, 'Coffee', 'KITCHEN', 5.00),
    (2, 'Sandwich', 'COUNTER', 9.50),
    (3, 'Beverage', 'KITCHEN', 3.00);

-- Printers + Print Jobs
INSERT INTO Printers (PrinterId, Name, PrinterType, ShareName) 
  VALUES 
    (1, 'Kitchen', 0, 'THERMAL_USB'),  -- Type 0 = ESC/POS
    (2, 'Counter', 5, 'COUNTER_WIN'); -- Type 5 = Windows

-- GL Accounts
INSERT INTO Accounts (AccountId, Name, Type) 
  VALUES 
    (100, 'Customer', 'Asset'),
    (200, 'Cash', 'Asset'),
    (210, 'Credit Card', 'Asset');
```

### Automation Rules (Event Triggers)

```
Rule: OnUserLoggedIn
  Event: RuleEventNames.UserLoggedIn
  Action: ResetCache, LoadDepartment, LogAudit

Rule: OnPaymentProcessed
  Event: RuleEventNames.PaymentProcessed
  Action: CreateGLTransaction, UpdateBalance, PrintReceipt
```

---

## Regression Testing Protocol

### Daily Regression Suite (< 5 min execution)

1. **Happy Path (Scenario 2):** Create ticket → add 3 orders → close
2. **Payment Parity (Scenario 3):** Full + partial + split payments verify GL
3. **Print Output (Scenario 4):** Verify template render matches baseline
4. **Hardware (Scenario 5):** Test cash drawer pulse, Caller ID lookup

### Weekly Extended Suite (< 30 min execution)

- All scenarios with 10 data variants (edge cases)
- Concurrency testing (100+ simultaneous tickets)
- High-load print queue (500+ jobs)
- Device offline/reconnect recovery
- Serial port codec mismatch handling

### Monthly Regression Gate (before release)

- Full reproducibility of all scenarios in clean environment
- SLO baselines verified (performance thresholds met)
- Financial audit: GL balances, no missing transactions
- Hardware reliability: 8-hour operating day no errors
- Acceptance criteria sign-off from ops team

---

## Exit Criteria for Phase 0

✓ All five scenarios implemented and reproducible  
✓ Test fixture initialization script passing  
✓ Daily regression suite automated  
✓ Performance baselines measured and documented  
✓ Ops team sign-off on acceptance thresholds  
✓ Baseline snapshots archived in version control  

**Phase 0 Complete:** Ready for Phase 1 foundation modernization.

## Execution Update - 2026-03-29

- Step 1 (quality gate): Completed. `Samba.ApiServer.Modern.Tests` compile drift fixed against current API contracts.
- Step 2 (domain placeholder reduction): Completed for order naming/pricing lookup by introducing catalog-backed resolution in domain services.
- Step 3 (reprint pathway): Completed for backend-supported queueing via `/api/v2/print-jobs/reprint`, with frontend reprint action now calling backend API.
- Build verification: `dotnet build` succeeds for modern API and modern test project; `npm run build` succeeds for `Samba.POS.Web`.
- Remaining hardening: package vulnerability advisories (`NU1902`/`NU1903`) still outstanding.

## Execution Update - 2026-03-29 (Step 4)

- Step 4 (security hardening): Completed.
- Upgraded vulnerable dependency chains in modern API and modern test projects to net10-era package lines.
- Verification: modern API build passes, modern tests build passes, web app build passes.
- Vulnerability scan result: no vulnerable packages reported for `Samba.ApiServer.Modern` and `Samba.ApiServer.Modern.Tests` using current NuGet sources.
- Remaining follow-up: address non-security code warnings (e.g., nullable/auth token warning and analyzer warning) as a separate cleanup task.

## Execution Update - 2026-03-29 (Step 5)

- Step 5 (Phase 3 bootstrap): Started with a terminal-agent heartbeat slice.
- Added API contracts and endpoints for terminal heartbeats:
  - `POST /api/v2/terminal-agent/heartbeats`
  - `GET /api/v2/terminal-agent/heartbeats`
- Added in-memory terminal agent heartbeat service to track latest terminal status.
- Warning hardening: modern API and modern test projects now build cleanly with no current compiler/analyzer warnings.
- Step objective status: complete for initial heartbeat/protocol bootstrap; next Phase 3 slices are queue replay and device adapter bridging.

## Execution Update - 2026-03-29 (Step 6)

- Step 6 (Phase 3 queue replay bootstrap): Completed.
- Added queue replay protocol contracts:
  - `TerminalQueueEventRequest`
  - `TerminalQueueEventDto`
  - `TerminalQueueReplayResultDto`
- Added terminal-agent queue service capabilities:
  - enqueue offline event
  - list queued events by terminal
  - replay queued batch by terminal
- Added API endpoints:
  - `POST /api/v2/terminal-agent/queues/events`
  - `GET /api/v2/terminal-agent/queues/{terminalId}/events`
  - `POST /api/v2/terminal-agent/queues/{terminalId}/replay`
- Validation: modern API build, modern tests build, and web build all pass.
- Next slice: durable queue persistence + replay outcome tracking + conflict resolution policy enforcement.

## Execution Update - 2026-03-29 (Step 7)
- Step 7 (Phase 3 durable queue persistence): Completed.
- Replaced in-memory terminal queue storage with EF Core persistence in `TerminalQueueEvents`.
- Added conflict policy for duplicate `correlationId` per terminal (returns `Conflict` with `DuplicateCorrelationId`).
- Replay now updates durable event state (`Status=Replayed`, `ReplayOutcome=Applied`, `ReplayedAtUtc`) and returns remaining queued count from DB.
- Added EF migration `20260329023542_TerminalQueueEventPersistence` with table/indexes for queue replay operations.
- Validation: `dotnet build Samba.ApiServer.Modern`, `dotnet build Samba.ApiServer.Modern.Tests`, and `npm run build` (Samba.POS.Web) all succeeded.
