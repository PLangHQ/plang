# Stage 8: Optional-param resolution — non-null Data, `[Default]`, `[NotNull]`

> **⚠ Not yet coder-audited.** This stage was added late, in direct conversation with Ingi — it has **not** been through the review loop Stages 1–7 went through with you. If anything here conflicts with what you've already built (especially the Stage-2 `.Value()` door you've landed), looks wrong, or doesn't fit the generator as it now stands, **stop and ask** — do not silently work around it. Treat the shapes below as proposals pending your read.

**Goal:** An optional action parameter resolves to a **non-null Data** (never a C# `null` reference), so a handler reads it with `await Param.Value()` or `await Param.Value(fallback)` — no `== null` guard, no `!`, no trailing `?? default`. Absence lives *inside* the Data (`IsInitialized`), not in a nullable reference.
**Scope:** Included — the source-gen Data-property emission (the nullable case), `Data.Uninitialized` as the absent sentinel, the generator's `[NotNull]` stamp, the `[Default]` extension (apply on null too), the `.Value(fallback)` door overload, and the `== null` → `IsInitialized` migration at the few sites that genuinely distinguish absent from supplied-null. Excluded — the comparison work (Stages 1–6) and surface typing (Stage 7); the `text`→`string` typing of an assignment like `channel.Mime` is Stage 7's concern, not this stage's. Required params are unchanged (no `?`, existing missing-param guard).
**Deliverables:**
- **Optional param → non-null Data.** In the Data-property emitter (`PLang.Generators/Emission/Property/Data/this.cs`), the **nullable case** (today returns `null` when `__d.IsEmpty`) instead binds `Data.Uninitialized(ParamName)`. The property reference is never null; only the resolved *value* is null. The plain-Data, `[Default]`, and required cases already return non-null — this brings the nullable case in line.
- **`Data.Uninitialized`, not `Data.Null`, for absent.** `Uninitialized` is `IsInitialized == false` — it keeps the absent-vs-supplied-null distinction inside the Data. `Null` (the present-null sentinel, `IsInitialized == true`) is what a *supplied* null resolves to.
- **`?` stays the optional signal; no `[Optional]` attribute.** Optional = `data.@this<T>?`; required = `data.@this<T>` (no `?`). Forgetting `?` makes the param required → the existing missing-param guard fires → loud failure. The signal can't be silently dropped, which is the whole reason not to introduce a forgettable `[Optional]`.
- **Generator stamps `[System.Diagnostics.CodeAnalysis.NotNull]` on every `?` Data property.** Under `<Nullable>enable</Nullable>` a `?`-typed property dereferenced (`await Mime.Value()`) trips CS8602, even though the getter now guarantees non-null. `[NotNull]` is the read-direction attribute — "the output is not null even if the type allows it" — which is exactly true here. Keep `?`, no `!` at the call site, no project-wide warning suppression.
- **`[Default(...)]` — extend to fire on a null resolved value, not only an absent slot.** Today the `[Default]` branch builds the default Data only when `__d.IsEmpty`. Extend it so a *supplied-but-null* value (`mime: %unsetVar%`) also falls to the default — matching the old `?? "text/plain"` (absent **or** null → default). Static literals only (attribute args are compile-time constants); the generator lifts the literal into the typed Data.
- **`.Value(fallback)` door overload — for runtime/computed defaults.** `[Default]` can't express `Context.Actor` or `TimeSpan.FromSeconds(30)`. Add the overload on the door: `ValueTask<object?> Value(object? fallback)` on base `Data`, `ValueTask<T?> Value(T fallback)` on `Data<T>` — returns the resolved value, or `fallback` when the value is null (absent or present-null). Sync-completing when in memory, like the no-arg door.
- **`== null` → `IsInitialized` at the sites that distinguish absent from null.** Most `if (Param == null)` collapse into `.Value(fallback)` / `[Default]` and disappear; the rare handler that must tell "not provided" from "provided null" reads `!Param.IsInitialized`.
**Dependencies:** Stage 2 — the `.Value()` door and the lazy `GetParameter<T>` param model; the `.Value(fallback)` overload extends that door. Independent of Stages 3–7; can land any time after 2.

## Design

**The problem this removes.** The async `.Value()` migration (Stage 2) turned `Mime?.Value ?? "text/plain"` into `(Mime == null ? null : await Mime.Value()) ?? "text/plain"` — two nulls in one expression: `Mime == null` (the slot wasn't supplied) and `?? default` (it resolved to nothing). The first is redundant: Data already models "absent" via `IsInitialized` (`Uninitialized`/`NotFound` vs the present-null sentinel). A nullable C# reference is a *second* encoding of the same fact — OBP smell #6. Collapse it (an optional param always resolves to a non-null Data) and the `== null` guard is gone; fold the default into the read and the `?? default` is gone too.

**Three states, none of them a null reference.** Not provided → `Data.Uninitialized(name)`, `IsInitialized == false`, value null. Provided as null → the `null.@this` sentinel, `IsInitialized == true`, value null. Provided with a value → normal Data. So `Param == null` is now *always false* — it's a real object every time. That is what makes the `[NotNull]` stamp honest, and why "was it provided?" is `IsInitialized`, never `== null`.

**Why `?` stays and there is no `[Optional]`.** The `?` is the idiomatic optional marker — it sits in the type where the eye lands, hard to skip. Its failure mode if forgotten is loud: the param becomes required and the missing-param guard fires the first time a step omits it. An `[Optional]` attribute has the *same* silent-required failure mode if forgotten but is easier to overlook, so it buys nothing and costs a forgettable annotation. Required stays the bare form (no `?`); optional is `?`.

**Why `[NotNull]`, not a warning suppression.** You can't scope `NoWarn`/`#pragma` to a type — killing CS8602 that way blinds you to genuinely-null Data everywhere else. `[NotNull]` is the surgical, per-member tool, and the *generator* owns the non-null guarantee, so the generator stamps it — not the handler author, so it can't be forgotten. Verify-API note: confirm `[NotNull]` on the **implementing** part of a partial property is honored by Roslyn for read-site flow analysis (attributes union across partial parts, so it should be) — one short compile check before the migration leans on it. This is the one assumption to test first.

**`[Default]` vs `.Value(fallback)` — two homes for the default, by kind.** Static literal (`"text/plain"`, `4096`, `"PT30S"`) → `[Default]`: declared once on the parameter, visible to the builder/LLM, and the generator bakes the default-carrying Data. A static default at the call site instead would be OBP smell #5 — the same fallback repeated at every read, free to diverge. Runtime/computed (`Context.Actor`, `TimeSpan.FromSeconds(30)`, cross-param) → `.Value(fallback)`: a literal can't express it, the call site can. Both apply one rule: absent **or** null → default.

**Call sites, before/after** (`app/module/channel/set.cs`). Before (mechanical Stage-2 migration):

```csharp
Mime  = (Mime == null ? null : await Mime.Value()) ?? "text/plain",
Actor = Actor?.Value ?? Context.Actor,
```

After:

```csharp
[Default("text/plain")] public partial data.@this<text>? Mime { get; init; }   // generator stamps [NotNull]
...
Mime  = await Mime.Value(),                // static default baked in
Actor = await Actor.Value(Context.Actor),  // runtime default at the call site
```

No `== null`, no `!`, no trailing `??`. (The `text` value `await Mime.Value()` returns still meets `channel.Mime` as a `string` via the implicit operator until Stage 7 retypes `channel.Mime` — that crossing is Stage 7, not this stage.)

## You own this (coder)

Shapes, names, and the generated-getter wording are suggestions. Non-negotiable: an optional param never resolves to a C# null reference — always a Data, `Uninitialized` when absent; the **generator** (not the handler) stamps `[NotNull]`; `[Default]` and `.Value(fallback)` both fall back on absent-**or**-null. Yours: the exact overload signatures, how a string literal lifts into `Data<text>` for `[Default]`, and the getter emission. If `[NotNull]` on a partial property turns out not to be honored by the compiler, stop and flag it before migrating call sites — everything else rides on it.
