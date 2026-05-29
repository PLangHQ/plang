# Review from Opus 4.8 on storage.md + policy.md — my counter-arguments

**Source.** Ingi shared the current `plan/storage.md` and `plan/policy.md` with Opus 4.8 on 2026-05-28. The review came back with six numbered critiques plus three smaller notes. This file walks each one and takes a position: concede, push back, or partially agree with a delta.

**Status (2026-05-29):** Ingi has reviewed and decided. Outcomes are recorded in "Decisions landed (2026-05-29)" at the bottom; the ones marked "landed" are now reflected in `storage.md`. The body below preserves the original argument-by-argument analysis as the reasoning record.

The thumbnail: **four of six I concede outright** (equality, throws-vs-Data, mutable Context, integer-divide), **one I push back on partially** (curated boundary — the framing-as-decision is rhetorical; I'd already implicitly committed), **one I partially agree on** (literal-shape semantics — value-dependent typing is deliberate but worth documenting loudly). Two of 4.8's collateral notes are factually wrong on current state (the policy concern was already addressed by my rewrite to `app.config`; "no App root anymore" is just incorrect).

---

## 1. Curated arithmetic-scalar set vs. open .NET numeric tower

**4.8's read.** Three slots cover the common tower but not the full one. `ulong` doesn't fit `long` (overflows by ~2×); has to go into `_d` (decimal) "which is messy and leaks into every switch." `Int128/UInt128/BigInteger` fit nothing. "unit" is not a number — it's a `quantity` type composing `number + unit`. So pick: curated or open? Commit, write the boundary down.

**My position: mostly concede, two corrections.**

**Correction 1: ulong-as-Decimal isn't the leak 4.8 thinks.** The "messy" claim assumes I'd carry a `ulong` Kind tag through every switch. I don't. In my coverage table `ulong` collapses to `Kind = Decimal`, storage in `_d`. Arithmetic dispatches on `Kind` and never sees `ulong` again — decimal arithmetic is exact for integers in decimal's range, so the round-trip is lossless. The leak only exists if the LLM-facing catalog lets devs write `ulong` and round-trip the *label*. It doesn't (the catalog hides narrow-int / unsigned per the storage table). So this part holds.

**Correction 2: I had already implicitly committed to curated.** Storage.md's "Bigger than decimal — BigInteger" subsection draws the boundary at `decimal.MaxValue` and defers BigInteger to a separate branch with a real consumer. That's a curated stance. What 4.8 is right about is that the *stance* is implicit, not loudly stated. The reader could plausibly read "BigInteger slot reserved for the day decimal stops being enough" as "we'll add it later by default."

**Where 4.8 is right.** "Unit" is not a number — a `quantity` type that composes `number + unit` (e.g., `5kg`, `30m/s`) is a sibling category that *uses* `number` as one of its fields. Don't let `5kg` into `number.Parse` or we're solving units-of-measure ambiguity inside arithmetic. Concede outright; quantity belongs in the [plan/types.md](types.md) follow-up list, parallel to image/code/document.

**Push-back.** The framing as a binary decision — "curated vs. open .NET numeric tower" — is rhetorical. Nobody is proposing to surface `Complex`, `Half`, `nint`, `nuint` to the LLM as PLang-facing types. The real decision is **curated + (BigInteger now or later)** and I want later. 4.8's case for "now" is the crypto domain (Ed25519, HMAC, hashes). 4.8 then correctly notes that **those values aren't arithmetic scalars** — they're `byte[]` carrying ed25519 / HMAC, not numbers PLang devs do `+` on. So the crypto case doesn't pull BigInteger into `number`. Keep BigInteger deferred until a real arithmetic consumer (large-integer math, perfect-precision financial summing past 28 digits) surfaces.

**Doc delta I'd land.** Add a "What's a `number`, what isn't" section near the top of storage.md, before the storage layout. State:
- IN: int / long / decimal / double, plus narrow-int / unsigned variants collapsing into those slots.
- OUT: `Int128` / `UInt128` (don't fit; no real consumer); `BigInteger` (slot reserved, deferred); complex, half, nint, nuint, fixed-point (not in PLang's prose-driven vocabulary); units of measure (separate `quantity` type, future branch).
- Why curated: PLang's surface is what the LLM picks from; every PLang-facing type needs an action surface that consumes it. Without consumers, types are noise.

---

## 2. Equality is not an equivalence relation

**4.8's read.** NaN breaks reflexivity. Cross-kind via double promotion breaks transitivity. Worked example:
- `From(0.1)` == `From(0.1m)` — promote to double, both land on the same nearest double.
- `From(0.1)` == `From(0.1000000000000000055m)` — same nearest double.
- `From(0.1m)` != `From(0.1000000000000000055m)` — both Decimal kind, exact `_d` compare, unequal.

So `a==b`, `a==c`, `b!=c`. No `GetHashCode` rescues a non-transitive `Equals`. HashSet/Dictionary semantics are silently broken — `Add` collapses distinct values, `Contains` misses transitively-equal ones. I labelled this "mechanical Stage 1 cleanup" in the doc; it isn't.

**My position: full concede. This is the real landmine in the storage file.** Bigger than I treated it.

The fix is **exact-only cross-kind equality**. `decimal(0.1m) == double(0.1d)` is *false*: 0.1 has no exact double representation, so they're not the same number under any precision-respecting view. Same for 0.1000000000000000055m vs 0.1d. All three become inequal. Transitivity restored.

Implementation: cross-kind equality requires one of the operands to convert losslessly to the other's slot. Decimal that exactly equals its `(decimal)(double)d` round-trip → may equal that double; otherwise not. Same for Int↔Decimal (always lossless since int range ⊂ decimal range). Same for Int↔Double (lossless when the int has ≤53 bits — `From(2^53)` == `From(2^53d)` true; `From(2^53 + 1)` == `From(2^53 + 1d)` false because the double rounds).

**The cost we accept:** `From(0.1m) != From(0.1)` will surprise developers who *think of them as the same number*. But that surprise is the truth — 0.1m IS NOT 0.1d in any honest precision view. Surfacing it as inequality is honest; silently producing wrong answers in HashSet/Dictionary is not.

NaN-as-falsy in `IBooleanResolvable` is separate from NaN-equality and stays. NaN-in-HashSet means the bearer can never be looked up — same as `double` itself in C#. Document loudly, don't try to "fix" IEEE-754.

**Doc delta I'd land.** Rewrite the value-equality section. Three subsections:
- **Within-kind:** exact bit-level (`_i == _i`, `_d == _d`, `_f.Equals(_f)` which is NaN-aware).
- **Cross-kind:** lossless conversion gate. `a == b` iff converting one to the other's kind produces a value equal to that other under within-kind rules. Examples in a table.
- **NaN and hashed collections:** NaN can't be looked up in HashSet/Dictionary. Same as `double` in C#. If you need "same mathematical number with fuzz," call `number.MathEquals(other, tolerance)` — a separate API, not the equality identity.

`GetHashCode` follows the exact rules: same hash iff exact-equal. Mechanical once equality is locked.

---

## 3. Int / Int → integer divide, and one promotion table for all six ops is wrong

**4.8's read.** `7 / 2` resolves to Int kind → integer divide → `3`. Footgun for non-programmers; Python deliberately split `/` and `//` to kill exactly this. Divide and Power want different promotion rules than Add/Sub/Mul.

**My position: concede.** Single-table-for-all-ops was an oversimplification. Per-operator rules.

Proposal:

| Op | Result-kind rule |
|---|---|
| Add / Sub / Mul / Mod | Today's table — kind preserved per operand kinds; integers stay integer. |
| **Divide** | Always at-least Decimal under default Precision. `Int/Int → Decimal` so `7/2 → 3.5m`. Under Precision=Double, `Int/Int → Double`. |
| **Power** | Depends on exponent. `Int^non-negative-Int → Long` (overflow per policy). `Int^negative-Int → Decimal`. `anything^fractional → Double` (IEEE). |
| Integer-divide | Separate `math.intdiv` action. `7 intdiv 2 → 3`. Devs who want the truncating behavior name it. |

C# operator overloads (`/`, `^`) follow the new rules. PLang devs writing `set %x% = 7 / 2` get `3.5` by default — matches their natural expectation. The integer-divide semantic is opt-in via a named action, the way Python's `//` is opt-in via a named operator.

**Doc delta I'd land.** Split the arithmetic section in storage.md. Per-op promotion shape table. New section for `math.intdiv` action contract.

---

## 4. Throws vs. Data — two error models in one stack

**4.8's read.** `ToInt32` / `ToDecimal` / `Resolve` / integer-divide / decimal-overflow all raise CLR exceptions. The handler I showed (`return Data<number>.Ok(...)`) has no failure path. An `OverflowException` from `(int)%n%` escapes the Data pipeline. Runtime2's contract is `Data.Success/Data.Error`, not try/catch. Two error models in one stack.

**My position: concede, with a clean split.** Real architectural mismatch. The fix isn't "everything returns Data" — C# operators (`+`, `*`, etc.) can't, by signature. The split is by surface:

- **C# operators (`+`, `-`, `*`, `/`, `%`, `^`) throw on overflow / div-by-zero.** Matches `decimal + decimal` C# convention exactly. In-process C# code uses operators and owns the exception path the way every other CLR numeric does.
- **Policy-aware named methods return `Data<number>`.** `number.Add(a, b, policy) → Data<number>`. Catches `OverflowException` internally, converts to `Data<number>.Fail("MathOverflow", details)`. Same for Divide / Power / etc.
- **`Resolve(string, context)` throws; `TryResolve(string, context) → Data<number>` doesn't.** Pair of methods, the throwing one for in-C# use where input is known valid, the Data-returning one for handler / parse-from-external-source paths.
- **`ToInt32` and siblings throw; `TryToInt32 → Data<int>` doesn't.** Same pattern.

Math action handlers call the Data-returning methods. The exception path exists for C#-side internal use only and never reaches a `Data.Run()` boundary. Two surfaces, one error model at the handler edge.

This also lets the Lifecycle/Events `[OnError]` bindings see arithmetic failures as `Data.Error("MathOverflow")` — surface-uniform with `Data.Error("ReadFailed")` from file actions. The handler can recover or rebind without try/catch.

**Doc delta I'd land.** Section in storage.md: "Error model — throws at C# boundary, Data at handler boundary." Spell out the surface pairs (operator+method, Resolve+TryResolve, cast+TryCast). Section in policy.md updates: the math handler example uses `number.Add(...)` (Data-returning), not the operator.

---

## 5. Value-dependent typing — `5` vs `5.0` vs `5e0` silently flip kind

**4.8's read.** `Parse("5")` → Int; `Parse("5.0")` → Decimal; `Parse("5e0")` → Double — same number, three kinds, three different downstream behaviors. A user adding `.0` or writing `1e2` instead of `100` silently flips the type track. Document loudly; consider whether literal form or set context wins.

**My position: partial agree.** The value-dependent typing is deliberate (Python, JavaScript, most dynamic languages do it) and the right shape for a language whose primary picker (the LLM) reads developer prose intent. But "document loudly" and "be explicit about which layer wins" are both valid asks.

Two layers cooperate today, but the doc doesn't make this explicit:

1. **Compile-time LLM rule.** When the planner reads `- set %x% = 5.0`, it stamps `%x%(decimal)` in the scope snapshot before runtime ever parses the literal. The LLM reads developer intent from prose context ("a price", "a coordinate", "an exponent") and types accordingly. Was Stage 5 in the original number-only plan.
2. **Runtime Parse.** When `number.Parse("5.0")` runs on a string that *didn't* come through LLM-stamping (file read, http body, user input from the terminal), it picks narrowest-losslessly. `5.0` → Decimal because the literal form carries information the LLM doesn't get to set retroactively.

Same outcome on the developer-written case; different motivation. Both correct. The doc should surface this.

**Doc delta I'd land.** New section "literal-shape semantics" in storage.md:

| Literal | Parse result | Reason |
|---|---|---|
| `5` | Int | No decimal point, no exponent, fits int. |
| `5L` | Long | Explicit suffix. |
| `5m` | Decimal | Explicit suffix. |
| `5.0` | Decimal | Decimal point → developer signaled "this matters precisely." |
| `5e0` | Double | Exponent → developer signaled "scientific notation, IEEE territory." |
| `5.0d` | Double | Explicit suffix. |
| `5.0f` | Float | Explicit suffix, widens to Double internally, label preserved. |

Cross-reference to the compile.llm rule (formerly Stage 5) for the developer-intent layer. Note that file/http-source strings flow through Parse only; LLM stamping doesn't apply post-hoc.

---

## 6. Mutable `Context` on `number` — not a pure value

**4.8's read.** The class declares `public actor.context.@this? Context { get; set; }` with public setter and `IContext` implementation. `From(5)` has `Context = null`; `Resolve(s, ctx)` attaches one — inconsistent. A `number` cached or parked in MemoryStack across requests carries stale per-request state. Violates [OBP Rule #4 in the formal doc](../../../Documentation/v0.2/object_pattern_formal.md) ("per-request state is a parameter, never cached on shared objects"). The class-not-struct + `IContext` decision was made for surface uniformity with `path.Resolve` — but `path` genuinely needs the filesystem; a scalar doesn't.

**My position: full concede.** Mutable Context on a value-typed numeric is wrong. I bolted it on for cosmetic factory-shape uniformity with `path.Resolve(raw, context)`. The cost wasn't visible to me at write-time but is real:

- **Cache invariant broken.** `MemoryStack[%x%]` returning a number with stale Context-from-last-request is a silent cross-request leak.
- **No real use site.** Number doesn't need Context for arithmetic, equality, truthiness, or serialization. The factory signature can accept Context and ignore it (registry lookup during construction, then discard).
- **Class-vs-struct cleaner.** Without Context, the case for `readonly record struct` strengthens (see "What 4.8 didn't address" below).

The fix is mechanical:
- Drop `public Context { get; set; }` from `number.@this`.
- Drop `IContext` from the interface list.
- `Resolve(raw, context)` keeps the parameter for signature uniformity with other types' Resolve, but doesn't store it.
- `From(5)` and all internal factories never see Context.

**Doc delta I'd land.** Storage.md class header: drop Context and IContext. Add a one-line note: "Context appears in `Resolve(raw, context)` for factory-shape consistency with other types but is never stored. `number` is a pure value." Cross-reference [object_pattern_formal.md Rule #4](../../../Documentation/v0.2/object_pattern_formal.md).

---

## Smaller notes 4.8 flagged

- **`try/catch` inside `Equals` for cross-kind decimal-vs-double-NaN.** Concede. Replace with explicit `IsFinite` + range guard. Hot path, no exceptions.
- **`Float` Kind missing from the promotion table.** Mostly concede. Float widens to Double on entry per the storage model but the promotion table doesn't show that normalization. Two ways: (a) drop `Float` from the `NumberKind` enum entirely — it's always-already-Double internally; (b) keep Float in the enum, add row/col with explicit "normalizes to Double pre-promotion." Cleaner is (a): drop Float Kind, lose the round-trip-label fidelity but gain table consistency. I'd punt this to a follow-up after the bigger landmines (equality, error model) settle.
- **"No boxing" oversells.** Concede. The phrase referred to "no double-allocation when wrapping a primitive in a class+box," which the design genuinely avoids. But the heap allocation per `@this` instance is still there. Reword to "primitives ride in slots, not boxed objects, but each `number` instance is one heap object." (Or move to struct — see below.)

---

## What 4.8 didn't address that I think it should have

### `readonly record struct number` revisited

Points 2 (equality) and 6 (pure value) together make the class form weak. The `@this` convention is built for *navigable owner types* — Goal, Step, App.Config — where the navigability of `parent.child` is the whole point. For a *pure value* with value-equality and no Context, `readonly record struct number(NumberKind Kind, long I, decimal D, double F)` would:

- Give exact value-equality automatically (record struct).
- Eliminate heap allocation (struct).
- Match what `int` / `decimal` / `double` themselves are.
- Mean breaking the @this-everywhere convention for one type.

The convention exists for a reason (LLM navigability, reading code like English). The argument for breaking it here: `number` isn't *navigated*, it's *computed on*. `app.types.number.@this` doesn't read better than `app.types.number` — there's no child reachable from a number to make the dot-chain pay off. The struct form costs nothing in OBP terms because there's no navigation to lose.

I'd want to discuss this 10 minutes with Ingi before deciding. It's the one structural question the review surfaced that's bigger than the others.

### Policy critique was already addressed

4.8's hint at the end: "the placement issue (goal-scope env on a shared `Goal` violating OBP #4, and there's no `App` root anymore) is the one piece there that doesn't show up in this file."

