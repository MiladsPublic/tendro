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
