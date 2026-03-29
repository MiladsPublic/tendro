# SambaPOS Migration Documentation Pack

This folder contains the migration reference documents for moving SambaPOS from .NET Framework 4.0 + WPF to .NET 10 with a web-first, cross-platform architecture.

## Documents

1. [01-audit-baseline.md](01-audit-baseline.md)
   - Current-state technical audit and known blockers.
2. [02-target-architecture.md](02-target-architecture.md)
   - Target architecture for API, web POS, offline terminal behavior, and hardware.
3. [03-implementation-plan.md](03-implementation-plan.md)
   - Phased execution plan with sequencing, milestones, and ownership guidance.
4. [04-reference-implementation.md](04-reference-implementation.md)
   - Reference implementation blueprint and coding conventions for new services.
5. [05-risk-register-and-gates.md](05-risk-register-and-gates.md)
   - Risk register, mitigation actions, quality gates, and rollout criteria.

## Current Progress Snapshot

- Modern API host (`Samba.ApiServer.Modern`) is active on .NET 10 and builds successfully.
- Domain endpoint surface exists for tickets, orders, and payments.
- Web POS app (`Samba.POS.Web`) has completed planned Phase 4 workflow slices and builds successfully.
- Live web-to-API integration currently covers:
   - ticket list
   - ticket detail
   - ticket create
   - add order to ticket
   - process payment
   - refund payment
   - close ticket
   - ticket/order state actions
- Known outstanding gaps:
   - menu/pricing behavior still has placeholder backend assumptions
   - reprint remains UI-level acknowledgement until hardware bridge work
   - Phase 3 offline/device platform scope not started
   - modern API test project compile drift remains

See [PHASE_STATUS.md](PHASE_STATUS.md) for the current audit and next-step sequence.

## How To Use This Pack

1. Use the audit doc as the source of truth for migration constraints.
2. Use the implementation plan to sequence work and avoid high-risk coupling.
3. Use the reference implementation doc to keep coding patterns consistent.
4. Use risk and gate definitions before moving each phase to production.

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
