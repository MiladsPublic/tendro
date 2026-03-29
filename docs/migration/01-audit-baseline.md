# Audit Baseline

Date: 2026-03-29
Scope: SambaPOS-3 repository

## Executive Summary

SambaPOS is currently a classic .NET Framework 4.0 WPF point-of-sale system with deep Windows runtime coupling. The codebase contains reusable domain and service logic, but modernization blockers exist in composition, transport, persistence stack, and UI runtime dependencies.

## Current Stack

- Runtime: .NET Framework 4.0 (many projects use Client Profile)
- UI: WPF + Prism region composition + MEF
- API: System.Web.Http self-hosted in legacy desktop process
- Messaging: legacy remoting patterns in messaging components
- Data access: EF4-era stack with older migration tooling
- Package management: mixed legacy references and old package conventions

## Strong Assets

- Business entities and much domain logic are already separated from views.
- Service abstractions exist for major POS workflows.
- Project is modularized by functional areas (ticket, payment, printer, etc.).

## High-Severity Blockers

1. MEF-heavy composition across modules and bootstrapping.
2. Legacy runtime patterns that are not supported in modern .NET paths.
3. EF4-era persistence not suitable for direct net10 migration.
4. WPF and Windows API dependence for printing and hardware orchestration.
5. Legacy API shape is too narrow for full web POS command workflows.

## Medium-Severity Blockers

1. Old package versions and technical debt in support libraries.
2. Limited automated integration tests for critical operational flows.
3. Implicit terminal-local assumptions for device and session behavior.

## Audit Conclusion

A direct cutover is high risk. The recommended approach is an incremental strangler migration:

1. Keep legacy runtime operational during transition.
2. Build modern API and terminal-facing contracts in parallel.
3. Deliver web POS in workflow slices with parity gates.

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