The first half — goal-scope env on shared Goal — is already addressed in the current `plan/policy.md`. The rewrite from earlier today switched to `app.config` (the existing scope mechanism: per-context `ConfigScope` with parent-walking), so no Goal-private overlay exists anymore. 4.8 might be reading the older draft of policy.md that did have the Goal overlay; the live version doesn't.

The second half — "no `App` root anymore" — is just factually wrong on current state. `app.@this:21` still exists; `App.Goals` (line 154), `App.Config` (line 169), `WireDefaultConsoleChannels` (line 354) all hang off it. My policy proposal reaches `Context.App.Config.For<number.Config>(Context)` and that works because App is the root. Either 4.8 has a stale snapshot of the codebase or it's hallucinating a rename. Worth confirming before we treat that critique as load-bearing.

---

## What I'd land if you sign off, in real-fix-not-polish order

1. **Equality rewrite** (point 2) — exact-only cross-kind. Real bug in the wild risk; lands first.
2. **Drop Context from `number`** (point 6) — mechanical but architecturally important.
3. **Error model split** (point 4) — C# operators throw, named methods return Data, handlers use methods. Touches every math handler so this is the bigger sweep.
4. **Per-op promotion** (point 3) — Divide and Power get their own rules; `Int/Int → Decimal` under default Precision; new `math.intdiv` action.
5. **Curated boundary section** (point 1) — one section near top of storage.md. Explicit, with quantity flagged as the future sibling for units of measure.
6. **Literal-shape semantics section** (point 5) — documentation, no code change. Includes the cooperation between LLM-stamping and runtime Parse.

