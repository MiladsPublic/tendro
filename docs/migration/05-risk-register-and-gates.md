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
