# Stage 3 — `datetime` + `date` + `time` flow native (and stop collapsing)

**Seam:** three related types in one slice. Build `datetime` out, **create `date.@this` and `time.@this`**, and fix the live bug where a `date` silently becomes a `datetime`.

> **You own the final shape.** Whether `date`/`time` share helpers with `datetime`, the parts/formatting surface, the names — yours. Keep the disposition: three distinct types, `date`≠`datetime`, each born native.

## The bug this fixes (make it the load-bearing test)

Today a `date` value does **not** exist as itself. `ScalarComparer` (`data/ScalarComparer.cs`) coerces `DateOnly` → `DateTimeOffset` (`:69`) and classifies it as `"datetime"` (`:82`); `TimeOnly` has no arm at all (`:63` omits it). So `date == datetime` compares true and a date sorts in the datetime family. Giving `date`/`time` their own wrappers makes them distinct — write the proof that **fails before this stage and passes after**.

## Build

- **`datetime.@this`** (`type/datetime/`, exists, thin) — build out as `: item.@this`: compare (offset compare), truthiness, formatting/parts (year/month/day/…), value-equality, bare serializer (ISO `"o"`). Backed by `DateTimeOffset`; **accepts CLR `DateTime`** on construction (the type map already aliases `DateTime → datetime`, `primitive/this.cs:97`).
- **`date.@this`** — new, `type/date/this.cs`, backed by `DateOnly`, `: item.@this`. Its own type, **not** a `datetime` kind. Compare/truthiness/parts/equality/serializer. Declare `OwnedClrTypes = DateOnly` (mirror `datetime/this.Owns.cs`).
- **`time.@this`** — new, `type/time/this.cs`, backed by `TimeOnly`, `: item.@this`. Same surface. `OwnedClrTypes = TimeOnly`.

## Construction (born native)

- `UnwrapJsonElement` / parse paths produce `datetime.@this` / `date.@this` / `time.@this` by the value's type, not a coerced `DateTimeOffset` for everything.
- `variable.set` / catalog conversion: a value declared `date` materialises as `date.@this`, `time` as `time.@this`.

## Fix the collapse

- **`ScalarComparer`** (`:62-83`) — `IsDateTime` must stop swallowing `DateOnly`; `Name()` must return `"date"` for `DateOnly`, `"time"` for `TimeOnly`, `"datetime"` only for `DateTimeOffset`/`DateTime`. Better: once these flow as wrappers (self-comparing via `item`), the raw arms become unreachable — but until the perimeter is fully swept, the `Name()`/`ToOffset` arms must classify correctly, not merge.
- **`catalog/this.cs:497-498`** — the `DateOnly`/`TimeOnly` identity checks: confirm they route to the right type, not folded into datetime.

## Sweep the consumers

Census rows for `is System.DateTimeOffset|DateTime|DateOnly|TimeOnly`. Behavioral (date math, formatting) → methods on the wrapper; perimeter (a BCL call wanting a raw `DateTimeOffset`) → `.Value` unwrap.

## Acceptance

- `%d% is date` true, `%d% is datetime` **false**, for a date value (the load-bearing fix).
- `→ returns DateOnly` / `DateTimeOffset` / `TimeOnly` each reconstruct from their wrapper.
- date/datetime/time compare and sort within their own type; a date-vs-datetime comparison is a clean coercion outcome (mediator's call), **not** a silent equal.
- ISO round-trip through `.plang`; bare ISO on `.json`.

## Green

Both suites pass. `text` (Stage 2) stays native; `duration`/`bool`/`null` still raw.
