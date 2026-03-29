# Implementation Plan

Date: 2026-03-29

## Delivery Strategy

Use incremental strangler delivery with strict parity and reliability gates. No big-bang cutover.

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

Work Items:

1. Build UI slices in value order:
   - terminal login and station state
   - ticket list and open/close flows
   - menu ordering and modifiers
   - payment and settlement
   - reprint, void, refund
2. Pilot by store and hardware profile.
3. Roll out progressively using feature flags and rollback controls.

Exit Criteria:

- Web POS meets parity score and SLO targets.
- Legacy paths can be retired for migrated locations.
