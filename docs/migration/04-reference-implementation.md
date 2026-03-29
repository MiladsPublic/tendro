# Reference Implementation Guide

Date: 2026-03-29

## Purpose

This guide defines implementation conventions for new migration code so teams can build consistently and reduce long-term maintenance cost.

## Reference Service: Modern API Host

Project: Samba.ApiServer.Modern

Current baseline capabilities:

- net10.0 ASP.NET Core host
- Operational endpoints:
  - GET /health
  - GET /api/v2/system/info
- OpenAPI support in development

## API Conventions

1. Route shape:
   - /api/v2/{resource}
2. Contract style:
   - explicit request and response records
   - stable error envelope format
3. Versioning:
   - keep v2 prefix stable for migration period
4. Idempotency:
   - write endpoints accept idempotency keys
5. Correlation:
   - include request id in logs and error responses

## Service Layer Conventions

1. Keep business invariants in service layer, not controllers.
2. Keep controllers thin and focused on validation and mapping.
3. Keep transport concerns out of domain logic.
4. Define interfaces for device and offline orchestration boundaries.

## Offline and Hardware Conventions

1. Treat device actions as commands with explicit result states.
2. Persist command queue entries with replay-safe identifiers.
3. Implement deterministic retry policy with dead-letter handling.
4. Surface operator-facing diagnostics for pending and failed actions.

## Testing Conventions

1. Unit tests for business rules and mapping.
2. Integration tests for command workflows and transaction outcomes.
3. Contract tests for v2 endpoints and error schemas.
4. End-to-end tests for offline queue replay and device command execution.

## Initial Next Implementation Tasks

1. Add POST /api/v2/auth/pin endpoint with modern token issuance.
2. Add GET /api/v2/tickets and GET /api/v2/tickets/{id} parity endpoints.
3. Add POST /api/v2/tickets to start command-path migration.
4. Add structured error contract and middleware exception mapping.

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
