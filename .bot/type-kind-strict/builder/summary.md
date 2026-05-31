# Builder — summary

**Version:** v1

## What this is
Builder-bot review of the `type-kind-strict` branch (structured `type` value
`{name, kind?, strict?}` replacing flat type strings), verified against real LLM
**trace files** (system + user + response triples), plus diagnosis of two failures
found while building the builder itself.

## What was done
1. **Reviewed the type-kind work — PASS.**
   - Structured `type` entity flows correctly through compile for the load-bearing
     cases (`as text/markdown strict`, `as image/gif strict`): LLM emits the full
     `{name, kind, strict}` dict, final `.pr` matches.
   - `os/system/builder/llm/CompileUser.llm` Type-reference block renders fully
     populated (PrimitiveNames, Kinds table, rules) — confirmed from traces.
   - Back-compat verified live: old flat-string `type` in existing `.pr` files still
     deserializes (lenient converter in `PLang/app/type/this.json.cs`) and rebuilds
     to the dict form (observed on `os/system/.build/build.pr`). This leniency is
     **deliberate** (coder commit `42b8430d6`: "the converter reads both string and
     dict forms") — it bridges old `.pr`s until the full Tests-suite rebuild pass.

2. **Diagnosed two build-time failures (handed to coder in `v1/result.md`).**
   Both are **pre-existing**, NOT type-kind regressions. Surfaced building
   `os/system/Run.goal`.
   - **F1:** `error.throw` coerces `Message` to string, so `- throw %!error%`
     (re-throw an Error object) crashes. The re-throw pattern must be valid;
     `error.throw` should accept and re-raise an Error object as-is.
   - **F2:** conversion-failure messages leak C# internals
     (`Object must implement IConvertible`) and omit the parameter name. They must
     lead with target type + variable/parameter name + actual content.
   - **F3 (flagged, not actioned):** `Run.goal`'s `run before app start event` maps
     to `event.on` but names no `GoalToCall`; likely a missing "fire lifecycle event"
     action. Team decision — NOT a teaching-file change, NOT a goal edit.

## Files
- `.bot/type-kind-strict/builder/v1/plan.md` — plan
- `.bot/type-kind-strict/builder/v1/result.md` — detailed coder handoff (F1/F2/F3)
- No production C#, `.goal`, `.llm`, or `.md` teaching files were edited. `Run.goal`
  was read-only.

## State
- Branch `type-kind-strict` @ fd7ee4812, builds clean (0 errors).
- Working tree clean apart from these `.bot/` additions. Build-artifact trace dirs
  from local `cache:false` builds are git-ignored and not committed.

## Change classification
- **No builder source changes.** This version is diagnosis + handoff only. No
  codeanalyzer pass needed (no C# touched by builder).

## Next
- Coder: address F1 + F2 (see `result.md`).
- Team: decide F3 (fire-lifecycle-event action vs. `Run.goal` rewording).
- Deferred (known): full `Tests/` `.pr` rebuild pass to migrate old flat-type `.pr`
  files to the dict shape (not now).
