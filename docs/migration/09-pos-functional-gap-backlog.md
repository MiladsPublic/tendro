# POS Functional Gap Backlog

Date: 2026-03-29

## Purpose

Track all remaining POS functionality gaps explicitly, with execution slices and acceptance criteria.

## Remaining Functional Gaps

1. Identity, session, and authorization
- Operator login/session persistence for web POS terminals.
- Role/permission enforcement for sensitive actions (void/refund/close/reopen).
- Auth middleware alignment (`UseAuthentication`/`UseAuthorization`) with endpoint policies.

2. Shift lifecycle and handoff
- Open/close shift flow with persisted operator handoff.
- Cash reconciliation and shift totals.
- Work period transitions and safeguards.

3. Ticket lifecycle parity
- Reopen closed ticket flow.
- Ticket transfer/split/merge operations.
- Stronger ticket state persistence and transition rules.

4. Order capture parity
- Stable add-order persistence under repeated updates (partially addressed in Step 8).
- Modifiers with pricing impact and validation.
- Hold-send pacing/coursing and kitchen release controls.

5. Settlement parity
- Split tender, partial payments, tendered/change details.
- Supervised void and supervised refund with approval trail.
- Policy-based refund controls (reason/limits/permissions).

6. Device and print bridge
- Terminal-agent to hardware adapter execution (printer/drawer/display/serial).
- End-to-end print/reprint acknowledgement and retry semantics.

7. Offline resilience
- Replay retry/backoff/dead-letter semantics.
- Operator recovery flows for failed offline mutations.
- Deterministic conflict handling beyond duplicate-correlation detection.

8. Catalog and pricing parity
- Replace placeholder menu pricing assumptions with real menu/modifier pricing engine.
- Discount/promotions/tax edge handling parity.

9. Observability and quality
- Structured domain errors for operator-facing failures.
- Parity integration tests for ticket -> order -> settlement -> close critical path.
- Pilot readiness metrics and reliability SLO checks.

## Execution Slices

### Slice A - Ticket Write Path Stabilization (active)
Scope:
- Eliminate EF tracking conflicts in ticket read -> update path.
- Validate create ticket + add order + read ticket flow.

Acceptance:
- `POST /api/v2/tickets/{id}/orders` succeeds consistently.
- Ticket detail returns persisted order rows.

### Slice B - Reopen + supervised controls
Scope:
- Add reopen endpoint and UI action.
- Add supervised policy checks for void/refund actions.

Acceptance:
- Closed tickets can be reopened by authorized roles only.
- Void/refund denied without required approval context.

### Slice C - Modifiers + hold-send pacing
Scope:
- Implement modifier selection and payload persistence.
- Add hold/send workflow and release controls.

Acceptance:
- Modifier selections affect line totals.
- Held items do not dispatch until explicit send.

### Slice D - Shift lifecycle
Scope:
- Add shift open/close APIs, state persistence, and handoff events.
- Wire dashboard actions to real shift operations.

Acceptance:
- Shift boundaries are auditable and persisted.
- Operator handoff is reflected across sessions.

### Slice E - Offline replay hardening
Scope:
- Retry policy, replay outcome taxonomy, dead-letter queue, operator recovery actions.

Acceptance:
- Failed replays are recoverable and visible.
- No silent event loss during replay.

### Slice F - Device bridge
Scope:
- Integrate agent queue replay with real print/drawer/display adapters.

Acceptance:
- Reprint and cash drawer actions are executed and acknowledged with durable status.

## Notes

- Slice A is now partially delivered by Step 8 execution update in migration docs.
- Subsequent slices should be shipped with build validation and markdown execution updates per step.
