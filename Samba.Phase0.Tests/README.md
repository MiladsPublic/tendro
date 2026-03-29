# Samba.Phase0.Tests

Phase 0 Baseline Testing Suite for SambaPOS-3 Migration

## Overview

This test project defines reproducible regression gates for the five critical workflows in SambaPOS-3:

1. **User Login & Session**: PIN validation, token generation, session TTL
2. **Ticket Lifecycle**: Create, add orders, state machine transitions, close
3. **Payment Processing**: Full/partial/split payments, GL accounting, idempotency
4. **Print & Reprint**: Template rendering, printer routing, all device types
5. **Hardware Integration**: Cash drawer, Caller ID, customer display, serial ports

## Running Tests

### All tests:
```bash
dotnet test Samba.Phase0.Tests
```

### Specific scenario:
```bash
dotnet test Samba.Phase0.Tests --filter "UserLogin"
```

### With verbose output:
```bash
dotnet test Samba.Phase0.Tests -v detailed
```

## Test Structure

- **BaselineScenarioTests.cs**: Main test suite organized by scenario
- **TestFixture.cs**: In-memory test database, service mocks, helper methods

## Test Database

Tests use an in-memory EF Core context (no external DB required). Each test:
1. Initializes fixture (creates services, seeds data)
2. Executes scenario
3. Asserts expected outcomes
4. Cleans up (disposes fixture)

## Acceptance Thresholds

Performance targets (see [Phase 0 Baseline Scenarios](../docs/migration/06-phase0-baseline-scenarios.md)):

| Workflow | Target Duration | Tolerance |
|----------|-----------------|-----------|
| Login | < 2 sec | ±500ms |
| Ticket | < 500ms | ±100ms |
| Payment | < 200ms | ±50ms |
| Print | < 500ms | ±200ms |
| Hardware | < 1 sec | ±200ms |

## Phase 0 Exit Criteria

- ✓ All 5 scenarios implemented and reproducible
- ✓ Performance baselines measured
- ✓ Daily regression suite automated
- ✓ Ops team acceptance sign-off
- ✓ Baseline snapshots archived

## Development

### Adding a new test:

1. Add test method to `BaselineScenarioTests` (organize by scenario)
2. Use `_fixture` helpers for setup
3. Include clear Arrange-Act-Assert structure
4. Follow naming: `Scenario_Behavior_Expected()`

### Adding a new helper:

1. Add to `TestFixture` class or mock service
2. Document purpose and return values
3. Ensure idempotent (safe to call multiple times)

## Troubleshooting

### Test fails with "Database not initialized"
- Fixture initialization may have failed
- Check `TestDatabaseContext.InitializeAsync()`
- Verify EF Core in-memory provider installed

### Mock service returns null
- Helper may not have created entity
- Check `_testState` or helper return values
- Add `.Result` if using async methods

### Tests run sequentially (slow)
- xUnit runs tests in parallel by default
- Parallel issues: shared `_testState` dictionary
- Use `[Collection]` for ordering if needed

## References

- [Phase 0 Baseline Scenarios](../docs/migration/06-phase0-baseline-scenarios.md)
- [SambaPOS-3 Workflow Analysis](../docs/migration/04-reference-implementation.md)
- [Acceptance Criteria](../docs/migration/06-phase0-baseline-scenarios.md#cross-scenario-acceptance-criteria)
