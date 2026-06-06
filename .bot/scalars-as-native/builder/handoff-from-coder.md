# Handoff to builder — two builder issues found on `scalars-as-native`

From: coder (scalars-as-native). Branch is pushed; both items below are about the
**LLM build path**, not the runtime. The runtime is fine — the existing PLang
suite **runs 271/309** and the C# suite is green except the 23 known lock-phase
stubs. Nothing here is caused by the scalar-wrapper work (bisect evidence below).

Working dir for all repro commands: `Tests/`. Binary:
`PlangConsole/bin/Debug/net10.0/plang`. Use `--build={"cache":false}` to force
fresh LLM calls (don't trust the cache while debugging — see #1).

---

## 1. LLM cache poisoning — ALREADY FIXED (FYI, please sanity-check)

**Symptom (before fix):** every build failed with `BuilderPlannerFailed` /
"Planner returned  step plans" / "no actions", even for `set %a% = 2`. Looked
like a dead builder.

**Root cause:** `OpenAi.cs` cached the parsed LLM response as a raw `JsonElement`.
`JsonElement` does not survive the cache's disk serialization — `Normalize` has no
`JsonElement` leaf arm, so it reflects to its `ValueKind` property and writes
`{"valuekind":"Object"}`, losing all content. Every cached JSON response (every
planner/compiler call) restored empty → `%plan.steps%` / `%planStep.actions%`
null. Live (cache-miss) calls worked because `Data.Ok` unwraps the `JsonElement`;
only the **cache-read** path broke, so builds failed on the *second* run onward
(the first write poisoned the cache). This is a branch-lineage interaction
(collections-are-data made the cached value a native dict; the `JsonElement`
nested inside it stopped round-tripping).

**Fix (committed `92bde6079` / the `OpenAi.cs` commit before it):** cache the plain
`RawResponse` string (lossless on disk) and re-parse it on restore via a shared
`ParseResultValue` helper, so a restored result is byte-identical to a live one.
Also store the unwrapped `result.Value` instead of the raw `JsonElement`.

**Please verify:** build any goal twice (the 2nd run is the cache-read path):
```
plang build --build='{"files":["Modules/List/ListOps.test.goal"]}'   # writes cache
plang build --build='{"files":["Modules/List/ListOps.test.goal"]}'   # reads cache — must still map correctly
```
If you have a cleaner home for "a value that must survive the cache round-trip"
than re-parsing RawResponse, that's yours to refactor — I took the minimal,
provably-correct path.

---

## 2. `assert` / `on error` mis-map — NEEDS YOUR FIX (the real handoff)

**Symptom:** the builder deterministically mis-maps `assert … is true` /
`assert … is null` / `assert … equals` to `condition.if` / `error.throw` /
hallucinated actions (`validator.acknowledge`, `error.throw.throw`) — and drops
the `on error …` modifier — **in some goals but not others**. It is *not* LLM
non-determinism (repeats identically across runs).

**Deterministic repro** (pre-existing committed `.pr` maps these correctly; rebuild
breaks them). Save the original first so you don't commit a bad `.pr`:
```
cp App/CallStack/.build/handledflagsetwhenrecoverysucceeds.test.pr /tmp/hf.pr
plang build --build='{"files":["App/CallStack/HandledFlagSetWhenRecoverySucceeds.test.goal"],"cache":false}'
# inspect: step 1 "assert %recovered% is true" -> condition.if (WRONG, should be assert.isTrue)
#          step 2 "assert %!error% is null"     -> error.throw  (WRONG, should be assert.isNull)
cp /tmp/hf.pr App/CallStack/.build/handledflagsetwhenrecoverysucceeds.test.pr   # restore
```
3/3 identical. A no-error-context goal breaks too: `assert %sum% equals 5` in
`ScalarsAsNative/Stage1/NumberArithmeticUnchanged.test.goal` first compiles to
`error.throw` (missing `Message`) → validation retry → garbage actions.

**Contrast:** `Modules/List/ListOps.test.goal` rebuilds with all asserts correct
(`assert.equals`, `assert.isTrue`, `assert.isFalse`, …) on the same branch/binary.
So the mapping works for that goal and fails for the error/assert-heavy ones —
context-dependent, deterministic.

**Where it sits (planner vs compiler):** I traced the planner — for these goals it
returns the *correct* `assert.*` set in some captures, but the per-step **compile**
phase emits `condition.if`/`error.throw` and then the `builder.validateStepActions`
retry loop hallucinates. I did **not** fully isolate whether the planner or the
compiler is the first to go wrong — that's the first thing to pin. `llmTrace`
helps: `--debug='{"llmTrace":true,"maxLength":4000,"goal":"Compile"}'` and
`...,"goal":"Plan"`.

**Bisect — it predates the scalar work.** I checked out the base commit
`032427b06` (test-designer's, before any coder commit) in a worktree, applied
*only* the cache fix, rebuilt `HandledFlag` with `cache:false` → it mis-maps
**identically**. So this is an **earlier branch-lineage regression** (collections-
are-data era), not the `item`/wrapper work. The committed `.pr` files are correct
because they were built by an older builder; the regression only surfaces on
*rebuild*, which is why the 271-test run is unaffected.

**Suspected fix area** (per `Documentation/v0.2/building_plang_tests.md` "Fix the
root cause"): the per-action teaching for `assert.*`
(`os/system/modules/assert/*.{notes,examples}.md`) and/or the planner/compiler
kernels (`system/builder/llm/{Plan,Compile}.llm`) — make `assert` verbs stop
collapsing into `condition.if`/`error.throw` under error/throw context. Also look
at why the `on error set %x% = …` modifier is dropped (the `error.handle` modifier
not being attached to the host action).

**Scope warning:** this touches every build, so re-validate broadly after any
prompt change — `builderVersion` tracks which builds need rebuilding. Don't fix it
by changing the `.goal` files being built (real users write the same patterns).

**Why it's handed to you, not fixed by me:** it's a pre-existing builder regression
unrelated to the scalars branch, and blind-tuning the planner/compiler prompt risks
the rebuildability of the 271 currently-passing builds — that's your call to make
with the builder's full context.

---

## What's blocked on #2

`Tests/ScalarsAsNative/Stage{1..7}/*.test.goal` can't be reliably built until the
assert mapping is fixed (the wrapper work itself is verified by the C# suite —
`PLang.Tests/App/ScalarsAsNative/`). Once #2 is fixed, those goals should build;
they use ordinary `set` / `if … is …` / `assert … equals` / `sort` / `assert … is
true` patterns. The Stage-1 goals are authored; the rest of the stages (born-native
flip, `where T : item` constraint) are deferred per `.bot/scalars-as-native/coder/v1/report.md`.
