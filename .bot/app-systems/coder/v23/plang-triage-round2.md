# app-systems — plang reds, round 2 (after the builder's rebuild aba68eb5b)

Run: fresh PlangConsole at db8ad1e69 (tag fix in), `cd test && plang --test={"exclude":["llm","network"],"timeout":"60s","parallel":4}`
→ **202 pass, 55 fail, 16 stale, 5 skipped** (the llm ones). The skip-tagged tests (signing ×2, duration) don't skip
yet: their `.pr` were built before a1a8ca80d, so no tag was stamped — one more rebuild and they drop out.

## R1. builder — goals reached only through a recovery, or only by discovery, are never built (~20)
The sibling pass builds what `goal.call` reaches, not what a step reaches through `on error call X`, nor fixtures a
test reaches through `test.discover`.
- on/error: InspectError, HandleError, GoalFirstHandler, BadHandler+OuterHandler, MixedCatcher, Catch{Arabic,English,
  Icelandic,Japanese,Korean,Swahili}, InnerHandler, HandleRetryError — ErrorProps, ErrorChain, ErrorGoalFirst,
  ErrorInHandler, ErrorMixed, OnErrorMultilingual, ErrorNested, retry/Retry.
- goal/missing HandleMissing → GoalCallMissing (the runtime catches it: CallMissingTests).
- module/test fixtures (`_fixtures_pass`, `_fixtures_fail`, `_fixtures_sensitive`, `run/_fail`, `run/_timeout`,
  `run/_isolation`, `assert/_fixtures`, `edgecase/_fixtures`) → report ×5, run ×3, assert ×1.
- GoalNotBuilt ×2: condition/file/filenotexists WhenMissing, loop/foreach/dictionary ProcessEntry.

## R2. builder — wrong saves still there after the rebuild
| test | saved as |
|---|---|
| DictIsItemKeepsNoOrder, DoublePlusDecimal_Errors | `on.error` after the write-back `variable.set` — it binds to the set, so the query/add's error isn't caught (the `.pr` that worked put it right after the main action: `math.add \| on.error \| variable.set`) |
| DoublePlusDecimal_Errors | also `as number/decimal` → `Type=number` (kind dropped): %b% is a double, nothing errors |
| condition/compare Cut5, Failure_DictEqNumber, Failure_DictOrderDict | `condition.if` only — `on error call Caught` dropped |
| channel/goalchannelrecursion, ActorSwitch | the channel / actor dropped or misplaced |
| Cut2, Narrow_ChainWideBang | `assert %cfg% is file` → `assert.equals(%!data%, %cfg%)`; `is dict` → `isTrue` |
| BareLiteralJudgedByForm | literals saved untyped ("2026-01-01" text) — the teaching types by form |
| DataTypeReadsEntity | `type=text/plain` → `Type=text` (kind dropped) |
| Plane_DataKeyVsProperty | `type=json` → `Type=text` (kind dropped) — `.size` can't navigate a plain text |
| Cut4 | `sort %files% by size` → `order: {}` (the field dropped) — paths have no order of their own |
| ListIsItemSortsAsBefore, NullsSortLast | still `order: "%nums%"` / `"null"` — the `{order: {}}` teaching isn't in these `.pr` |
| RetryFirstReturnsRecoveryValue | `variable.set \| on.error \| error.throw` — the recovery's set saved first, as an action |
| signing/headermismatch | built from the repo root again (`/test/...` path) |

## R3. stale — rewritten (e00faf6cf), builder to rebuild
`%x.Type.Kind%`/`.Type.Strict` → `!type.kind`/`!type.strict` (kind/strict ×9, ReadConfigJson, ReadCsv — 572);
ErrorVarIsNullOutsideHandler expects the NotFound (226).

## R4. runtime — for the architect
1. **on.error GoalFirst returns a failed retry before Ignore** (module/on/error.cs:82–87). `if (retryResult != null)
   return retryResult;` returns the retry's failure, so `Ignore` (and a successful recovery's result) is never reached
   when RetryCount is set; the RetryFirst branch returns only a retry that succeeded. OnErrorRetry
   (`retry 3 times … ignore`, GoalFirst) fails "intentional failure". Fix: return the retry only when it succeeded;
   otherwise fall through to the recovery's result, then Ignore — as RetryFirst does.

## R5. not yet traced past the goal
app/callstack/Audit (`%!call.Audit.Count%` ≠ 7 — Audit exists, call/list/this.cs:74); module/variable/scoping
VariableScoping (`call InnerWithParam id=42` then `%id%` = 999: the callee's `id` is its own parameter — stale if the
rule is that parameters are the callee's own, which decision?); app/permission ×2 (statefulauthorize PermissionDenied
on //tmp; grantastorespersisted NoWireContract on a goal channel); signing/corruptedsignature (`%data.Signature.Signature%`
— likely 572: the signature is a fact, `!signature`); module/test/tag (depends on the tag stamp + rebuild).
