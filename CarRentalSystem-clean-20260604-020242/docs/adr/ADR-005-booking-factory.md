# ADR-005: Booking Factory Boundary

Date: 2026-05-19
Status: Proposed

## Context

Booking creation is currently spread across manual payment, PayOS paid hold conversion, cart checkout, and direct booking flows. The audit found duplicated defaults for status, deposit, service type, driving license, quote snapshot, and contract metadata.

## Decision

Introduce an `IBookingFactory` boundary before any large refactor. The factory should create bookings only from a validated quote or validated admin command. Controllers and payment providers should not directly assemble booking invariants.

## Scope

- Normalize status, service type, deposit status, reservation fee, collateral amount, license reference, quote snapshot, and audit metadata.
- Keep state transitions in `IBookingTransitionService`.
- Keep payment ledger writes in payment transition services.
- Do not change the state machine until transition tests protect the existing behavior.

## Rollout Plan

1. Add regression tests for the four existing creation paths.
2. Add `IBookingFactory` behind existing flows without changing public routes.
3. Move one creation path at a time.
4. Compare generated bookings before/after using snapshot assertions.
5. Remove duplicated construction only after parity evidence exists.

## Consequences

This blocks blind refactoring in Pass 2.1. `TASK-B14` stays Blocked until the test matrix and review evidence exist.
