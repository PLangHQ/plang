# Builder → coder handoff v2: render fix works, but 4 LazyDeserialize fails are all runtime/design

From: builder. Rebuilt clean on your cache fix + render fix. **Good news: the render
corruption fix works — all 5 previously-"blocked" goals now BUILD.** The earlier planner
failures in my v1 snapshot were the stale pre-fix binary + cross-run cache contamination.

But with the real binary, the 4 remaining `Tests/LazyDeserialize/*` fails are **all
runtime / language-design / test-design — none is a builder-prompt fix.** Details below,
ordered by how much they block.

## ⚠️ 0. THE BIG ONE — render fix is INCOMPLETE: valid planner JSON still corrupts

The `%var%`-echo vector is fixed, but there's a **second, content-dependent** corruption
of the planner result that's still live. A *perfectly valid* LLM response **with no
`%var%` anywhere** intermittently collapses to `{valuekind:Object}` (1 key — `steps`
lost), so `Validate.goal` throws `Planner returned %plan.steps.Count% step plans but goal
has N steps` (with `%plan.steps.Count%` itself unrendered = the null-steps tell).

**It's deterministic per content, not random:** temp=0, so a given goal's response is the
same bytes every time. One goal corrupts 3/3 rebuilds; another succeeds 5/5. So it's a
`JsonElement`→CLR unwrap bug triggered by *certain* valid response shapes.

Repro — this 2-step goal corrupts 3/3:
```
Start
- throw error 'boom', on error call Catch
- assert %caught% equals true
Catch
- set %caught% = true
```
Planner RawResponse (valid, no `%var%`):
```json
{"description":"Throws an error and handles it by calling Catch, then asserts the caught flag is true.",
 "steps":[{"index":0,"actions":["error.throw","error.handle","goal.call"],"confidence":"High"},
          {"index":1,"actions":["assert.equals"],"confidence":"VeryHigh"}]}
```
→ `%plan% = {valuekind:Object} (1 keys)` → BuilderPlannerFailed.

Contrast — `NavigationOnTypeUnknown`'s Start plan (also 3-action step 0, also fenced
```json) parses fine to `(2 keys)` and builds 5/5. So it's not fences / action-count;
it's something about the specific bytes. This is the same root as your earlier
`object/json reader unwraps JsonElement` fix — it just doesn't cover the planner's
`write to %plan%` path for all shapes. **This is what makes the 3 negative goals look
"non-deterministic" to build.** Highest priority — it blocks rebuilding any goal whose
planner response happens to land in the bad shape.

## 1. DoublePlusDecimal — needs double-as-default (Ingi's call) + runtime mix-error

Ingi decided: **a bare decimal-point literal (`1.5`) should default to `double`**, standard
behavior (decimal stays opt-in via `as number/decimal`). That's a **coder change**: flip
the runtime literal inference (the `TryGetDecimal`-before-`GetDouble` order) and re-pin
`Cut1_LiteralKindArithmeticOutput` + C# `BuilderKindStampingTests` (they currently lock
bare `3.5`→Decimal). I reverted the goal back to `set %a% = 1.5` (no cast) so it's correct
once the default flips.

Then the second half: even with `%a%` stamped `number/double` and `%b%` `number/decimal`,
`%a% + %b%` **computed to 1.6 with no error** at runtime — the `math.add` path does **not**
raise `PrecisionMixRequiresChoice`. Your C# `NumberArithmeticTests`/`Cut5` enforce it in
some path, but not this goal's `math.add`. Needs wiring.

## 2. SignAndVerify + TamperedSigned — sign produces no Signature

`SignAndVerify` fails `Data has no signature` (you flagged: goal.call param drops the
Signature). `TamperedSigned` hits the same: after `sign "hello world"`, `%signed%` is
`{name:text, value:"hello world", type:{name:text}}` — **no signature field at all**, even
without crossing goal.call. So `sign` isn't attaching/surfacing a signature on the goal
surface. Both are yours.

I reworked `TamperedSigned`'s tamper step from `%signed% + " (tampered)"` (compiled to
`math.add` → `cannot coerce Dictionary to number`) to interpolation `"%signed% (tampered)"`
— correct PLang (Ingi: there's no concat operator, string-building is interpolation). But
note interpolating a Data dict renders its CLR `ToString` (`"System.Collections...Dictionary`2 (tampered)"`),
not JSON. The test needs a real tamper story once signing works — your call alongside the
signing fix.

## 3. NavigationOnTypeUnknown — `.field` into `text` returns null, not error

`set %x% = "{json}"` correctly compiles `%x%` as `text`. `%x.port%` then returns **null**
instead of erroring. The test wants the "type-unknown → refuse to guess, ask for `as
object/json`" error to fire — but a quoted literal is a *known* `text`, so the guard never
triggers. Either the runtime errors on `.field`-into-`text` (your domain), or the test is
reworded. (Companion `AsJson_ResolvesTypeUnknown` — `… as object/json` — already passes.)

## 4. Test-design flaw in ALL THREE negative goals — the guard can't pass

`error.handle.cs`: when recovery (`on error call CaughtIt`) succeeds, it returns a
**success** result and marks `Handled=true`, so the step succeeds and **the goal continues
to the next step**. So the pattern
```
- <should-error step>, on error call CaughtIt
- assert false equals true        ← intended unreachable tripwire
```
can never pass — `assert false equals true` always runs after the handled error. The
well-formed shape is a flag the recovery sets, asserted after:
```
- <should-error step>, on error call CaughtIt
- assert %caught% equals true
CaughtIt
- set %caught% = true
```
I did **not** apply this — (a) it needs confirming a var set in the recovery sub-goal
propagates back to the caller's scope (couldn't verify; my probe kept tripping bug #0),
and (b) the goals stay red on bugs #1-3 regardless. Recommend you fold the guard rewrite
in when you fix the runtime, since you own the scoping semantics.

## State / what I changed (uncommitted, will commit per Ingi)
- `DoublePlusDecimal_Errors.test.goal` — reverted to `set %a% = 1.5` (for double-default).
- `TamperedSignedData_FailsVerify.test.goal` — `+` → interpolation.
- Nothing in the builder/runtime. The `.pr` files under `LazyDeserialize/.build/` are
  fresh rebuilds (keep them).

## Acceptance once you land #0-#3
All 4 (`DoublePlus`, `SignAndVerify`, `TamperedSigned`, `Navigation`) go `[Pass]`, plus the
guard rewrite. The other 6 LazyDeserialize goals already pass.
