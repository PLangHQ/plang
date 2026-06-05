# Stage 7 — lock the constraint + delete the dead

**Seam:** the horizontal close-out. Every value now flows native (Stages 1–6); this stage makes the type system *enforce* it and removes the raw-scalar machinery that's now unreachable.

> **You own the final shape.** Whether the constraint goes on `Data<T>` directly or via an analyzer, how the mediator inspects wrappers — yours. Keep the disposition: the constraint compiles, the raw switches are gone, and `Unwrap*`/`Wrap*` shrink.

## 1. Everything that rides a `Data<T>` slot becomes `: item`

Every type that appears as a concrete `Data<X>` is a value and inherits `item.@this`. Verified census on the base: `Data<Ask>` (3), `Data<snapshot>` (1), `Data<path>` (1), `Data<object>` (4), plus the leaf scalar swaps. So:
- **`path` / `image` / `code`** — real catalog values; `: item` (mechanical — they already implement the interfaces they honor).
- **`Variable`** — `: item`, stays `IRawNameResolvable` for name-binding (orthogonal: structural slot-fit vs. resolution behavior). This is what lets `Data<Variable>` satisfy the constraint. Its `item` surface is minimal — no ordering, equality-by-`Name` if needed; because `item` forces no contract, it writes no stub.
- **`Ask`** (resume-sentinel, `IExitsGoal`) and **`snapshot`** (execution-state capture) — `: item` too (Ingi: everything in a `Data` slot is a value). This costs nothing precisely because `item` forces no contract — they implement none of the three interfaces. (`snapshot` is slated for deletion; don't over-invest.) **Not** bare-`Data` — keep the typed slots, just make the types `: item`.

## 2. Swap the remaining `Data<rawCLR>` / `Data<object>` signatures

Every handler param and action return still typed `Data<int>` / `Data<string>` / `Data<bool>` / `Data<DateTimeOffset>` / `Data<TimeSpan>` / … → its wrapper (`Data<number>` / `Data<text>` / `Data<bool>` / `Data<datetime>` / `Data<duration>` / …). And `Data<object>` (the `→ returns data` polymorphic slot) → `Data<item>`. Use-sites read the wrapper; unwrap to raw `.Value` only at the BCL perimeter / conversion leaf. Finish the `object → item` fold if any of it is still outstanding from earlier stages.

## 3. Turn on the constraint — the compiler is your census, the cascade is the real cost

Add `where T : item` to `data.@this<T>`. **Every remaining `Data<rawCLR>` slot is a build error** — fix until it compiles; the compiler enumerates the signature sites, no manual hunt. (`[Code] T` is unaffected — not a `Data<T>`.)

**The cascade is the actual work, not the leaf swaps.** ~25 generic `Data<T>`/`Data<U>` infrastructure methods (`Merge`/`Clone`/`Ok<T>`/`Fail<T>` and friends) each name `Data<T>` and must thread `where T : item` through. Budget for this — it's the bulk of "turn the constraint on."

Watch for `Data<data.@this>` / accidental `Data<object>`-carrying-a-Data: the constraint rejects them (Data is not an `item`), which is the point — repoint those polymorphic forwarders to bare `Data` or `Data<item>`.

## 4. Collapse `ScalarComparer` + rewrite the coercion mediator

- `data/ScalarComparer.cs` — with no raw scalars left in flight, the per-type arms (`IsNumeric`, `IsDateTime`, `ToOffset`, the `Name()` switch, the `TimeSpan`/`DateTimeOffset` blocks) are unreachable. Collapse to coercion + a thin `IComparable` fallback. `Compare.cs`'s `IOrderableValue`/`IEquatableValue` dispatch already routes every wrapper to self-compare.
- `Operator.NormalizeTypes` (the one binary-coercion mediator) — rewrite its internals to inspect **wrapper types** (`a is text && b is number`, the one blessed type-discrimination site), not raw CLR. This is where `"5" == 5`, numeric widening, and date-vs-datetime get reconciled.

## 5. Delete the dead

- `ToBoolean`'s raw-scalar fallbacks (`is string ""`, `is bool`, null→false) — keep only what a genuine perimeter still needs; delete the rest (wrapped values never reach them).
- Retire `Unwrap*`/`Wrap*` where they now fall — `UnwrapJsonElement` is parse-to-native; **delete `UnwrapNewtonsoftToken`** (`data/this.cs:1387`, dead v1 shim, Newtonsoft is not a dependency — verify nothing live feeds JTokens first). Full elimination of the family may not all land here; none should remain *added*.

## Acceptance

- `where T : item` is in place and the solution **compiles** — no `Data<rawCLR>` slot survives; `Data<object>` is gone (→ `Data<item>`); there is no enduring PLang `object` type.
- **Double-wrap is structurally impossible** (first-class criterion): `data.@this<T> : data.@this`, `Data` is not an `item`, so `Data<item>` cannot nest a `Data` — a C# test that tries `Data<data.@this>` must not compile.
- A grep shows no `is <scalar>` / `(string)value` / `.Value is <scalar>` outside the two legal sites (perimeter `.Value`, the coercion mediator).
- `ScalarComparer`'s `Name()`/per-type switch is gone; `UnwrapNewtonsoftToken` is gone.
- `"5" == 5`, numeric widening, and date-vs-datetime still resolve through the mediator; `dict` still throws `NotOrderableException` on sort (no order leaked in via `item`).

## Green

Both suites pass **and** the constraint compiles. This is the branch's done bar (`plan.md` "Done when").

## Mutation check (announce per CLAUDE.md)

With the branch complete, temporarily revert one wrapper's construction arm (e.g. make `UnwrapJsonElement` emit a raw `string` again) and confirm it fails to compile (constraint) or a body/integration test goes red. Proves the constraint + sweep actually bind. Announce before editing; revert immediately.
