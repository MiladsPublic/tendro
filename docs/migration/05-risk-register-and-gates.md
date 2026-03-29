# Risk Register and Quality Gates

Date: 2026-03-29

## Top Risks

1. Functional parity gaps in financial flows.
   - Mitigation: golden-path regression suite and dual-run comparisons.

2. Offline sync conflicts causing duplicate or inconsistent records.
   - Mitigation: idempotency keys, deterministic merge rules, replay audit trail.

3. Hardware variance across locations.
   - Mitigation: certified device matrix and adapter conformance tests.

4. Performance regressions in peak service periods.
   - Mitigation: load testing with representative ticket and print volumes.

5. Operational complexity during coexistence period.
   - Mitigation: environment playbooks, observability standards, rollback plans.

## Quality Gates by Phase

### Foundation Gate

- Modern host builds in CI with zero blocking errors.
- Operational health and environment endpoints available.
- Logging and correlation baseline established.

### Core Workflow Gate

- Ticket, order, and payment APIs pass contract and integration tests.
- Tax and totals calculations are within accepted variance thresholds.
- Write endpoints are idempotent under retry conditions.

### Offline and Device Gate

- Network interruption scenarios complete without data loss.
- Queue replay resolves to single financial outcome per command.
- Device command success rates meet pilot threshold.

### Rollout Gate

- Pilot stores meet latency and reliability SLOs.
- Operational incidents stay below agreed threshold.
- Feature-flag rollback validated in staging and pilot.

## Suggested SLO Baseline

1. API p95 latency for critical write endpoints: <= 250 ms in pilot median load.
2. Print command dispatch success: >= 99.5 percent.
3. Offline queue replay reconciliation success: >= 99.9 percent.
4. Duplicate financial command rate: 0 tolerated after idempotency enforcement.

## Go/No-Go Checklist

1. Functional parity score approved by product and operations.
2. Pilot success metrics achieved for at least two stable cycles.
3. Support runbook and incident response flow signed off.
4. Rollback path tested and verified.

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
