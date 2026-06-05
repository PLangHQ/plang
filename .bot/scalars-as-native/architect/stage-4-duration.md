# Stage 4 — `duration` flows native

**Seam:** the smallest of the time-ish types, on its own because its CLR backing (`TimeSpan`) and sweep are distinct from datetime's.

> **You own the final shape.** The parts surface (days/hours/total-seconds/…), names — yours. Keep the disposition: `TimeSpan` values flow as `duration.@this`.

## Build

`type/duration/` exists (thin). Build out as `: item.@this`:
- Compare (TimeSpan order), truthiness (zero vs non-zero — your policy; document it), parts (days/hours/minutes/total-X), value-equality, bare serializer.
- Backed by `TimeSpan`. `OwnedClrTypes = TimeSpan` already declared (`duration/this.Owns.cs` — verify). The type map keeps `timespan` as a deprecated alias (`primitive/this.cs:40`) — `duration` is canonical.

## Construction (born native)

Parse / `variable.set` / catalog conversion produce `duration.@this` for `TimeSpan`-shaped values.

## Sweep + collapse

- Census rows for `is System.TimeSpan` (e.g. `ScalarComparer:46`, the `ta`/`tb` arm). Behavioral → method on `duration`; perimeter → `.Value`.
- The `ScalarComparer` TimeSpan arm and `Name() => "duration"` (`:81`) become unreachable for wrapped values; leave for the perimeter, delete in Stage 7.

## Acceptance

- `set %d% = "1.5 hours"` (or however duration literals build) → `duration.@this`; parts and compare behave; `→ returns TimeSpan` reconstructs.
- Two equal durations are value-equal (dict key / `HashSet`).
- `.plang` round-trip preserves; `.json` bare.
- Truthiness policy holds (zero-duration falsy/truthy per your documented choice).

## Green

Both suites pass. `text`, `datetime`/`date`/`time` stay native; `bool`/`null` still raw.
