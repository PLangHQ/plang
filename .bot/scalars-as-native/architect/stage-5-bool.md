# Stage 5 — `bool` flows native (the truthiness primitive)

**Seam:** create the wrapper that doesn't exist yet. `bool` is special — it *is* the truthiness primitive — so the contract bottoms out at the raw `bool` it wraps.

> **You own the final shape.** Member names, how truthiness bottoms out — yours. Keep the disposition (decided with Ingi): create `bool.@this` for uniform flow; the `is bool` sites dissolve.

## Build

Create `type/bool/this.cs` — `public sealed partial class @this : item.@this`, backed by a raw `bool`:
- **Truthiness** — `IBooleanResolvable` via `item`, returning the raw `bool` it wraps. This is where the turtles stop: every other type's `AsBooleanAsync` may delegate, `bool`'s *is* the value.
- **Equality** — value equality (`true == true`); `Equals`/`GetHashCode`.
- **Compare** — booleans order if you want them to (false < true) or stay equality-only; your call, document it.
- **Serializer** — bare `true`/`false` on `application/json`; self-describing on the `.plang` wire.
- `OwnedClrTypes = bool`. Keep an `implicit operator bool` for perimeter convenience; **no** implicit `object`.

## Construction (born native)

- `UnwrapJsonElement` `JsonValueKind.True`/`False` arms (`data/this.cs:1340-1341`) → `bool.@this`, not raw `bool`.
- `variable.set` / CLI / catalog conversion produce `bool.@this`.

## Sweep + collapse

- Census `is bool` rows. In `data/this.cs` alone: `:788` (`nb`), `:791` (`is bool b`), and others — most are perimeter probes (reading a `strict` flag). Behavioral → method; perimeter → `.Value`.
- `ScalarComparer` `Name() => "bool"` (`:77`) and the bool equality path become unreachable for wrapped values.
- `ToBoolean`'s raw `is bool` fast-path — unreachable for `bool.@this` (it's `IBooleanResolvable`); keep only for the perimeter, delete in Stage 7.

## Acceptance

- `set %b% = true` → `bool.@this`; `if %b%` truthy; `if !%b%` works; `→ returns bool` reconstructs.
- A condition action (`if`, `assert.IsTrue`) reads a `bool.@this` result correctly through `IBooleanResolvable`.
- Bare `true`/`false` on `.json`; self-describing on `.plang`.

## Green

Both suites pass. `text`, datetime-family, `duration` native; only `null` still raw.
