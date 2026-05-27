# `number` — typed numeric category for PLang

**Branch:** `number-type` (off `typed-action-returns`)
**Status:** plan written; stages not yet carved.

## Why

Two LLM-scope drift sources survived `typed-action-returns`:

1. **`math.*` returns `Data<object>`.** The actual numeric kind depends on arg types (`int + int = int`, `int + decimal = decimal`). Today `math.add` widens through `MathHelper.ToDouble` and tries to `PreserveType` post-hoc — a scattered, fragile home for kind preservation. The compile-LLM sees `%b%(object)` after `math.add`, honest about today's system but useless for the next step's scope snapshot.
2. **Decimal-point literals get tagged `object`.** `set %z% = 3.14` compiles to `Type=object` because the LLM has no clear rule for decimal-point literals.

Both bugs trace to one missing concept: PLang has no umbrella type for "numeric." Concrete kinds (`int`, `decimal`, `double`, ...) are the floor; nothing sits above them. This branch introduces `number` as a real C# class — the umbrella for categorically-polymorphic numerics. Parallel to `path` but lighter (no variants needed). Once `number` exists:

- `math.add`'s signature stabilizes at `Task<Data<number>>`. LLM-scope shows `%b%(number)` consistently.
- String→numeric coercion has one home (`number.Parse`); `MathHelper.ToDouble` and its `PreserveType` siblings collapse.
- `Data<number>` rides the existing `Data<T>` plumbing — no source-generator special-casing.

## The decision

Three architectural calls, settled in conversation with Ingi before this plan was written:

1. **`number` spans all numeric kinds, not just decimal.** PLang is a general-purpose language; the architect doesn't get to pick "currency-first" or "science-first." The umbrella carries `int`, `long`, `decimal`, `double` (and `float` as a label widened to double internally) with proper promotion. Decimal-only storage was considered and rejected — it would break IEEE-754 semantics (NaN, Infinity, scientific notation, `Math.Sin`).

2. **Storage is a tagged union with explicit slots — not `object Value`, not `readonly struct`.** Builder pitched a boxed wrapper (`object Value` + `Kind` enum). That double-allocates: once for the class header, once for the boxed primitive. The C#-clean shape is a `sealed class @this` carrying `long _i; decimal _d; double _f; NumberKind _kind` — exactly one slot is meaningful per `Kind`. Construction from a primitive doesn't box; arithmetic reaches the right slot directly; the wasted bytes per instance are negligible at PLang's scale. A `readonly struct` was on the table but breaks the `@this` class convention without enough payoff. Details in [plan/storage.md](plan/storage.md).

3. **Arithmetic policy is developer-configurable, not architect-imposed.** Two axes — `Overflow` (Promote | Throw) and `Precision` (Double | Decimal) — × three scopes — app / goal / step. Defaults are lenient: "PLang sorts it." Strict mode is one step away for financial / scientific code that can't tolerate the lossy path. C# operators on `number` itself are policy-free (always lenient); the math action handlers consult settings and call a policy-aware overload. Details in [plan/policy.md](plan/policy.md).

Implicit-IN operators from concrete numerics (`number n = 5;`), explicit-OUT (`(decimal)n` may throw on narrowing). `Parse(string)` picks the narrowest kind that fits losslessly — the single home for string → numeric coercion. `IBooleanResolvable` returns `Task.FromResult(value != 0)` — sync logic under an async interface.

## Cross-cutting decisions

- **`number` is the umbrella; `int` / `decimal` / `double` stay as user-visible primitives.** A handler that genuinely always returns one concrete kind (`list.count` → `int`) keeps its concrete return type. `number` is only for the categorically-polymorphic case — math.add, list.sum, list.avg, etc.
- **No silent precision loss inside `number`.** Decimal stored as decimal, double stored as double — the tagged union preserves kind through arithmetic. Loss only happens at mixed-kind promotion, and even then it's policy-controlled.
- **App always carries an `environment.number` config**, constructed at App startup with lenient defaults. Goal carries an overlay only if a step writes to it (lazy, mirrors the existing `Goal.Events` pattern).
- **Action handlers gain optional `Overflow` / `Precision` parameters.** The LLM sees them as ordinary enum-valued action params; the resolver does the precedence walk `step → goal → app → built-in`.
- **C# operators on `number` never reach for Context.** Operators are static — pulling Context from one of two operands is ambiguous and reading ambient state in arithmetic is a debugging nightmare. Policy lives in the handler, not the type.

## Stages (to be carved next)