Smaller cleanups (try/catch, Float Kind, "no boxing" wording) fold into whichever section they touch.

**The one thing I'd want to discuss before changing anything**: the **sealed-class-vs-readonly-record-struct** question. 4.8 didn't raise it directly but the equality and Context critiques both reinforce the case. Worth a 10-minute back-and-forth before either decision is committed to the doc.

---

## Decisions landed (2026-05-29)

Ingi reviewed this file. Outcomes, and where each is now reflected:

| Point | Decision | Landed in |
|---|---|---|
| 2 — equality | **Lenient `==` by default** (`0.1 == 0.1` true regardless of kind), **`ExactEquals` opt-in** for crypto/finance/debug. Not exact-only — that's the worse default. Non-transitivity caveat documented, not "fixed." | [storage.md](storage.md) "Value equality" |
| 4 — error model | **Throws at the C# boundary, `Data` at the handler boundary.** Operators/private internals throw like any CLR numeric; module surface returns `Data.Error`. Ingi: "everything in plang returns data… private method would throw, but we should return Data error to the runtime from the module." | [storage.md](storage.md) "Error model" |
| 6 — Context | **Dropped.** No `Context` field, no `IContext`. `Resolve(raw, context)` keeps the param for signature consistency, never stores it. Ingi noted thread-safety isn't the worry; the point is a value shouldn't carry unused per-request state. | [storage.md](storage.md) "The shape" |
| struct vs class | **`readonly struct @this`** (struct named `@this`, convention intact). Ingi: "the win for less allocation is more important." **Architect correction:** verified `Data.Value` is `object` (`app/data/this.cs:86`), so a struct **boxes on store into Data** — the allocation win is real only for pure-C# arithmetic intermediates that never enter Data (reducer accumulators), and that path is partly mooted by `[code]`. Struct still stands on **value-semantics** grounds. Proceeding on that corrected basis; flip back to `sealed class` is one keyword if the corrected premise changes the call. | [storage.md](storage.md) "The shape" |
| 3 — divide/per-op promotion | **Open — not landed.** No user ruling yet. My rec (in point 3 above): `/` promotes out of integers (`7/2 → 3.5`), `^` promotes on negative/fractional exponents, `math.intdiv` for truncating. Flagged as a provisional note in storage.md Arithmetic. | pending |
| 1 — curated boundary | **Open — recommendation standing.** Curated set; BigInteger/Int128 deferred (no arithmetic consumer; crypto is `byte[]` not arithmetic-scalar); units → future `quantity` sibling. Not yet written as a storage.md section pending confirm. | pending (rec in point 1) |
| 5 — literal-shape typing | **Open — documentation only.** The `5`/`5.0`/`5e0` kind-flip is deliberate; should be surfaced loudly with the LLM-stamp vs runtime-Parse cooperation. Not yet a storage.md section. | pending (rec in point 5) |
| rational numbers | **Option B (separate `rational` sibling type), added later — not this branch.** Ingi: "we dont need to implement now, that can be added later." Recorded; nothing to build. | this file (below) + plan.md "Open questions" |

### Rational numbers — Ingi's call (Option B, deferred)

`- set %x% = 7/8` keeping the value exact as a rational is a real future want. Decision: a **separate `rational` type** under `app/types/rational/` (parallel to `number` and the future `quantity`), not a sixth `NumberKind` inside `number` — rational arithmetic has its own rules (GCD/lowest-terms on construction, denominator-overflow, no IEEE concepts) and most code never needs it, so folding it into the universal `number` would tax every `number` switch. **Not built on this branch** — it's a follow-up with a real consumer driving it, same discipline as BigInteger and the format-kind types. Captured so the type vocabulary stays forward-coherent.
