# coder — runtime2-callstack — v2

## What this is

Closes the runtime2-callstack branch: merges in the source-resolution fix,
removes a band-aid that became a regression, and lands Phase 11 (the
renderer reductions the architect plan deferred). 7 commits on top of v1.

## What was done

### Source-resolution merge (`959cdd36`, then `bc760b42`)

The 280k-prompt LlmFixer blowup came from `Data.AsT_Impl` re-entering
substitution branches when reading a stored value (full-match
`Variables.Get` → recurse → walk container → re-resolve every `%var%` leaf
against current scope). For source-shaped data (rendered template output,
file content, anything carrying literal `%var%` as payload), the second
walk turned literal references into the builder's variable values.

**`959cdd36` fix** — both recursion sites in `App/Data/this.cs` (full-match
line 613, partial-match line 619) replaced with `AsT_Convert<T>` —
type-conversion only, no walk, no substitution. Matches mainstream language
semantics: assignment evaluates once, stored value is opaque payload.

**`bc760b42` fix** — `dd7bf37e` had unconditionally skipped `%!*%`
infrastructure refs in `SubstitutePrimitive` (both branches) to keep the
builder from baking its own `%!callStack%` depth into LLM-emitted parameter
values. With the 959cdd36 fix landed, the `As<T>` path no longer walks
container leaves at consume time — the dd7bf37e protection became
redundant AND overshot: literal `%!error.Message%` inside developer-authored
container values stayed literal at runtime. Concrete trace evidence at
`os/system/builder/.build/traces/.../BuildGoal.json` showed
`buildError.message = "%!error.Message%"` literally instead of the actual
error.

Both branches of `SubstitutePrimitive` now resolve infra refs. Regression
test: `AsTIdentityTests.AsT_PlainDataTarget_DictWithInfraVar_ResolvesAtCanonicalWalk`
pins the new behavior using `Data.DynamicData("!error", () => "boom")`.

The earlier session also dropped `Documentation/v0.2/source-resolution-problem.md`
since the problem-statement doc isn't load-bearing once the fix shipped.

### Callstack test fixes (`6bfdc97b`)

- `Audit.test.goal` — expected count was 4, but documented Audit
  semantics ("every error observed at every call frame") count
  propagation levels. Updated to 7 with a comment explaining the
  multi-level counting.
- `HandledFlagFalseWhenRecoveryFails.test.goal` — was a placeholder
  (`throw "not implemented"`). Replaced with a real scenario: outer
  goal calls a sub-goal whose recovery body itself throws; outer
  handler catches the re-thrown error and asserts `Handled=false`.

### LlmFixer flow regression test (`9d878b3e`)

`AsT_ListObjectSlot_AsListLlmMessage_StoredLeavesNotReResolved` mirrors the
exact builder construction site: `variable.set` MintTyped storing
`List<object?>`, `llm.query` reading as `Data<List<LlmMessage>>`. Embedded
`%goal.Name%` and `%buildStart%` in Content fields must survive intact
through the typed conversion. Pins the path that caused the original 280k
blowup.

### Phase 11: error report renderer (`e8f0114d`)

The architect plan §Phase 11 sketched two renderer improvements the new
Call tree enables: compression of recursive runs and Cause annotation. v1
deferred this. v2 implements the report side (Children-walk / flamegraph
view still deferred — explicitly out of scope per Ingi).

`PLang/App/Errors/CallChainRenderer.cs` is a static `Render(IReadOnlyList<Call>)`
→ `IReadOnlyList<string>`. Pure function, no Error.cs entanglement.

- **Recursion compression**: walks the chain; consecutive frames sharing
  `(Goal.Path, Action.Module, Step.Index)` collapse to one `Name ×N` line.
  A frame whose `Errors.Count > 0` breaks the run so the failing frame
  stays individually visible.
