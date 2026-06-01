# Self-rebuild instability — diagnosis in progress

**Goal (Ingi + builder):** the builder MUST be able to self-build. We cannot rely
on an old builder binary to build the new builder. "LLM is non-deterministic, try
again" is NOT an acceptable answer — the fix is diagnostics: what is sent to the
LLM, what comes back, and what changes if we adjust X/Y.

## Confirmed so far (binary @ 254f31b69, coder F1/F2 in)

- **Normal user-goal build WORKS.** `Tests/TypeKindStrict/SetIntLiteralIsNumberInt`
  → `Saved Start`, all steps `[✓]`, 0 errors. So the builder builds *other* goals.
- **Self-rebuild FAILS deterministically.** `cd os/` + the 8-file ordered list
  (per `building-the-builder.md`) → `0 Saved`, every attempt fails the same way.
- **Failure is the PLANNER, not error-handling, not the throw fix.** Error:
  `BuilderPlannerFailed(400)` — "the LLM never returned a steps array / proposed
  step count didn't match the goal, retry didn't recover." Raised at plan time
  (`BuildGoal/Plan.goal`), upstream of `error.throw`.
- **Builder SOURCE goals are intact.** `BuildStep/Start.goal:7` is exactly
  `- call Compile` — the garbled `call Compile.Message%` seen in one error dump was
  an LLM/planner mangling in the *response*, NOT a defect in the source `.goal`.

## CORRECTED diagnosis (trace-confirmed) — planner returns NO usable response

Read `Build.json` trace `plan` object directly. The finding overturns the
"comment miscount" guess:

```
plan keys = ['valuekind', 'system', 'user', 'usage']   ← NO 'response' key
plan step count = 0
buildError = BuilderPlannerFailed "the LLM never returned a steps array"
```

The planner LLM **was called** (token `usage` is recorded) but produced **no
parseable response** — the `response` field is absent, and 0 steps came back.
"Step count didn't match" is the DOWNSTREAM symptom of 0 returned steps, not a
miscount of prose. So this is NOT a step-definition/comment problem. It is a
**response shape / parse / empty-output problem** at the planner LLM boundary.

Three candidate causes, in order of likelihood to check:
1. **Response schema mismatch** — the model's JSON doesn't match the expected
   `{description, steps:[{index, actions, confidence}]}` contract, so it
   deserializes to empty. The Plan response contract lives in the planner's
   llm.query schema (see `Plan.goal` / `Plan.llm` / any `*.scheme.json`).
2. **Model returns empty/truncated** — prompt grew (type-kind teaching) or is
   malformed, model refuses/truncates.
3. **Parse path regressed by the type-kind branch** — response parsing flows
   through the same Data/type deserialization that changed on this branch.

To discriminate: need the RAW planner exchange (system+user+**response**). The
per-goal `*.json` omits `response` precisely because it failed; must capture the
`llm/` raw files. `--debug={"llm":{"output":"file"}}` did NOT write `llm/`
subdirs this run (likely cache hit — builder LLM cache in `.db/system.sqlite`).
Re-run with `cache:false` AND llm output-to-file to force a fresh call and
capture the raw response.

This ties to tester v8 FAIL + the parked migration: 688/703 committed `.pr` carry
stale bare-string `type`, so the @known/keep reuse path can't cleanly reuse them →
the builder is forced into full LLM re-plans every run → the planner miscount bites
every time instead of being masked by reuse.

## Where the evidence lives (trace files)

Build with: `cd os/ && plang '--build={"files":[...8 ordered...]}'
'--debug={"llm":{"output":"file"}}' build`
Planner exchange per goal: `os/.build/traces/<id>/llm/<Goal>_goal.txt`
(contains system + user + response). Per-goal output: `<id>/<Goal>.json`
(has `buildError`, `plan`, `goal.Steps`). NOTE: each build run makes its OWN
trace dir (sortable id) — discover the newest from disk, never guess the id.

## Next steps (resume here)

1. Read a planner `*_goal.txt`: compare the goal's REAL step count (count `- `
   lines, exclude `/` comments) against the `steps[]` array length in the
   response. Confirm/refute the miscount hypothesis.
2. If miscount confirmed: is it the system prompt (`Plan.llm`) under-teaching
   "what is a step" (comments, compound on-error)? Draft the prompt fix, rebuild
   no-cache, re-trace, verify step counts now match.
3. If NOT miscount: read the actual response — is it empty/unparseable? Then it's
   an LLM-output-shape or schema issue, look at `Plan.scheme.json` / the response
   contract.
4. Candidate levers to test (X vs Y): (a) tighten Plan.llm step-definition +
   add the builder's compound-step shapes as examples; (b) make `/` comment
   handling explicit; (c) check whether the stale-`.pr` reuse path, once the
   corpus is migrated, removes the need to re-plan at all (the migration may be
   the real fix, not the prompt).

## Discipline notes
- Cardinal rule: NEVER keep a builder `.pr` from an errored self-rebuild —
  `git checkout HEAD -- os/system/.build/ os/system/builder/.build/` after each
  failed attempt. (Done; tree clean.)
- Builder build cwd is `os/` (not os/system/). Verify target: `cd Tests/Simple`.
- Trace dirs under `.build/traces/` are git-ignored — fine to leave, not committed.
