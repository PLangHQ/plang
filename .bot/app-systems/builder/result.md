# app-systems — builder Step-3 test run report (final)

Fresh binary, `cd test && plang --test`.

## Totals (progression across rebuild passes)

| run | pass | fail | stale | total |
|---|---|---|---|---|
| baseline (batch built only `.test.goal`) | 129 | 126 | 28 | 283 |
| + sibling/stale/wrong-prPath rebuild | 195 | 68 | 15 | 278 |
| + coder-rewrites + wrong-saves + E1 sort | 203 | 61 | 14 | 278 |
| round 3: +100 helpers + rewrites + tag fix | 220 | 33 | 21 | 277 (+3 skipped) |

**Round-3 baseline: 220 pass, 33 fail, 21 stale, 3 skipped.** sibling-unbuilt is 0; the skip-tag fix works (the signing pair + Duration now skip). The 33 fails: ~29 genuine runtime reds (coder's, from this session's runtime changes); 2 write-back placement wrong-saves (DictIsItemKeepsNoOrder, DoublePlusDecimal — the clause anchors to the appended set in the SERIALIZED .pr; the fix corrects the in-memory order but not the serialized/load order — WITH THE CODER, who owns the clause binding + .pr order and will replace the builder's IsClause name-check with the clause's own knowledge); 2 wrong-prPath (doubleplusdecimal, signingheadermismatch — flaky). The 21 stale are builderVersion drift from the IsClause/write-back binary rebuilds — a rebuild pass clears them, best after the coder's write-back fix lands (else they re-stale). The llm built tests ran against the real provider on each full run. This round-3 baseline is what the coder's round 3 starts from.

## What the rebuild passes fixed

- **Sibling goals**: the batch built only `.test.goal` entry points; three passes built every helper `.goal` a test calls (AuditLog, CountItem, Inner, DoThing, …). sibling-unbuilt is now **0**.
- **Stale / wrong-prPath**: rebuilt the stale `.pr` (builderVersion drift) and the `.pr` built earlier at cwd=/workspace/plang (prPath `/test/…` → `/app/…`).
- **Coder-triage wrong-saves**: rebuilt on the final binary with this branch's fixes (on.error-does-nothing check, condition/channel teaching, 71): on/error ×6, condition Failure_*/Not, channel, ActorSwitch, DictIsItemKeepsNoOrder, Plane_DataKey, DoublePlusDecimal, rolechannelrefused.
- **Coder stale-test rewrites** (sections D/E4/F): 39 rebuilt (app/data, app/type bool/date/datetime/item/kind-strict/null/path/text/time, module callback/crypto/file/list/setting/test).
- **E1 teaching** (list.query): `sort %list%` with no field → `order: {}` (by elements); fixed ListOps, ListIsItemSortsAsBefore, Cut3, Cut4.

## The 61 remaining fails = genuine runtime reds (coder's) + 14 stale next-branch

The builder maps these correctly (they BUILD); they fail at RUN from the session's coder runtime changes (kind-redesign, math-seam, sensitive-rename, 10b, container-Holds). Concentrated in app/type (12), module/test (10), module/on (10), app/data (6), signing (3), condition (3). Dominant first-lines: `Expected: "bool"/"date"/"gif"/"md"/"int"` (type/kind), `Data has no signature` (signing), `Error: always fails`/`boom` (on.error expectations). These match the coder's triage (plang-triage.md); they are the coder's, not builder mapping. Per-test table below.

## Left unbuilt by reason (next branch)

