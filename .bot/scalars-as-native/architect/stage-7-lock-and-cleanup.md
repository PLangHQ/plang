# Stage 7 — lock the constraint + delete the dead

**Seam:** the horizontal close-out. Every value now flows native (Stages 1–6); this stage makes the type system *enforce* it and removes the raw-scalar machinery that's now unreachable.

> **You own the final shape.** Whether the constraint goes on `Data<T>` directly or via an analyzer, how the mediator inspects wrappers — yours. Keep the disposition: the constraint compiles, the raw switches are gone, and `Unwrap*`/`Wrap*` shrink.

## 1. `Variable : item`

Make `app.variable.Variable` inherit `item.@this`. It stays `IRawNameResolvable` — the two are orthogonal (structural slot-fit vs. name-resolution behavior). This is what lets `Data<Variable>` satisfy `where T : item` with no exemption. Variable's `item` members can be minimal (equality by `Name`; order/truthiness unsupported-or-by-name — it's a reference, resolved before those are asked).

## 2. Swap the remaining `Data<rawCLR>` signatures

Every handler param and action return still typed `Data<int>` / `Data<string>` / `Data<bool>` / `Data<DateTimeOffset>` / `Data<TimeSpan>` / … → its wrapper (`Data<number>` / `Data<text>` / `Data<bool>` / `Data<datetime>` / `Data<duration>` / …). And `Data<object>` (the `→ returns data` polymorphic slot) → `Data<item>`. Use-sites read the wrapper; unwrap to raw `.Value` only at the BCL perimeter / conversion leaf.

## 3. Turn on the constraint — the compiler is your census

Add `where T : item` to `data.@this<T>`. **Every remaining `Data<rawCLR>` slot is now a build error** — fix until it compiles. No manual hunt for the signature sites; the compiler enumerates them. (`[Code] T` is unaffected — it's not a `Data<T>`. `Data<Variable>` compiles because of step 1.)

Watch for `Data<data.@this>` / accidental `Data<object>`-carrying-a-Data: the constraint rejects them (Data is not an `item`), which is the point — repoint those polymorphic forwarders to bare `Data` or `Data<item>`.

## 4. Collapse `ScalarComparer` + rewrite the coercion mediator

- `data/ScalarComparer.cs` — with no raw scalars left in flight, the per-type arms (`IsNumeric`, `IsDateTime`, `ToOffset`, the `Name()` switch, the `TimeSpan`/`DateTimeOffset` blocks) are unreachable. Collapse to coercion + a thin `IComparable` fallback. `Compare.cs`'s `IOrderableValue`/`IEquatableValue` dispatch already routes every wrapper to self-compare.
- `Operator.NormalizeTypes` (the one binary-coercion mediator) — rewrite its internals to inspect **wrapper types** (`a is text && b is number`, the one blessed type-discrimination site), not raw CLR. This is where `"5" == 5`, numeric widening, and date-vs-datetime get reconciled.

## 5. Delete the dead

- `ToBoolean`'s raw-scalar fallbacks (`is string ""`, `is bool`, null→false) — keep only what a genuine perimeter still needs; delete the rest (wrapped values never reach them).
- Retire `Unwrap*`/`Wrap*` where they now fall — `UnwrapJsonElement` is parse-to-native; **delete `UnwrapNewtonsoftToken`** (`data/this.cs:1387`, dead v1 shim, Newtonsoft is not a dependency — verify nothing live feeds JTokens first). Full elimination of the family may not all land here; none should remain *added*.

## Acceptance

- `where T : item` is in place and the solution **compiles** — no `Data<rawCLR>` slot survives; `Data<object>` is gone (→ `Data<item>`).
- A grep shows no `is <scalar>` / `(string)value` / `.Value is <scalar>` outside the two legal sites (perimeter `.Value`, the coercion mediator).
- `ScalarComparer`'s `Name()`/per-type switch is gone; `UnwrapNewtonsoftToken` is gone.
- `"5" == 5`, numeric widening, and date-vs-datetime still resolve through the mediator.

## Green

Both suites pass **and** the constraint compiles. This is the branch's done bar (`plan.md` "Done when").

## Mutation check (announce per CLAUDE.md)

With the branch complete, temporarily revert one wrapper's construction arm (e.g. make `UnwrapJsonElement` emit a raw `string` again) and confirm it fails to compile (constraint) or a body/integration test goes red. Proves the constraint + sweep actually bind. Announce before editing; revert immediately.
