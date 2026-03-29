# Migration Status Audit

Date: 2026-03-29

## Summary

Migration has progressed through the planned Phase 4 web POS rollout slices, with a few non-Phase-4 hardening items still open.

- Modern API project (`Samba.ApiServer.Modern`) builds successfully.
- Web POS monorepo app (`Samba.POS.Web`) builds successfully.
- Ticket list/detail/create/add-order flows are wired from web UI to modern API.
- Settlement and operator action flows are now wired end-to-end:
   - process payment
   - list ticket payments
   - refund payment
   - close ticket
   - ticket state update
   - order state update
   - order void
- Phase 3 test project has compile failures and is not in a clean state.

## Phase-by-Phase Status

### Phase 0: Baseline and Safety Net

Status: Mostly complete

- Baseline scenarios and documentation exist (`06-phase0-baseline-scenarios.md`).
- Residual task: keep parity baselines updated as modern API behavior changes.

### Phase 1: Foundation Modernization

Status: Complete

- Logging, health checks, swagger, middleware, and auth scaffolding are in place.
- Route grouping and operational endpoints are functional.

### Phase 2: Domain and Data Path Modernization

Status: Partially complete

- Ticket and order endpoint surfaces exist and are functional enough for initial web integration.
- `POST /api/v2/payments` is now implemented and validated with ticket-scoped processing.
- Domain services still contain placeholder pricing/menu assumptions (`MenuItemName`, `UnitPrice`, payment type string mapping).

### Phase 3: Offline and Device Platform

Status: Not started (implementation scope)

- No terminal agent, hardware adapters, or offline queue replay implementation in modern stack yet.
- EF Core persistence layer and migrations are present, but offline/device objectives are still outstanding.

### Phase 4: Web POS Rollout

Status: Complete (workflow slice scope)

- `Samba.POS.Web` exists in monorepo with React + TypeScript + Vite.
- Shared app shell, routing, and shadcn-compatible component structure are implemented.
- Connected flows:
   - open tickets query
   - selected ticket detail query
   - create ticket mutation
   - add order mutation
   - process payment mutation
   - list/refund ticket payments
   - ticket close and ticket state update
   - order state update and order void
- Remaining hardening outside Phase 4 slice scope:
   - menu/modifier pricing still uses placeholder backend values
   - print/reprint integration is UI-level only (no hardware bridge yet)

## Verified Build/Test State

### Build status

- `dotnet build Samba.ApiServer.Modern/Samba.ApiServer.Modern.csproj -c Release`: succeeds with warnings.
- `npm run build` in `Samba.POS.Web`: succeeds.

### Test project status

- `dotnet build Samba.ApiServer.Modern.Tests/Samba.ApiServer.Modern.Tests.csproj -c Release`: fails.
- Current blockers are concentrated in `Phase3IntegrationTests.cs` (DTO/signature mismatch and invalid deconstruction assumptions).

## Cross-Cutting Outstanding Items

1. Fix modern API test project compile failures and align tests to current contracts.
2. Replace placeholder menu/price behavior with real menu and pricing source.
3. Address package vulnerability warnings in modern API and test projects (NU1902/NU1903 advisories).
4. Implement actual Phase 3 terminal/offline/device platform scope.

## Recommended Next Steps (Execution Order)

1. Stabilize quality baseline
   - Fix `Samba.ApiServer.Modern.Tests` compile failures.
   - Keep `Samba.ApiServer.Modern` and `Samba.POS.Web` builds green.

2. Stabilize transactional parity
   - Add/restore integration tests for payment + ticket close path.

3. Harden post-Phase-4 behavior
   - Replace placeholder menu/pricing logic.
   - Integrate real print/reprint pathway.

4. Security and operational hardening
   - Upgrade vulnerable NuGet packages.
   - Re-run build and tests, update risk/gate metrics.

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
