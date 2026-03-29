## Plan: SambaPOS to .NET 10 + Web Multiplatform

Migrate SambaPOS in a strangler pattern: first isolate and modernize business/runtime foundations, then deliver a web POS shell with offline-capable terminal agent and hardware bridge, while WPF remains operational until parity is proven. This meets your chosen direction (full web rewrite as primary track) but keeps rollout risk low by avoiding big-bang cutover.

**Steps**
1. Phase 0 - Baseline and guardrails
1. Freeze current behavior with regression baselines for ticket lifecycle, payments, printing, and device flows. Add golden-path integration tests around existing domain/services before major refactors.
1. Define non-functional targets: offline SLA, print latency, sync conflict policy, and supported hardware matrix by OS.
1. Phase 1 - Runtime modernization foundation
1. Convert core non-UI projects to SDK style and target net10.0 in this order: Samba.Domain -> Samba.Infrastructure -> Samba.Persistance -> Samba.Services. Keep changes mechanical first (project format, package references, compile fixes), then behavioral fixes.
1. Replace removed platform/runtime features: MEF composition roots (in core) -> Microsoft DI, AppDomain-era reflection patterns -> AssemblyLoadContext-safe discovery, remoting dependencies -> explicit transport abstractions.
1. Move packages.config to PackageReference and upgrade deprecated dependencies (EF4 stack, old Newtonsoft/FluentValidation/NUnit versions, old utility libs).
1. Phase 2 - Data and API extraction (blocks web UI)
1. Build a new ASP.NET Core net10 API host (parallel with final core-library cleanup), reusing Samba.Services contracts where feasible.
1. Replace EF4.4 data access with EF Core (or Dapper where device/queue hot paths need deterministic SQL), preserving business invariants in services.
1. Introduce production auth/session for web clients (JWT + refresh tokens + role/permission enforcement from existing user/permission model).
1. Implement full POS command/query endpoints (tickets, orders, payments, entities, menu, shifts/work periods), not just read endpoints.
1. Add real-time channel for terminal updates (SignalR/WebSocket) and idempotency keys for duplicate-safe writes.
1. Phase 3 - Offline and hardware platform (parallel with Phase 4 UI)
1. Implement a Terminal Agent (cross-platform service) running on each POS station: local queue, offline cache, sync engine, printer/cash drawer/customer-display/serial drivers.
1. Define API <-> Agent protocol for print jobs, drawer kicks, display pushes, serial events, health heartbeats.
1. Implement conflict resolution for offline ticket/payment mutations with deterministic merge rules and operator-visible recovery flows.
1. Phase 4 - Web UI rewrite (depends on Phase 2 API contracts)
1. Build a web POS frontend (PWA-first) against new API contracts with explicit module parity tracking from current WPF modules.
1. Deliver in slices by business value: login/terminal state -> ticket grid -> menu/ordering -> payment/close -> reprint/void/refund -> advanced operations.
1. Preserve existing automation/report workflows via API adapters, then redesign admin/config surfaces after POS parity.
1. Phase 5 - Controlled rollout and WPF decommission
1. Run dual-track production pilots (selected stores/terminals) with feature flags and rollback switches.
1. Track parity scorecard by workflow and hardware path; deprecate WPF modules only after sustained parity and reliability thresholds.
1. Retire legacy self-host API and remoting/messaging paths once all terminals use API + agent architecture.

**Parallelization and dependencies**
1. Can run in parallel: dependency/package audit, test baseline creation, API contract drafting.
1. Blocks all downstream work: Phase 2 core API command surface for ticket/order/payment.
1. Can run in parallel after initial API contracts: Terminal Agent implementation and Web UI slices.
1. Final cutover depends on: offline reliability KPIs, hardware success rates, and workflow parity completion.

**Relevant files**
- /Users/milad/Developer/SambaPOS-3/SambaPos.sln - solution-level retargeting and project migration sequencing.
- /Users/milad/Developer/SambaPOS-3/Samba.Domain/Samba.Domain.csproj - first core library SDK/net10 conversion template.
- /Users/milad/Developer/SambaPOS-3/Samba.Services/Samba.Services.csproj - business-service migration and DI composition boundaries.
- /Users/milad/Developer/SambaPOS-3/Samba.Persistance/Samba.Persistance.csproj - EF4 to EF Core migration center.
- /Users/milad/Developer/SambaPOS-3/Samba.Persistance/Data/DataContext.cs - DbContext/model mapping modernization.
- /Users/milad/Developer/SambaPOS-3/Samba.ApiServer/Samba.ApiServer.csproj - legacy API baseline and endpoint parity source.
- /Users/milad/Developer/SambaPOS-3/Samba.ApiServer/Controllers/LoginController.cs - current auth baseline to replace.
- /Users/milad/Developer/SambaPOS-3/Samba.ApiServer/Controllers/TicketsController.cs - current ticket read-model baseline.
- /Users/milad/Developer/SambaPOS-3/Samba.Presentation/Samba.Presentation.csproj - WPF dependency boundary and eventual decommission scope.
- /Users/milad/Developer/SambaPOS-3/Samba.Presentation.Services/Samba.Presentation.Services.csproj - identify UI orchestration logic to move behind API.

**Verification**
1. Build gates: clean build on net10 for migrated projects, no warnings treated-as-errors regressions.
1. Contract tests: API command/query behavior matches legacy service outcomes for golden scenarios.
1. Data correctness: ticket totals/tax/payment/accounting invariants match legacy outputs on migrated datasets.
1. Offline validation: terminate network mid-flow, complete sale offline, restore connection, verify deterministic sync and no duplicate financial records.
1. Hardware E2E: print receipt/kitchen, open drawer, update customer display, process serial device events from web POS via Terminal Agent.
1. Pilot metrics: p95 API latency, print dispatch latency, offline queue drain time, sync conflict rate, session/auth failure rates.

**Decisions**
- Included scope: net10 modernization, web POS primary path, cross-platform client support via browser plus terminal agent.
- Explicitly excluded from first wave: full admin/config redesign, historical report redesign, complete module-by-module WPF parity before pilot.
- Architecture decision: web-first UI with local terminal agent is mandatory to satisfy offline + hardware on Windows/macOS/tablet/browser.
- Risk decision: no big-bang migration; keep WPF runnable until measurable parity gates are met.

**Further Considerations**
1. Web stack selection recommendation: React + TypeScript + PWA for broad hiring/ecosystem support, unless your team is C#-dominant and strongly prefers Blazor.
2. Database strategy recommendation: standardize production on SQL Server/PostgreSQL-compatible model before expanding tablet/macOS rollout to reduce edge-case divergence.
3. Device support recommendation: define a certified hardware list early; unsupported variants should route through a plugin SDK, not custom per-client patches.
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

## Execution Update - 2026-03-29 (Step 8)
- Step 8 (ticket write-path stabilization): Completed.
- Fixed EF tracking conflict risk by making ticket aggregate reads no-tracking in `EfCoreTicketRepository`.
- Verified `POST /api/v2/tickets/{ticketId}/orders` no longer returns 500 in local container-backed runtime.
- Verified ticket detail reflects persisted order rows after add-order.
- Added explicit POS gap backlog and execution slices in `docs/migration/09-pos-functional-gap-backlog.md`.
- Validation: `dotnet build Samba.ApiServer.Modern`, `dotnet build Samba.ApiServer.Modern.Tests`, and runtime curl checks for create ticket/add order/get ticket succeeded.
