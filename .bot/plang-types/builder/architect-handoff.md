# Architect handoff: introduce `number` category type (C# class + PLang primitive)

**From:** builder
**Branch:** `number-type` (off `typed-action-returns`)
**Context source:** verified empirically on `typed-action-returns` against `/tmp/typetest/Start.goal` — trace at `/tmp/typetest/.build/traces/639154869898711577_83674ada/Start.json`.

## The problem

Two related drift sources still show up in the "Variables in scope" snapshot the compile-LLM sees, after `typed-action-returns` shipped:

**1. `math.*` (and other numeric producers) return `Data<object>`**, because the actual numeric kind depends on arg types — `int+int=int`, `int+decimal=decimal`. Today:

```
Step 5 scope: %x%(int), %y%(string), %z%(object), %rows%(csv), %b%(object)
                                                                 ^^^^^^^^^^
                                                                 math.add(%x%, %z%) → object
```

`%b%(object)` is honest about today's type system but uninformative — the LLM can't tell `%b%` supports arithmetic, comparison, or `ToString()` without guessing. Stage 4's per-action `Build()` can't fix this without scope-aware inference (it would need to read `%x%`'s and `%z%`'s resolved types from the validator's running scope), which isn't in the current `Build()` contract.

**2. Literal `3.14` is tagged `object` by the LLM.** Empirically: `set %z% = 3.14` compiles to `variable.set Name=%z%, Value=3.14, type=object`. The LLM has no clear rule for decimal-point literals; today's catalog doesn't make `decimal` the obvious choice. This is a smaller fix — Compile.llm rule — but rides alongside #1.

## What we want

Introduce a `number` **category type** — a real C# class at `app/types/number/this.cs` that:

- Holds a boxed numeric (int / long / decimal / double / float) plus a `Kind` discriminator.
- Has implicit-in operators from concrete numerics (frictionless construction at handler sites: `Data<number>.Ok(5.14m)`).
- Has explicit-out operators to concrete numerics (caller picks narrowing; may throw at the boundary).
- Has a single `Parse(string)` / `TryParse(string, out)` — the one home for `"123" → 123`, `"3.14" → 3.14m`. Picks narrowest CLR type.
- Has arithmetic operators (`+`, `-`, `*`, `/`) that promote per `int < long < decimal < double` rules.
- Implements `IBooleanResolvable` (zero is false).
- Lives in the PLang `Primitives` table as a type whose assignability accepts any concrete numeric (`int`/`long`/`decimal`/`double`/`float`), and which any concrete numeric can substitute into.

This is parallel to `path` — `path` is the OBP-clean wrapper for "any path-like" with `FilePath`/`HttpPath` variants. `number` is the same shape: one C# class as the umbrella, no variants needed (Kind-discriminator over boxed Value is sufficient — unlike paths, numeric kinds don't have meaningfully different storage shapes).

Once it exists, the `math.*` family (and `list.sum`/`avg`/`min`/`max`) retype `Run()` from `Task<Data<object>>` to `Task<Data<number>>`. The catalog advertises `→ returns number`. The LLM emits `Type="number"` on the terminal `variable.set`. Downstream steps see `%b%(number)` in scope — honest, useful, stable.

## Why "category type" and not "scope-aware Build()"

We considered extending `Build()`'s contract to receive the validator's running scope, so `math.add.Build()` could read `%x%`/`%z%`'s declared types from prior steps and narrow the return to `int` or `decimal` per the promotion table. Reasons not to:

- **Unstable prompts.** Same step compiles to different return types depending on call-site arg types → harder LLM mental model, less cacheable.
- **Promotion-table maintenance.** Every numeric op grows a table; drift hazard.
- **Doesn't help the literal-tagging case** (`%z%(object)` from `3.14`).
- **`number` is what users actually mean.** A PLang dev writing `%b% = %x% + %z%` thinks "b is a number," not "b is int because x and z happen to be int." The category captures intent.

`number` is the floor of what's promised; runtime `.Type` on the Data can still narrow to a concrete (`Data<number>` carrying a decimal-kind value can satisfy a `Data<decimal>` slot — checked narrowing at access). If a future refinement *does* want scope-aware Build() narrowing on top, it adds in cleanly without re-litigating the type system.

## Why a real C# class, not a PLang-only label

Original sketch considered keeping `number` purely as a PLang-side primitive name with C# staying on `Data<object>` for math. Ingi pushed back: PLang's whole direction is "strongly typed end-to-end, even at the C# level." Making `number` a real class buys:

- **Strong typing in handler code.** `Data<number>` is a real generic param; PLNG001 gate works as-is.
- **Single home for string→numeric coercion.** `number.Parse` is the One Place. Today this logic is scattered (TypeMapping, individual converters, `Data.As<T>`).
- **Operator overloads on the class** — PLang-side arithmetic can lean on `+`/`-`/`*`/`/` directly rather than re-implementing in each handler.
- **Materializer entry**: `Serializers` registry gets a `number` materializer; `variable.set Type="number"` on a string Value parses at first access via the existing lazy-materialization discipline.
- **Substitutability checked at access, not at set time.** Matches `IBooleanResolvable` / lazy-params / Stage 0 Data discipline already in the codebase.

## Verification recipe (architect should fold into stages.md)

Reproduce the gap before the work, confirm the fix after:

```bash
# Scratch goal in any throwaway dir
cat > Start.goal <<'EOF'
Start
- set %x% = 1
- set %z% = 3.14
- set %b% = %x% + %z%
- write out %b%
EOF

# Build with a clean trace
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang \
    '--app={"create":true}' \
    '--build={"files":["Start.goal"],"cache":false}'

# Inspect the compile prompt the LLM saw for the %b% step
python3 -c "
import json
d = json.load(open('.build/traces/<id>/Start.json'))
print(d['stepPasses'][2]['value']['user'])  # step index 2 = 'set %b% = %x% + %z%'
" | grep -A2 'Variables in scope'
```

**Before this branch:** `%x%(int), %z%(object)` — `%z%` is wrong.
**After Compile.llm decimal rule:** `%x%(int), %z%(decimal)`.
**After number-type sweep:** the next step (`write out %b%`) sees `%b%(number)` instead of `%b%(object)`.

## Scope sketch (architect to refine)

A rough cut, not load-bearing — architect owns the staging:

1. **`app/types/number/this.cs`** — the class. Parse, implicit-in, explicit-out, arithmetic, `IBooleanResolvable`, `ToString`.
2. **`Primitives` entry** — register `number` with the assignability rules (accepts any concrete numeric kind both directions).
3. **`Serializers` registration** — a `number` materializer for lazy-set parsing.
4. **PLNG001 acceptance** — confirm `Data<number>` passes the source-generator gate without special-casing.
5. **`math.add` first** as the canary — retype `Run()` to `Task<Data<number>>`, build the scratch goal above, eyeball the trace.
6. **Sweep `math.*` family** (`subtract`, `multiply`, `divide`, `round`, `mod`, ...) and likely `list.sum` / `avg` / `min` / `max` once #5 is clean.
7. **Compile.llm decimal-literal rule** — separate small fix; can land in any stage. Rule shape: "literal numerics in `set %x% = N`: integers → `int` (or `long` if too big); decimal-point literals → `decimal` (not `object`, not `double`). `double` only if step text says 'as double' / 'floating point' / etc."

## Decisions to lock with Ingi during planning

- **Variants vs single class.** Builder's read: single class with `Kind` discriminator over boxed `Value` is the OBP-clean shape (unlike `path`, numeric kinds don't differ in storage). Worth Ingi-confirming.
- **Implicit vs explicit direction.** Implicit IN (concrete → number) for ergonomics at construction sites; explicit OUT (number → concrete) so narrowing is intentional. Worth Ingi-confirming.
- **`text` category** as a follow-up. Same pattern for `string`/`tstring`. Out of scope for this branch; flag in plan as the natural sibling work.
- **Backwards-compat with concrete return types**: `list.count` returns `int`, not `number` — anything that's *always* one concrete kind keeps its concrete return type. `number` is for *categorically polymorphic* numerics only.

## Out of scope

- Scope-aware `Build()` narrowing (`int+int → int`). Not blocked by this branch; can be added cleanly later if value materializes.
- The `text` category (`string` ∪ `tstring`).
- Backfilling existing concrete `Data<int>` / `Data<decimal>` returns to `Data<number>`. They stay concrete — `number` is only for the genuinely-polymorphic numeric returns.

## What's already verified

- `typed-action-returns` Stage 0 + Stage 4 infrastructure works end-to-end. Empirical test on `/tmp/typetest/Start.goal`:
  - `%x%(int)`, `%y%(string)`, `%rows%(csv)` all propagate correctly to next-step scope.
  - The remaining `%z%(object)` and `%b%(object)` misses are exactly the two cases this branch targets.
- Clean rebuild of `PlangConsole` is green on `typed-action-returns` HEAD (`bc794aea2`).
- PLNG001 gate accepts arbitrary `Data<T>` shapes; adding `Data<number>` does not require generator changes.
