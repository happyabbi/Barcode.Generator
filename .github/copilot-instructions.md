# Copilot Instructions — Barcode.Generator

These instructions guide AI coding assistance for this repository.

## Project overview

This repo contains:
- `src/Barcode.Generator`: barcode generation library
- `src/Demo.WebApi`: ASP.NET Core minimal API (barcode + POS/inventory APIs)
- `src/Demo.Web`: React + Vite frontend
- `src/Barcode.Generator.Tests`: xUnit integration tests

Current product direction includes POS + inventory MVP work (see `docs/pos-mvp/*`).

## Core engineering rules

1. **Test locally before suggesting push-ready changes**
   - Use:
     - `./scripts/local-check.sh`
   - This script runs backend tests and frontend E2E checks.

2. **Prefer small, reviewable commits**
   - Keep changes scoped by concern (API, UI, tests, docs).

3. **Do not introduce destructive operations**
   - Avoid deleting user data or changing DB behavior without explicit migration plan.

4. **Maintain API compatibility when possible**
   - Existing endpoints should keep response shape unless intentionally versioned.

## Backend conventions (`Demo.WebApi`)

- Keep minimal API style consistent with existing `Program.cs`.
- Use explicit validation and clear error payloads (`{ error: "..." }`).
- Use transaction boundaries for checkout/order operations.
- SQLite is primary local database; avoid unsupported LINQ/SQL patterns.
  - Example: ordering by `DateTimeOffset` can fail in SQLite translation.
- For money values, keep decimal precision and deterministic rounding.

## Frontend conventions (`Demo.Web`)

- Keep UI practical and dense for POS workflows.
- Favor clear validation and actionable user messages over generic errors.
- Preserve responsive behavior (especially form/table layouts on narrow screens).
- For API calls, gracefully handle network/server failures.

## Testing conventions

- Backend tests use xUnit integration tests with `TestWebApplicationFactory`.
- Each test fixture should isolate DB state (no shared mutable SQLite file across tests).
- Frontend E2E uses Playwright under `src/Demo.Web/tests/e2e`.
- When adding new feature flows, add at least one happy-path and one failure-path test.

## Sprint focus (current)

Sprint 2 targets:
- checkout transaction flow
- sales order endpoints
- POS checkout frontend flow

Before starting a Sprint 2 code task, review:
- `docs/pos-mvp/SPRINT_PLAN.md`
- `docs/pos-mvp/POS_CHECKOUT_FLOW.md`

## PR/checklist expectations for AI-generated changes

When proposing a patch, include:
1. What changed
2. Why it changed
3. Test evidence (local command + result)
4. Any known limitations or follow-up tasks
