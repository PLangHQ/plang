# codeanalyzer — runtime2-callstack — v3

## Scope

Verifies coder/v2 cleanup commit `be77dc12` against the v2 review's 5 v1
carry-forward MINORs + 3 new MINORs + 2 NITs. Plus comments on coder's
two rejections and one deferral.

## Verified fixes (8/8 MINORs)

| Finding | Site | Status |
|---|---|---|
| v1#1 Tags flag dead | `CallStackFlags.Tags` | **Doc-reframed** (see below) |
| v1#2 `Errors._current` static | `Errors/this.cs:19` | ✅ instance-level `private readonly AsyncLocal` |
| v1#3 App.Run Push outside try | `App/this.cs:404-416` | ✅ Push inside try with `CallStackOverflow → ServiceError` catch |
| v1#4 `Diffs.Add` not thread-safe | `Call/this.cs:134` | ✅ `lock(Diffs!)` inside OnSet handler |
| v1#5 `Step.Context` not restored | `App/this.cs:471` | ✅ `previousStepContext` saved and restored in `finally` |
| v1 dead `SerializableCallStack` | `CallStack/SerializableCallStack.cs` | ✅ deleted + 2 GlobalUsings aliases removed |
| v2#1 Goal.RunAsync Push outside try | `Goals/Goal/this.cs:286-306` | ✅ Push inside try with same overflow catch |
| v2#2 `[ThreadStatic]` cycle HashSets | `Data/this.cs:538`, `Variables/this.cs:622` | ✅ `AsyncLocal<HashSet<string>?>`, comments explain "future await safety" |
| NIT DictionaryNavigator Count | `DictionaryNavigator.cs:38-83` | ✅ all three arms consistent — keys win, Count is fallback |

Spot-checks pass. Quality of fixes is high — the explanatory comments
(`previousStepContext`, the goal-frame Action.Step rationale, the AsyncLocal
"future await" note) make intent durable.

## Rejection 1: Tags flag — ACCEPTED

`CallStackFlags.Tags` is now documented as an advisory hint for downstream
exporters; `Call.Tag()` writes unconditionally. Coder's reasoning:

> Enforcing the gate on `Call.Tag()` broke user-authored `- tag x=y` PLang
> tests because the runner uses default flags — explicit observability
> intent (user wrote `tag`, or a C# handler emitted a diagnostic) shouldn't
> be silenced.

Accepted. The original v1 finding framed this as "doc lie OR enforce"; coder
picked "doc fix" with a reasonable design rationale (explicit intent
overrides default-off flag). The flag retains meaning as exporter advice.

## Rejection 2: AsCanonical `skipInfrastructure` — ACCEPTED with note

Coder framed this as "analyzer working from stale state" citing
`bc760b42`. Two clarifications:

- The actual fix commit on this branch is `c4381135` ("SubstitutePrimitive
  resolves %!*% in container leaves"), not `bc760b42` (which doesn't exist
  here).
- v2's review window: at review time, `SubstitutePrimitive` still had the
  `dd7bf37e` carve-out. The asymmetry I flagged was real **as of that
  snapshot**. Coder resolved it by going the opposite direction —
  removing the carve-out from `SubstitutePrimitive` to match `AsCanonical`,
  rather than adding it to `AsCanonical`.

Either resolution closes the asymmetry. The chosen direction is pinned by
`AsT_PlainDataTarget_DictWithInfraVar_ResolvesAtCanonicalWalk`. Accepted.

## Deferral: Goal frame `Action.Step = Steps[0]` — ACCEPTED

Comment block at `Goals/Goal/this.cs:278-282` now explains: `Steps[0]` is the
**Step→Goal anchor for ContainsGoal cycle detection** (reads
`action.Step?.Goal?.PrPath`), not "step 0 currently running". Observers
should treat the goal frame's `Action.Step` as the goal anchor; child
`stepCall.Action.Step` is the live step.

The comment turns the misleading provenance into explicit semantics. Fine.

## Side observations on this round's other commits

Out of scope but noticed:

- `e31e5236` (Phase 11 `CallChainRenderer`): clean static, no Error.cs
  entanglement, two well-bounded reductions (recursion compression, Cause
  annotation). The risk-register note about reference-equality on
  `IsCauseBoundary` is correctly identified.
- `367ca1e7` `Audit.test.goal` count change 4→7 with rationale comment is
  fine — the original test number was wrong, not the implementation.
- `71c76598` `.gitignore` `**/.build/traces/` glob fix is correct (the
  un-anchored form was missing nested matches).

## Verdict

**PASS.** All MINORs addressed (fix or accepted rationale). NIT explained
in code. Tests green (2623 C# / 181 PLang per coder, not re-verified here).
Branch is ready to merge.
