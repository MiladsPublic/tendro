# SambaPOS Migration Documentation Pack

This folder contains the migration reference documents for moving SambaPOS from .NET Framework 4.0 + WPF to .NET 10 with a web-first, cross-platform architecture.

## Documents

1. [01-audit-baseline.md](01-audit-baseline.md)
   - Current-state technical audit and known blockers.
2. [02-target-architecture.md](02-target-architecture.md)
   - Target architecture for API, web POS, offline terminal behavior, and hardware.
3. [03-implementation-plan.md](03-implementation-plan.md)
   - Phased execution plan with sequencing, milestones, and ownership guidance.
4. [04-reference-implementation.md](04-reference-implementation.md)
   - Reference implementation blueprint and coding conventions for new services.
5. [05-risk-register-and-gates.md](05-risk-register-and-gates.md)
   - Risk register, mitigation actions, quality gates, and rollout criteria.

## Current Progress Snapshot

- A new parallel modern API host has already been created in Samba.ApiServer.Modern.
- The project targets net10.0 and compiles successfully.
- Initial endpoints are available:
  - GET /health
  - GET /api/v2/system/info

## How To Use This Pack

1. Use the audit doc as the source of truth for migration constraints.
2. Use the implementation plan to sequence work and avoid high-risk coupling.
3. Use the reference implementation doc to keep coding patterns consistent.
4. Use risk and gate definitions before moving each phase to production.
