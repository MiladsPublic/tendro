# Target Architecture

Date: 2026-03-29

## Architecture Goals

1. Modernize runtime to .NET 10.
2. Support web-first UX across Windows, macOS, and tablets.
3. Preserve offline operation at terminal level.
4. Preserve hardware workflows: receipt and kitchen printing, cash drawer, customer display, serial devices.
5. Reduce deployment risk by phased coexistence with legacy system.

## Target Components

1. Modern API host (ASP.NET Core net10)
   - Canonical command and query surface for POS workflows.
   - Stateless auth/session model with role and permission enforcement.
   - Real-time updates channel for terminal synchronization.

2. Web POS frontend (PWA-first)
   - Browser-based UI for operators.
   - Optimized for touch workflows and constrained device layouts.
   - Offline-aware UI state with queue visibility and recovery UX.

3. Terminal Agent (cross-platform service)
   - Runs on each POS station.
   - Handles local device operations and offline command queueing.
   - Bridges hardware actions from API/web client to local devices.

4. Persistence modernization layer
   - Modernized data access path in net10-compatible runtime.
   - Explicit transaction boundaries and idempotent write policies.

## Functional Boundaries

- API owns business invariants and workflow validation.
- Terminal Agent owns local hardware execution and offline buffering.
- Web UI owns interaction and operator workflow ergonomics.
- Legacy WPF remains fallback path until parity gates pass.

## Deployment Model

1. Phase-in model:
   - Legacy WPF + new API coexist in controlled environments.
2. Progressive adoption:
   - Selected terminals run web POS + terminal agent.
3. Full migration:
   - Legacy self-host API and WPF transactional paths retired after parity and reliability objectives are met.
