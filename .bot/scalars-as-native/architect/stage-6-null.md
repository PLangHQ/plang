# Stage 6 — `null` flows native (singleton wrapper)

**Seam:** the last wrapper. A `null` value gets a type so the `is null` / `== null` value-switches dissolve like the rest — but kept a singleton, and kept distinct from "no Data at all."

> **You own the final shape.** Singleton access pattern, member names — yours. Keep the two disposition guards below; they're what stop `null.@this` from becoming a footgun.

## Build

Create `type/null/this.cs` — `null.@this : item.@this`, a **singleton** (`null.@this.Instance`), not a per-value allocation:
- **Truthiness** — always false.
- **Equality** — `null == null` true; equal to nothing else.
- **Compare** — nulls sort last (match the existing `ScalarComparer` null policy / the collections null-ordering rule).
- **Serializer** — bare `null` on `application/json`; self-describing on `.plang`.
- `null.@this : item.@this` so it rides `Data<item>` slots.

## The two guards (read before building)

1. **Singleton.** There is one null. `Data.Null()` (`data/this.cs:570`) stays the factory and stamps the singleton — don't allocate a fresh `null.@this` per null value.
2. **The null *value*, not the absence of a Data.** A `Data` whose value is null → `null.@this`. A *missing* variable / `NotFound` / uninitialised read (`data/this.cs:571-572`, `IsInitialized = false`) is a null **`data` reference** — no box at all — and stays a C# null. `null.@this` must not try to represent "no Data." Keep that line bright: `Data.Null()` (present, value null) vs. a null `data` reference (absent).

## Construction (born native)

- `UnwrapJsonElement` `JsonValueKind.Null`/`Undefined` (`:1342-1343`) → `Data.Null()` carrying the `null.@this` singleton, not a bare C# `null` value.
- Any producer that today sets `_value = null` for a *present* null value routes through `Data.Null()`.

## Sweep + collapse

- Census `is null` / `== null` / `_value == null` rows that branch on a **value** being null (truthiness, equality, empty checks like `IsEmpty` `:559`). Those move onto `null.@this` / dissolve.
- **Do not** sweep the `IsInitialized` / `NotFound` / null-`data`-reference checks — those are the absence axis, not the null-value axis. Leaving them is correct.
- `ToBoolean` null→false fallback and `ScalarComparer` `Name() => "null"` (`:76`) become unreachable for the wrapped null value.

## Acceptance

- `set %x% = null` → `%x%` is the `null.@this` singleton; `if %x%` is falsy; `%x% == null` true.
- A missing variable still reads as an absent/`NotFound` `data` reference, **not** `null.@this` (the guard).
- Bare `null` on `.json`; self-describing on `.plang`.
- Nulls sort last in a mixed list.

## Green

Both suites pass. All scalars now flow native. The raw `ScalarComparer`/`ToBoolean` arms are unreachable except at the perimeter — Stage 7 deletes them.
