# User feedback to the builder bot

**From:** Coder (after shipping the data-serialize-cleanup branch — wrote ~10 PLang test goals, rebuilt them many times, debugged build-time failures).
**Date:** 2026-05-28
**Branch base:** `runtime2`
**Posture:** This is a feature-and-friction list, not a bug report. Net experience as a user: ~7/10. The natural-language → action mapping is genuinely magical when it works (add `compress.cs` + two `.md` files, type "compress %x%, write to %y%" in a goal, it Just Works). The friction is consistent enough that I lost real hours, and most of those hours were "debugging what the builder thinks," not "debugging my code."

Out of scope on purpose: **builder-bootstrap fragility** (when a runtime primitive change breaks the builder itself so you can't rebuild to test the fix). Ingi: "we're making the language, breaking the builder is part of the pain." Acknowledged, not chasing.

---

## Priority 1 — Cache invalidation by action catalog

**Problem.** I added `variable.compress` to the C# catalog and a `compress.description.md` for the LLM. My existing `.pr` (built before `compress` existed) still ran. The `[Stale]` marker only fires when the `.pr` file is *missing*. The test ran the wrong shape silently for ~an hour before I realised the `.pr` was outdated relative to the catalog.

**What I want.** At build time, embed a hash of the action catalog into the `.pr` (concretely: ordered list of `(module, action, parameter-name+type signature)` tuples, SHA over their canonical form). At run time, the test runner / engine compares the embedded hash to the current catalog. Mismatch → mark `[CatalogStale]`, fail loud, suggest `plang build --files=...` to rebuild.

Bonus: include the hash in the `.pr` JSON under `builderVersion` (already exists per `app-tree.md`) or a sibling `catalogHash` field. The build is already non-deterministic per goal (LLM mood), so the catalog hash isn't expected to make builds reproducible — it just catches the "your code changed, your bytecode didn't" failure mode.

**Acceptance:** I add a new action to the catalog, run `plang --test` on an existing goal whose `.pr` doesn't mention it. The runner says `[CatalogStale]` (or equivalent) instead of silently using the old bytecode.

---

## Priority 2 — `plang build --explain` for the LLM input

**Problem.** When my goal step `compress %original%, write to %archived%` first compiled to just `[variable.set]` (the LLM ignored "compress"), I had no quick way to see *what the planner LLM saw*. I had to:

1. Read the architect's docs to discover `.md` files surface actions to the LLM.
2. Guess that was the missing piece.
3. Add them, rebuild with `cache:false`, hope.

`--debug={"goal":"Compile","step":1}` showed me the planner's *output* but not its *input*.

**What I want.** A dry-run mode that prints, for one step:

1. **The catalog the planner LLM saw** — `(module, action, description-md-excerpt)` rows. So I can verify "yes, `variable.compress` is in the list with my description."
2. **The planner's chosen action set** — what it returned (e.g. `["variable.compress", "variable.set"]`).
3. **The compiler's parameter mapping** — the final `.pr` shape.

Concretely: `plang build --explain='{"files":["foo.goal"],"step":1}'` prints sections (1), (2), (3) for that step. Default output is human-readable; `--explain={..., "json":true}` emits machine-readable for piping.

**Acceptance:** I add a new action, write a goal step using it, and one `--explain` invocation tells me whether the LLM is even considering the action.

---

## Priority 3 — Path/CWD discipline is silent (real footgun)

**Problem.** Building from `/workspace/plang` writes `.pr` files whose `path` field is `/Tests/Serialization/Foo.test.goal`. Building from `Tests/` writes the same `.pr` files but with `path = /Serialization/Foo.test.goal`. The test runner discovers goals from its CWD; only path-prefix-matching `.pr` files are found. So a build from the "wrong" directory silently puts the .pr in a place the runner won't see — `[Stale]` (file missing) reappears, but the .pr literally does exist on disk.

This bit me twice in one session.

**What I want.** Either:

(a) **Make path canonical regardless of CWD.** Compute the goal path relative to the *project root* (the directory containing `Tests/`, `PLang/`, etc.), not the build CWD.

(b) **Warn at build time.** Print "goal will be assigned path `/Tests/Serialization/Foo.test.goal`" and if `--test` discovery rules don't match, say "test runner looks at `/Serialization/...`; this `.pr` won't be picked up from `Tests/`."

Either is fine. (a) is the real fix, (b) is the cheap diagnostic that catches the footgun.

**Acceptance:** Whatever directory I run `plang build` from, the test runner finds the resulting `.pr`. Or, the builder warns me loudly when the path won't match.

---

## Priority 4 — Surface the root cause, not the recovery chain

**Problem.** When the build crashed, the top of the error display was always:

```
🔴 PrimitiveConversionFailed(400)
🧐 Reason: Cannot convert '[NullReferenceException] builder.validateStepActions: NullReferenceException ...' to String: Object must implement IConvertible.
```

That's the **recovery handler** failing to format the real error. The real error was a `NullReferenceException` inside `validateStepActions`, displayed several scrolls down inside the inner error block, under a `StackTrace:` line.

The recovery scaffolding (HandleBuildFailure → BuildGoal → Start) frames the real cause as if *it* were the bug. I had to learn that any time I see `Cannot convert '[ExceptionTypeName] message...' to String`, I should look for the bracketed inner exception and read THAT.

**What I want.** Invert the display:

```
🔴 Root cause — NullReferenceException at validateStepActions:329
   (during build of /Tests/Serialization/Foo.test.goal, step 1)
   
   <inner error details, stack trace>

ℹ️ Recovery context: error.throw at HandleBuildFailure:53 failed to format the original error (this is normal — secondary).
```

Mechanically: when a `PrimitiveConversionFailed` (or any error-during-error-handling) is the outermost error, walk the inner `[ExceptionTypeName] message...` string to find the original, surface IT first, then mention the recovery failure as a footnote.

**Acceptance:** When I trigger a build-time crash, the FIRST thing I see is what actually went wrong, not the scaffolding that couldn't print it.

---

## Priority 5 — `plang build --strict-cache` (determinism mode for CI)

**Problem.** The same goal text builds differently across runs because the LLM is non-deterministic. CI can't assert "the .pr we ship is the .pr we get from a clean build."

**What I want.** A flag (`--build={"cache":"strict"}` or `--strict-cache`) that:

- Uses LLM cache hits silently.
- **Fails the build** the moment any step would need a live LLM call (cache miss).

That gives CI a contract: "all bytecode in this repo is reachable from cached responses; the build is reproducible." Local developers still use the default mode for authoring; CI uses strict.

**Acceptance:** `plang build --strict-cache --files=...` succeeds when every step is cached, fails when any step needs the LLM. Exit code 0 vs 1 is the CI signal.

---

## Priority 6 — Catch unmapped verbs in step text

**Problem.** `validateStepActions` checks the inverse direction: drops planner-suggested actions that don't exist in the runtime catalog. But the opposite direction has no check.

When the planner LLM ignored "compress" entirely and chose only `variable.set`, the resulting `.pr` had no `compress` action despite the step text saying `compress %x%, write to %y%`. Silent drop of user intent.

**What I want.** A second pass in `validateStepActions` (or a sibling action) that scans the step text for verbs corresponding to registered actions and warns when none of the chosen actions cover them. The check has to be heuristic — natural-language verb to action-name is fuzzy — but even a "step text contains the literal action name `compress` and the chosen action set doesn't include any `*.compress`" check catches the failure mode I hit.

False positives matter here. Better to log a build-time warning ("step text mentions `compress` but no matching action was selected — consider `module.action` explicit form") than to fail the build outright.

**Acceptance:** I write `compress %x%, write to %y%` and the LLM forgets `compress`. A build warning surfaces: "step 1 mentions verb `compress`; closest catalog match `variable.compress`; not selected."

---

## Priority 7 — `plang build --watch`

Quality-of-life, not load-bearing.

**Problem.** Iterative goal authoring is a manual `rm -rf .build && plang build` loop. Slow when you're tuning the step text to coax the LLM into picking the right action.

**What I want.** `plang build --watch --files=...` re-runs the build on `.goal` file change. Cache stays warm; only the touched file's `.pr` regenerates.

**Acceptance:** Save the `.goal`, terminal shows the rebuild and the new `.pr` digest. Don't have to remember to `rm` first.

---

## Bigger thought — `.pr` as build artifact, not committed source

The current model treats `.pr` as committed bytecode. The `.goal` source IS readable, but the `.pr` is what runs — and it's checked into git, opaque without `python -m json.tool`, and (importantly) **not regenerable** because the LLM is non-deterministic. So the `.pr` is the source of truth for *what actually runs*, and the `.goal` is documentation of what the user *intended*.

That's an inversion of how source-vs-build typically works, and it's where most of the friction above comes from. If we got determinism (catalog hash + strict-cache + a frozen LLM cache file in the repo), `.pr` could move to `.gitignore` and `.goal` become the actual source of truth. Then:

- The `[Stale]` problem disappears (cache invalidation is just "regenerate on every build").
- Path/CWD problem disappears (no committed artifact to mis-place).
- Test-runner becomes a one-shot build-then-test pipeline.
- Diff review focuses on `.goal` changes, not `.pr` byte churn.

This is the conversation that touches all six priorities above, so I'm flagging it as the underlying architectural shape rather than as a P-number itself. Open question for Ingi, not an action item.

---

## Suggested ordering

1. **P1 (catalog hash)** — small, mechanical, big quality-of-life win. The .pr already has builderVersion field to extend.
2. **P3 (path/CWD)** — also small. Either canonicalize from project root or warn loudly.
3. **P4 (root-cause-first errors)** — display-only change in the recovery handler. No new infrastructure.
4. **P2 (--explain)** — bigger lift but the highest-leverage diagnostic. Worth the cost.
5. **P5 (--strict-cache)** + **P6 (verb coverage warn)** — pair them, both feed determinism story.
6. **P7 (--watch)** — nice-to-have, ship when there's time.

Bigger thought (.pr as artifact) parks until after the priorities land — by then the infrastructure for it is in place.