1. **`app/types/number/` — the class itself.** `@this` with tagged-union storage, `NumberKind` enum, `Parse` / `TryParse` / `Resolve`, implicit-IN / explicit-OUT operators, arithmetic (`Add` / `Subtract` / `Multiply` / `Divide` / `Mod` / `Power`, each with policy overload), `IBooleanResolvable`, value-based `Equals` / `GetHashCode`, `ToString`. Pure value-level math, no policy reach.
2. **`app/environment/number/` — the settings home.** `environment.number.@this` config object (typed properties for the two axes). `App.Environment.Number` always present, constructed lenient. `Goal.Environment` lazy overlay mirroring `Events`. `NumberPolicy` struct + static `Resolve(stepOverflow, stepPrecision, goalScope, appScope)`. `environment.set` action surface for `- set environment.number.overflow = throw, scope: goal`.
3. **`math.*` retype — the canary, then the sweep.** `math.add` first: `Run()` returns `Task<Data<number>>`, gains optional `Overflow` / `Precision` parameters, reads policy via the resolver. Verify the trace on the scratch goal in the recipe below. Then sweep `subtract` / `multiply` / `divide` / `modulo` / `abs` / `ceiling` / `floor` / `round` / `max` / `min` / `power` / `sqrt` / `random` (where applicable), and `list.sum` / `avg` / `min` / `max`. `MathHelper.ToDouble` and `MathHelper.PreserveType` get deleted at the end of the sweep.
4. **Primitives + catalog.** Register `"number"` in the `app.types` `Primitives` table mapping to `app.types.number.@this`. Add the `static Resolve(string, context)` factory so the catalog renders `number` as a scalar with shape `string`. Confirm PLNG001 accepts `Data<number>` without changes to the source generator. Confirm assignability: `Data<number>` slot accepts any concrete numeric (Variable resolution at `Data.As<T>` time), and `Data<int>` / `Data<decimal>` slots accept a `Data<number>` that narrows checked at access.
5. **Compile.llm decimal-literal rule.** Independent small fix — lands **first** as a precursor before any of the above. Immediately moves `%z%(object)` → `%z%(decimal)` on the existing scope-snapshot trace before the `number` class exists. Rule shape: decimal-point literals → `decimal`; scientific notation or "as double" / "floating point" hints → `double`. After Stage 1 ships, the rule updates to → `number`.

Order: **5 first** (no class needed, immediate signal that the LLM-rule path works), then **1 → 2 → 3 → 4**. The class precedes math because math depends on it; environment precedes math because the resolver consumes it; primitives last because it's a one-line registration once the class settles.

## Verification recipe

Reproduce the gap before the work, confirm the fix after each milestone:

```bash
mkdir -p /tmp/numtest && cd /tmp/numtest
cat > Start.goal <<'EOF'
Start
- set %x% = 1
- set %z% = 3.14
- set %b% = %x% + %z%
- write out %b%
EOF

/workspace/plang/PlangConsole/bin/Debug/net10.0/plang \
    '--app={"create":true}' \
    '--build={"files":["Start.goal"],"cache":false}'

# Inspect the compile prompt for the %b% step (step index 2):
python3 -c "
import json, glob
trace = sorted(glob.glob('.build/traces/*/Start.json'))[-1]
d = json.load(open(trace))
print(d['stepPasses'][2]['value']['user'])
" | grep -A2 'Variables in scope'
```

- **Before this branch:** `%x%(int), %z%(object)` — `%z%` is wrong.
- **After Stage 5 (Compile.llm literal rule):** `%x%(int), %z%(decimal)`.
- **After Stages 1–4:** the `write out %b%` step sees `%b%(number)` instead of `%b%(object)`.

## Out of scope

- **`text` category** (`string ∪ tstring`). Same shape as `number`, natural sibling, follow-up branch.
- **Scope-aware `Build()` narrowing** (`int + int → int` at build time via reading the validator scope). Explicitly rejected — unstable prompts, promotion-table drift, doesn't help the literal-tagging case. Not blocked; can layer on later if real value emerges.
- **Backfilling concrete `Data<int>` / `Data<decimal>` returns to `Data<number>`.** Stays concrete. `number` is only for genuine polymorphism.
- **Block-scoped policy** (Python's `with localcontext()`). PLang doesn't have blocks; step scope is the closest analogue and is covered by per-call action parameters.
- **Culture-aware formatting** (commas vs periods, currency symbols). `app.Culture` handles this in a follow-up; `number.ToString` uses invariant culture for now.

## What's already verified

- `typed-action-returns` Stage 0 + Stage 4 infrastructure works end-to-end. The remaining `%z%(object)` and `%b%(object)` misses are exactly the two cases this branch targets.
- `app/types/path/` precedent confirms the umbrella-class shape. `path.@this` is abstract because file/http variants have genuinely different storage and I/O dispatch; `number` is sealed because numeric kinds share storage and arithmetic — no variants needed.
- `app/modules/environment/` already exists (currently only `run.cs`, the renamed `app.run`). Adding `environment.set` and the `number` config child is a clean extension.
- `Goal.@this` has a lazy `Events` overlay pattern (private backing field, `??=` getter) that `Environment` can mirror line-for-line.
- PLNG001 accepts arbitrary `Data<T>` shapes today; `Data<number>` rides the existing rails with no generator change.
- `MathHelper.PreserveType` (PLang/app/modules/math/MathHelper.cs) is the current scattered home for kind preservation — its existence empirically confirms the need and gives a clean deletion target at Stage 3 end.