- **issue 74** (`%s%.length%` / `%x!` / `%!error.data[1%` nav written outside `%…%`): TextLengthOp, MissingVarIsNotNullValue, ThrowAttachesData. Refuses, never a wrong save.
- **issue 75** (the `archive identity X, on error ignore` step drops its action): SigningHeaderMismatch (still wrong-prPath, flaky), RoundTripAfterRawSignatureDeletion, UnknownSettingIsAnError.
- **kinded Type choices**: TypedListHoldsItsType (10b closes on its C# pins; decider offers only base `list`, not `list<path>`).
- **LlmProperties**: the writer pads `Limit` with `tool:10/retry:0` defaults → UnwrittenNumber refuses.
- **Failure_SortMixedIncomparable**: the complex `sort … , on error … , write to …` drops the order.
- Math2, Failure_ParamResolutionError: to diagnose.
- `plan/snapshot/module/callback/*`: unbuilt by design (await the snapshot resume design).

## Builder fixes landed this branch (behaviour + the one C# each)

- 71: `type=json` → Type=json text (teaching); inverse-Cover reads the step's quoted literals decoded (`goal/step/this.Validate.cs`).
- on.error: recovery nests in Recovery (teaching); **on.error-does-nothing structural check** (`module/on/error.cs`) refuses an empty clause — kills the hoisted-recovery wrong-save.
- channel `%!channel%` shortcut (decision 500); condition `and`=nested / `or`=if+elseif / file-exists-else / `!%b%`→`==false`; issue-75 refusal wording; `as list of <type>`, llm maxTokens→Limit.token, E1 `sort` order-by-elements teaching.

All with decider-fixture + page-golden re-pins.

## Per-test failures (final run; for coder triage)

| folder | test | class | first failure line |
|---|---|---|---|
| app/actor/switch | ActorSwitch | runtime | Error: Channel 'system' not found |
| app/callstack | Audit | runtime | Expected: "7" |
| app/callstack | ErrorVarIsNullOutsideHandler | runtime | Error: no error is current |
| app/data | Cut2 | runtime | Expected: %!data% |
| app/data | DataTypeReadsEntity | runtime | Expected: "plain" |
| app/data | Narrow_ChainWideBangBothBranches | runtime | Expected: %!data% |
| app/data | Plane_DataKeyVsProperty | runtime | Expected: 10 |
| app/data | ReadConfigJson_UntouchedIsJsonString | runtime | Expected: "json" |
| app/data | ReadCsv_LandsAsTable | runtime | Expected: "csv" |
| app/permission/authorize/statefulauthorizegrantsandcontinues | Start | runtime | Error: Permission denied: User on //tmp/plang-pattern.txt |
| app/permission/grantastorespersisted | Start | runtime | Error: 'app.channel.type.goal.this' has no wire contract — it declares no [Out]/[Store] face and is not meant to cross the wire. Write what  |
| app/type/dict | DictIsItemKeepsNoOrder | runtime | Error: order: cannot order 'dict' against 'dict' — mixed or unordered values |
| app/type/duration | DurationPartsAndCompare | runtime | Expected: 1 |
| app/type/item | BareLiteralJudgedByForm | runtime | Expected: "date" |
| app/type/kind/strict | SetAsImageGifStrictMatching | runtime | Expected: "gif" |
| app/type/kind/strict | SetAsImageGifStrictMismatch | runtime | Expected: "gif" |
| app/type/kind/strict | SetAsImageStrictNoKind | runtime | Expected: true |
| app/type/kind/strict | SetAsTextSlashMarkdownNormalises | runtime | Expected: "md" |
| app/type/kind/strict | SetAsTextSlashMarkdownStrictUnverifiable | runtime | Expected: "md" |
| app/type/kind/strict | SetIntLiteralIsNumberInt | runtime | Expected: "int" |
| app/type/list | ListIsItemSortsAsBefore | runtime | Error: order: No item has a field '[3, 1, 2]' |
| app/type/null | NullsSortLast | runtime | Error: order: No item has a field 'null' |
| module/channel/goalchannelrecursion | Start | runtime | Expected: "ChannelNotFound" |
| module/condition/compare | Cut5 | runtime | Error: cannot order 'item' and 'item' values with '>' |
| module/condition/compare | Failure_DictEqNumber | runtime | Error: 'dict' and 'number' values cannot be compared with '==' |
| module/condition/compare | Failure_DictOrderDict | runtime | Error: cannot order 'item' and 'item' values with '<' |
| module/condition/file/filenotexists | ConditionFileNotExists | runtime | Error: Not found: /test/module/condition/file/filenotexists/.build/conditionfilenotexists.test.pr |
| module/goal/missing | GoalCallMissing | runtime | Error: Goal 'NonExistentGoal' not found. |
| module/identity/export | IdentityExport | runtime | Error: Not found: /test/module/identity/export/.build/identityexport.test.pr |
| module/identity/switchdefault | IdentitySwitchDefault | runtime | Expected: %MyIdentity.Name% |
| module/list/query | Cut4 | runtime | Error: order: cannot order 'path' against 'path' — mixed or unordered values |
| module/llm | LlmContinue | runtime | Error: a conversation continues an llm answer; %query% isn't one |
| module/llm | LlmSchema | runtime | Expected: "(not null)" |
| module/loop/foreach/dictionary | ForeachDictionary | runtime | Error: Not found: /test/module/loop/foreach/dictionary/.build/foreachdictionary.test.pr |
| module/math | DoublePlusDecimal_Errors | runtime | Expected: true |
| module/on/error | OnErrorRetry | runtime | Error: intentional failure |
| module/on/error | RetryFirstReturnsRecoveryValue | runtime | Error: boom |
| module/on/error/chain | ErrorChain | runtime | Error: always fails |
| module/on/error/goalfirst | ErrorGoalFirst | runtime | Error: always fails |
| module/on/error/inhandler | ErrorInHandler | runtime | Error: original error |
| module/on/error/mixed | ErrorMixed | runtime | Error: mixed error |
| module/on/error/multilingual | OnErrorMultilingual | runtime | Error: intentional error |
| module/on/error/nested | ErrorNested | runtime | Error: inner error |
| module/on/error/prop | ErrorProps | runtime | Error: bad thing |
| module/on/error/retry | Retry | runtime | Error: retry then goal |
| module/signing | SignAndVerifyRoundTrip | runtime | Error: Data has no signature |
| module/signing | SignedDataSurvivesInList | runtime | Error: Data has no signature |
| module/signing/corruptedsignature | SigningCorruptedSignature | runtime | Error: %data.Signature% holds nothing, so %data.Signature.Signature% has nowhere to be written |
| module/signing/headermismatch | SigningHeaderMismatch | runtime | Error: Not found: /test/module/signing/headermismatch/.build/signingheadermismatch.test.pr |
| module/test/assert | TestAssertFailureSnapshotsVariables | runtime | Expected: true |
| module/test/report | TestReportIncludesCoverageTables | runtime | Expected: ""[{"name":"TrivialPass","path":"_fixtures_pass/trivial.fixture.goal","hash":"616f696a404101877837e02b4c912908e6c7f23085c226a60185 |
| module/test/report | TestReportMasksSensitiveVariables | runtime | Expected: 1 |
| module/test/report | TestReportMasksSensitiveVariablesJunit | runtime | Expected: 1 |
| module/test/report | TestReportRendersFailureWithVariables | runtime | Expected: 1 |
| module/test/report | TestReportWritesJunitXml | runtime | Expected: 1 |
| module/test/run | TestRunEnforcesTimeout | runtime | Expected: true |
| module/test/run | TestRunIsolatesMemoryStackBetweenTests | runtime | Expected: false |
| module/test/run | TestRunReportsAssertionFailure | runtime | Expected: true |
| module/test/tag | TestTagAccumulatesUserTagsOnRun | runtime | Expected: true |
| module/variable/scoping | VariableScoping | runtime | Expected: 999 |
| plan/app-systems/app/type/item | SetChildOnAValueIsAnError | runtime | Expected: "(not null)" |
| app/type/null | MissingVarIsNotNullValue | stale |  |
| app/type/null | NullBareOnJson | stale |  |
| app/type/text | TextLengthOp | stale |  |
| module/error | ThrowAttachesData | stale |  |
| module/file/read | Failure_ParamResolutionError | stale |  |
| module/list/query | Failure_SortMixedIncomparable | stale |  |
| module/llm | LlmProperties | stale |  |
| module/math | Math2 | stale |  |
| module/signing | RoundTripAfterRawSignatureDeletion | stale |  |
| plan/app-systems/app/setting | UnknownSettingIsAnError | stale |  |
| plan/app-systems/app/type/list | TypedListHoldsItsType | stale |  |
| plan/snapshot/module/callback/askvarsresumebindsvalue | Start | stale |  |
| plan/snapshot/module/callback/statefulaskmidgoalbindsvalue | Start | stale |  |
| plan/snapshot/module/callback/statelesscrossgoalresumes | Start | stale |  |

## Round-3 per-test failures (for coder round 3)

| folder | test | class | first failure line |
|---|---|---|---|
| app/actor/switch | ActorSwitch | runtime | Error: Channel 'system' not found |
| app/callstack | Audit | runtime | Expected: "7" |
| app/data | Cut2 | runtime | Expected: %!data% |
| app/data | DataTypeReadsEntity | runtime | Expected: "plain" |
| app/data | Narrow_ChainWideBangBothBranches | runtime | Expected: %!data% |
| app/data | Plane_DataKeyVsProperty | runtime | Expected: 10 |
| app/data | ReadConfigJson_UntouchedIsJsonString | runtime | Expected: "json" |
| app/data | ReadCsv_LandsAsTable | runtime | Expected: "csv" |
| app/permission/grantastorespersisted | Start | runtime | Error: 'app.channel.type.goal.this' has no wire contract — it declares no [Out]/[Store] face and is not meant to cross the wire. Write what  |
| app/type/dict | DictIsItemKeepsNoOrder | runtime | Error: order: cannot order 'dict' against 'dict' — mixed or unordered values |
| app/type/item | BareLiteralJudgedByForm | runtime | Expected: "date" |
| app/type/kind/strict | SetAsImageStrictNoKind | runtime | Expected: (null) |
| app/type/kind/strict | SetAsTextUppercase | runtime | Expected: (null) |
| app/type/kind/strict | SetAsTextWithMdExtension | runtime | Expected: (null) |
| app/type/kind/strict | SetIntLiteralIsNumberInt | runtime | Expected: "int" |
| app/type/list | ListIsItemSortsAsBefore | runtime | Error: order: No item has a field '[3, 1, 2]' |
| module/channel/goalchannelrecursion | Start | runtime | Expected: "ChannelNotFound" |
| module/condition/compare | Cut5 | runtime | Error: cannot order 'item' and 'item' values with '>' |
| module/condition/compare | Failure_DictEqNumber | runtime | Error: 'dict' and 'number' values cannot be compared with '==' |
| module/condition/compare | Failure_DictOrderDict | runtime | Error: cannot order 'item' and 'item' values with '<' |
| module/llm | LlmContinue | runtime | Error: a conversation continues an llm answer; %query% isn't one |
| module/llm | LlmSchema | runtime | Expected: "(not null)" |
| module/loop/foreach/dictionary | ForeachDictionary | runtime | Expected: "3" |
| module/math | DoublePlusDecimal_Errors | runtime | Error: Not found: /test/module/math/.build/doubleplusdecimal_errors.test.pr |
| module/on/error | OnErrorRetry | runtime | Error: intentional failure |
| module/on/error/goalfirst | ErrorGoalFirst | runtime | Error: always fails |
| module/signing/headermismatch | SigningHeaderMismatch | runtime | Error: Not found: /test/module/signing/headermismatch/.build/signingheadermismatch.test.pr |
| module/test/assert | TestAssertFailureSnapshotsVariables | runtime | Error: No item has a field 'Error.Variables.foo' |
| module/test/discover | TestDiscoverReportsStaleWhenPrMissing | runtime | Expected: true |
| module/test/report | TestReportIncludesCoverageTables | runtime | Expected: ""[{"name":"TrivialPass","path":"/module/test/report/_fixtures_pass/trivial.fixture.goal","hash":"616f696a404101877837e02b4c912908 |
| module/test/report | TestReportMasksSensitiveVariables | runtime | Expected: ""[{"name":"SensitiveFail","path":"/module/test/report/_fixtures_sensitive/sensitivefail.fixture.goal","hash":"e0bd94cb525644f9b2c |
| module/test/report | TestReportMasksSensitiveVariablesJunit | runtime | Expected: ""<?xml version="1.0" encoding="UTF-8"?> |
| module/test/tag | TestTagAccumulatesUserTagsOnRun | runtime | Expected: true |
| app/permission/authorize/statefulauthorizegrantsandcontinues | Start | stale |  |
| app/type/kind/strict | SetAsImageGifStrictMatching | stale |  |
| app/type/kind/strict | SetAsImageGifStrictMismatch | stale |  |
| app/type/kind/strict | SetAsTextDefaultNoValidation | stale |  |
| app/type/kind/strict | SetAsTextSlashMarkdownNormalises | stale |  |
| app/type/kind/strict | SetAsTextSlashMarkdownStrictUnverifiable | stale |  |
| app/type/null | MissingVarIsNotNullValue | stale |  |
| app/type/null | NullBareOnJson | stale |  |
| app/type/text | TextLengthOp | stale |  |
| module/error | ThrowAttachesData | stale |  |
| module/file/read | Failure_ParamResolutionError | stale |  |
| module/list/query | Failure_SortMixedIncomparable | stale |  |
| module/llm | LlmProperties | stale |  |
| module/math | Math2 | stale |  |
| module/signing | RoundTripAfterRawSignatureDeletion | stale |  |
| module/variable/scoping | VariableScoping | stale |  |
| plan/app-systems/app/setting | UnknownSettingIsAnError | stale |  |
| plan/app-systems/app/type/list | TypedListHoldsItsType | stale |  |
| plan/snapshot/module/callback/askvarsresumebindsvalue | Start | stale |  |
| plan/snapshot/module/callback/statefulaskmidgoalbindsvalue | Start | stale |  |
| plan/snapshot/module/callback/statelesscrossgoalresumes | Start | stale |  |