- **Cause annotation**: a frame at a recovery boundary (own `Cause` set,
  differs from the next outer frame's `Cause` by reference) gets a trailing
  `↷ caused by error in: <name> (line N)` hint. Inherited causes (walk-up
  via `Call.Cause => _ownCause ?? Caller?.Cause`) don't re-annotate
  descendants.

`Error.cs:189-200` is now a 2-line iteration over `CallChainRenderer.Render`
output instead of inline frame formatting.

### Housekeeping (`1124f1ea`, `ba4a49ee`)

- `.gitignore` — `**/.build/traces/` (was `.build/traces/`, anchored to root,
  didn't match nested), `**/junit_sensitive_masked.xml` (snapshots embed
  run timestamps + freshly generated public keys; untracked the 2 existing).
- `CLAUDE.md` — dropped the removed `p` subcommand from build/debug
  examples, replaced the `!debug=Start` form (internal storage key, not a
  CLI shape) with `--debug={"goal":"Start"}` JSON form, points at
  `cli_reference.md` for the full property bag.

### Builder rebuild artifacts (`14a0e4a5`)

Builder rebuild after the SubstitutePrimitive fix picked up unrelated LLM
re-emit improvements: `variable.set Name` slot type `string` → `variable`
(matching the IRawNameResolvable convention), `loop.foreach` KeyName /
ItemName populated for `item=%subGoal%` bindings, `llm.query Cache`
parameter `%build.cache%` → `%!build.cache%` (correct infra ref), Schema
parameters as nested dict shape rather than stringified JSON. No code
change required — pure LLM-output drift.

## Test results

- C# tests: **2623/2623 pass** (was 2615 at v1 close + 8 new across DataAsT /
  AsTIdentity / CallChainRenderer)
- PLang tests: **181/181 pass**
- Builder rebuild: succeeds outer-first; .pr files reflect the new behavior

## Risk register

- **`SubstitutePrimitive` infra resolution** is now unconditional. The
  dd7bf37e protection is gone; the case it protected (builder reading
  LLM-emitted `%!callStack%` strings via container walks) is now covered
  by 959cdd36's "no recursion through resolved data" rule via the
  `As<T> → AsT_Convert` short-circuit. **Manual verification gap**: I
  reasoned that the builder's read of `%stepResults%` is a full-match
  `As<BuildResponse>` which goes through AsT_Convert — but didn't observe
  it firing on a fresh build of a goal that emits an `%!callStack%`
  parameter value. If a future LLM emits one, watch the resulting .pr
  for resolved-vs-literal infra strings. The existing CallStack `.pr`
  test files (depthincreasesongoalcall.test.pr etc.) have the right
  literal shape — those tests pass, which is some evidence.
- **Cause annotation reference equality** (CallChainRenderer
  `IsCauseBoundary`) compares `Cause` instances by ref. Correct because
  `Cause => _ownCause ?? Caller?.Cause` returns the same instance through
  walk-up. If `Cause` ever becomes a virtual property that reconstructs,
  the boundary detection breaks.
- **Compression rule fires only on consecutive chain frames.** This means
  `foreach` iterations (siblings, not chain entries) don't compress in the
  error chain — they were never in the chain to begin with. If users ever
  expect "this errored on iteration 47 of 100", the rendering would need
  Children-walk, which Phase 11-as-shipped doesn't do.

## Open follow-ups

- **Default-exclude `slow` tag in CLI** — discussed; deferred. Tag-based
  filtering is supported via `--test='{"exclude":["slow"]}'`, but no
  built-in default. Worth wiring into the CLI when you've collected
  enough slow-tagged tests to make it pay off. (No slow-tagged tests
  exist yet.)
- **OOM safety test** — already exists as `DiffCaptureTests.Diff_DiffModeOverLargeListDoesNotOom`
  (Phase 10 #8). Runs in the regular suite at ~1s; no `slow` tag needed.
- **Token-cap guardrail on `llm.query`** — proposed in the original
  source-resolution doc as a defensive backstop. Dropped per Ingi's call
  ("we have max length that is enough").

## Plan status

Architect plan §Phase 1-11: **all done.** Phase 11 Children-walk /
flamegraph view explicitly out of scope per Ingi.

Branch ready to merge.
