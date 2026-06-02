# Coder handoff — deterministic repro of builder self-build failures (durable execution)

## The problem
After fixing cache poisoning + slimming prompts, the builder self-build completes ~1/5; the
rest fail on a **low-frequency, random tail** of distinct runtime/conversion edges. Reproducing
them via the LLM is slow and non-deterministic. We want to capture each failure once and replay
it deterministically — fix, re-execute, validate — with NO live LLM.

## Why the cache-replay stopgap does NOT work (tested)
The `llmcache` replays a stored response exactly, BUT the failing call's bad response **errors
before it's written to cache** (cache writes on success). So the cache never holds it, and on
replay the failing step makes a fresh random call → no repro. Confirmed: restoring a captured
cache bundle did not reproduce `String→LlmMessage`. Cache-replay is therefore unreliable here.

## The right approach: durable-execution snapshot (already 90% in place)
- `app.this.Snapshot.cs` `App.Snapshot()` captures full state: Variables, Errors, CallStack
  position (StepIndex/ActionIndex), Build, Providers, Statics.
- **`app/error/Error.cs:64` already calls `App.Snapshot()` AT THROW-TIME** — the failing step's
  exact state is captured the moment it errors.
- `app/snapshot/this.Resume.cs` `Resume()` → `App.Restore` + walks `CallStack.RestoredChain` and
  re-enters each goal at its captured `(StepIndex, ActionIndex)` via `Goal.RunFrom`. Deterministic
  re-execution of the failing step.

**What's missing (the coder task):**
1. **Persist** the throw-time `App.Snapshot()` to disk on a build-step failure (serialize the
   `snapshot.@this` tree — same shape keepalive/channel-suspend already round-trips). Suggested
   sink: `.bot/<branch>/builder/v2/repros/<id>/snapshot.json`.
2. **Load-and-Resume entry**: a way to point the runtime at a stored snapshot and call
   `Resume(context)` — so `plang` can replay a captured failure with no LLM.
3. Verify Variables capture includes the LLM response in scope at throw (e.g. `%plan%` /
   `%compileResult%`) so the failing conversion sees the same bad value on Resume.

Once (1)+(2) land, the loop is: capture failure → Resume (deterministic) → fix handler → Resume
again → green.

## Failure modes to fix (from N=5, fresh, clean cache — evidence in repros/*/build.log)
1. **`llm.query`: Cannot convert System.String to `app.module.llm.LlmMessage`** — a Messages
   element arrived as a bare string instead of `{Role,Content}`. (bundle: repro_15827_1/build.log)
2. **`builder.validateStepActions` param `Step`: Cannot convert System.String** — Step param
   conversion.
3. **`%messages%` null on `error.handle` RETRY** of QueryAndValidatePlan → NRE. I added a minimal
   guard (`OpenAi.cs`, treats null Messages as empty → clean error) but the ROOT is retry/scope:
   a sub-goal's parent-scope variable doesn't survive an error.handle retry. Real fix:
   re-bind on retry, or pass `%messages%` as a goal.call param. `TODO(coder)` in `OpenAi.cs`.
4. **nano returns invalid JSON** (planner/compiler) — needs a retry-on-parse-failure or tolerant parse.
5. **`type` emitted as a JSON array** — minimal tolerance hack in `app/type/this.json.cs`
   (`TODO(coder)`); real fix is prompt/schema (CompileUser.llm Type reference) so `type` is never
   an array, then remove the hack.

## Infra bugs (separate)
- `--build cache:false` does NOT fully bypass the local `llmcache` on the build path (`%!build.cache%`
  is correctly false, yet steps still serve from cache). 
- Degenerate/empty LLM responses get cached with no validation → one bad response poisons all
  future builds until `DELETE FROM llmcache`. Consider validating before caching.

## Pointers
- `PLang/app/this.Snapshot.cs`, `PLang/app/snapshot/this.cs`, `this.Resume.cs`, `ISnapshot.cs`
- `PLang/app/error/Error.cs:64` (throw-time snapshot)
- `PLang/app/module/llm/code/OpenAi.cs` (NRE guard + Messages conversion)
- `PLang/app/module/builder/validateStepActions.cs` (Step conversion)
- Harness: `.bot/<branch>/builder/v2/harness/` (selfbuild.sh measures; capture-repro.sh = the
  cache stopgap, kept for reference though insufficient per above)
