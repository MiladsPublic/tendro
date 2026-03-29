# Target Architecture

Date: 2026-03-29

## Architecture Goals

1. Modernize runtime to .NET 10.
2. Support web-first UX across Windows, macOS, and tablets.
3. Preserve offline operation at terminal level.
4. Preserve hardware workflows: receipt and kitchen printing, cash drawer, customer display, serial devices.
5. Reduce deployment risk by phased coexistence with legacy system.

## Target Components

1. Modern API host (ASP.NET Core net10)
   - Canonical command and query surface for POS workflows.
   - Stateless auth/session model with role and permission enforcement.
   - Real-time updates channel for terminal synchronization.

2. Web POS frontend (PWA-first)
   - Browser-based UI for operators.
   - Optimized for touch workflows and constrained device layouts.
   - Offline-aware UI state with queue visibility and recovery UX.
   - Monorepo project path: `Samba.POS.Web` using React + TypeScript + Vite.

3. Terminal Agent (cross-platform service)
   - Runs on each POS station.
   - Handles local device operations and offline command queueing.
   - Bridges hardware actions from API/web client to local devices.

4. Persistence modernization layer
   - Modernized data access path in net10-compatible runtime.
   - Explicit transaction boundaries and idempotent write policies.

## Functional Boundaries

- API owns business invariants and workflow validation.
- Terminal Agent owns local hardware execution and offline buffering.
- Web UI owns interaction and operator workflow ergonomics.
- Web UI consumes the modern API as canonical truth and keeps local state only for UX and offline buffering.
- Legacy WPF remains fallback path until parity gates pass.

## Deployment Model

1. Phase-in model:
   - Legacy WPF + new API coexist in controlled environments.
2. Progressive adoption:
   - Selected terminals run web POS + terminal agent.
3. Full migration:
   - Legacy self-host API and WPF transactional paths retired after parity and reliability objectives are met.

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
