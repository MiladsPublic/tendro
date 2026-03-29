# Implementation Plan

Date: 2026-03-29

## Delivery Strategy

Use incremental strangler delivery with strict parity and reliability gates. No big-bang cutover.

## Current Audit Snapshot (2026-03-29)

- `Samba.ApiServer.Modern` builds in Release configuration.
- `Samba.POS.Web` builds and includes live ticket/list/detail/create/add-order/settlement integration.
- Payment processing path is implemented (`POST /api/v2/payments`) and ticket-scoped.
- `Samba.ApiServer.Modern.Tests` currently fails to compile due to contract drift in `Phase3IntegrationTests.cs`.
- Offline/device platform scope (terminal agent, hardware adapters, sync conflict flow) has not started.

## Immediate Outstanding Work

1. Correct test harness drift:
   - Align `Phase3IntegrationTests.cs` to current API contracts and `PagedResponse<T>` shape.
2. Replace placeholder pricing/menu assumptions:
   - Remove hardcoded order pricing/menu naming from domain service layer.
3. Land backend-supported print/reprint integration (currently UI-only acknowledgement).
4. Address package security warnings surfaced by build (`NU1902`, `NU1903`).

## Phase Plan

### Phase 0: Baseline and Safety Net

Objectives:

- Freeze baseline behavior for critical workflows.
- Define non-functional targets and hardware support matrix.

Work Items:

1. Build golden-path scenario catalog for:
   - login/session
   - ticket lifecycle
   - payment and close
   - print and reprint
   - hardware interactions
2. Add baseline integration tests around current service behavior where feasible.
3. Define acceptance thresholds for offline and sync outcomes.

Exit Criteria:

- Baseline scenarios documented and reproducible.
- Test harness available for regression comparisons.

### Phase 1: Foundation Modernization

Objectives:

- Stand up modern API host and migration scaffolding.

Work Items:

1. Create modern host (completed): Samba.ApiServer.Modern.
2. Add structured logging, error contracts, request correlation, and health metadata.
3. Introduce v2 API route standards and domain-oriented endpoint grouping.
4. Define auth and permission model for modern clients.

Exit Criteria:

- Modern host builds in CI and exposes operational endpoints.
- Standard middleware and observability baselines are active.

### Phase 2: Domain and Data Path Modernization

Objectives:

- Expose reliable command and query paths for core POS workflows.

Work Items:

1. Implement first-class endpoints for tickets, orders, and payments.
2. Introduce idempotency keys for write endpoints.
3. Modernize persistence path to net10-compatible data access.
4. Validate data invariants against legacy outputs.

Exit Criteria:

- Core transactional workflows can run entirely through modern API.
- Financial and tax outputs match baseline tolerances.

### Phase 3: Offline and Device Platform

Objectives:

- Support offline terminal operation and hardware parity.

Current Status:

- Not started. EF Core persistence work is in place, but terminal agent/device/offline features are not yet implemented.

Work Items:

1. Implement terminal agent protocol and local queue storage.
2. Add hardware command adapters:
   - print
   - cash drawer
   - customer display
   - serial transport
3. Add sync conflict handling and operator recovery workflows.

Exit Criteria:

- Sales can complete during network loss and sync safely after reconnect.
- Device actions meet reliability targets in pilot environments.

### Phase 4: Web POS Rollout

Objectives:

- Deliver production-ready web POS by workflow slices.
- Establish the monorepo web client foundation in `Samba.POS.Web` with React, TypeScript, Vite, and shadcn-compatible UI primitives.

Current Status:

- Completed for planned web rollout slices.
- Connected to modern API for ticket list/detail/create/add-order/settlement/refund/close and ticket/order state actions.
- Reprint action is currently a terminal-side placeholder acknowledgement pending Phase 3 hardware bridge.

Work Items:

1. Build UI slices in value order:
   - terminal login and station state
   - ticket list and open/close flows
   - menu ordering and modifiers
   - payment and settlement
   - reprint, void, refund
2. Land shared frontend foundations:
   - router, data client, local terminal state, design tokens, installable manifest
   - touch-first component library aligned to shadcn/ui conventions
3. Pilot by store and hardware profile.
4. Roll out progressively using feature flags and rollback controls.

Exit Criteria:

- Web POS meets parity score and SLO targets.
- Legacy paths can be retired for migrated locations.

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
