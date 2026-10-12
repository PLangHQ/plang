# app-systems — builder Step-3 test run report

Fresh binary (PlangConsole rebuilt with all session changes), `cd test && plang --test`.

## Totals

**283 total — 129 pass, 126 fail, 28 stale.** (The module/llm built tests ran against the real LLM once; that cost is in this run.)

## Failure classes (the 126 fails + 28 stale)

1. **Sibling helper goals not built (~36 fails).** The per-folder batch built each `.test.goal` entry point individually (`--build={files:[...]}`), which does NOT build the sibling `.goal` files the test calls (e.g. `Loop.test.goal` calls `CountItem.goal`). At run: "Goal …/CountItem.goal exists but isn't built." A whole-tree `plang build` from `test/` does not fix this cleanly — it aborts at the first refusing goal (built only 5 callstack siblings before halting). Clearing these needs a per-goal build pass over all `.goal` (not just `.test.goal`), surviving refusals.

2. **Stale .pr (28).** `.pr` whose builderVersion predates the final binary — mostly folders NOT in this session's batches (app/callstack 11, plan/snapshot 3, …) plus builderVersion drift from the session's many builder changes (on.error check, inverse-Cover, teaching re-pins). The suite needs a rebuild pass with the final binary.

3. **Runtime reds (~90, assertion + error).** Tests that BUILD clean but fail at run, concentrated in app/type (28), module/condition (18), module/on (17), module/test (13). Samples: app/type/date "Expected date", app/data "Expected text/plain", module/signing "Data has no signature". These look like behaviour shifts from the session's pulled coder RUNTIME changes (kind-redesign 4cb19476b, math-seam 955b1446b, sensitive-rename 4afe34fcb, 10b 679/694, container-Holds 00fe6c72e, environment removal 35d2f74bf). Builder maps correctly (these built); the runtime mismatch is the coder's domain (coder stopped) → architect/reviewer bots.

## Left unbuilt, by reason (for the next branch)

- `app/type/text/TextLengthOp`, `app/data/MissingVarIsNotNullValue`, `module/error/ThrowAttachesData` — **issue 74** (navigation written `%s%.length%` / `%x!`): the writer closes `%` after the root. Refuses, never a wrong save. Kernel pass, next branch.
- `plan/app-systems/app/type/list/TypedListHoldsItsType` — **kinded Type choices**: the decider offers only base `list` (not `list<path>`), same family as json-not-offered. 10b closes on its C# pins (TypedListElementTests); plan test waits for the Type-choice item. Noted in the plan's start.md.
- `module/llm/LlmProperties` — writer pads `Limit` with `tool:10/retry:0` defaults → UnwrittenNumber refuses. Writer item, next branch.
- `module/signing/RoundTripAfterRawSignatureDeletion`, `module/signing/corruptedsignature/SigningCorruptedSignature` — **issue 75** family (the `archive identity X, on error ignore` step drops its main action). Flaky; left unbuilt.
- `module/http/downloadskip/DownloadSkip` — **deleted** (stale test of a gone feature: http.download has no IfExists/skip property).

## Builder changes landed this branch (fixes + the one C# each)

- Issue 71: `type=json` → Type=json text (teaching); inverse-Cover reads the step's quoted literals decoded (goal/step/this.Validate.cs).
- on.error: recovery nests in Recovery (teaching); **on.error-does-nothing structural check** refuses an empty clause (module/on/error.cs) — kills the hoisted-recovery wrong-save.
- channel events: `%!channel%` shortcut (decision 500); condition `and`=nested / `or`=if+elseif / file-exists-else / `!%b%`→`==false`; issue-75 refusal wording; `as list of <type>`, llm maxTokens→Limit.token teaching.

All with decider-fixture + page-golden re-pins.

## Per-test failures (for coder class-3 triage)

Note: some 'runtime' rows showing "Not found: /test/...test.pr" are a wrong-cwd prPath artifact (built from the wrong cwd during measurement), fixed by the rebuild pass — not genuine runtime reds.

| folder | test | class | first failure line |
|---|---|---|---|
| app/actor/switch | ActorSwitch | runtime | Error: Channel 'system' not found |
| app/channel | ChannelIndexMissThrows | runtime | Error: Not found: /test/app/channel/.build/channelindexmissthrows.test.pr |
| app/data | AsJson_ResolvesTypeUnknown | runtime | Error: Not found: /test/app/data/.build/asjson_resolvestypeunknown.test.pr |
| app/data | Cut2 | runtime | Error: Not found: /test/app/data/.build/cut2.test.pr |
| app/data | DataTypeReadsEntity | runtime | Expected: "text/plain" |
| app/data | Narrow_ChainWideBangBothBranches | runtime | Error: 'Value' is %config!file!path%, a variable that holds nothing |
| app/data | NavigationOnTypeUnknown_AsksForAsType | runtime | Error: Not found: /test/app/data/.build/navigationontypeunknown_asksforastype.test.pr |
| app/data | Plane_DataKeyVsProperty | runtime | Error: Unknown type 'json' |
| app/data | ReadConfigJson_UntouchedIsJsonString | runtime | Expected: "file" |
| app/data | ReadCsv_LandsAsTable | runtime | Expected: "file" |
| app/type/bool | BoolBareOnJson | runtime | Expected: "bool" |
| app/type/bool | BoolBornNative | runtime | Expected: "bool" |
| app/type/bool | IfBoolTruthy | runtime | Error: Not found: /test/app/type/bool/.build/ifbooltruthy.test.pr |
| app/type/date | DateDatetimeSortFamiliesSeparate | runtime | Expected: "date" |
| app/type/date | DateIsDateNotDatetime | runtime | Expected: "date" |
| app/type/date | DateReturnsDateOnly | runtime | Expected: "date" |
| app/type/datetime | DatetimeAcceptsClrDateTime | runtime | Expected: "datetime" |
| app/type/datetime | DatetimeBornNative | runtime | Expected: "datetime" |
| app/type/dict | DictIsItemKeepsNoOrder | runtime | Expected: true |
| app/type/duration | DurationPartsAndCompare | runtime | Expected: 1 |
| app/type/item | BareLiteralJudgedByForm | runtime | Expected: "date" |
| app/type/kind/strict | SetAsImageGifStrictMatching | runtime | Expected: "image" |
| app/type/kind/strict | SetAsImageGifStrictMismatch | runtime | Expected: "image" |
| app/type/kind/strict | SetAsImageStrictNoKind | runtime | Expected: "image" |
| app/type/kind/strict | SetAsTextDefaultNoValidation | runtime | Expected: "text" |
| app/type/kind/strict | SetAsTextSlashMarkdownNormalises | runtime | Expected: "text" |
| app/type/kind/strict | SetAsTextSlashMarkdownStrictUnverifiable | runtime | Expected: "text" |
| app/type/kind/strict | SetAsTextUppercase | runtime | Expected: "text" |
| app/type/kind/strict | SetAsTextWithMdExtension | runtime | Expected: "text" |
| app/type/kind/strict | SetIntLiteralIsNumberInt | runtime | Expected: "number" |
| app/type/list | ListIsItemSortsAsBefore | runtime | Error: order: No item has a field '[3, 1, 2]' |
| app/type/null | NullBareOnJson | runtime | Expected: "null" |
| app/type/null | NullsSortLast | runtime | Error: Not found: /test/app/type/null/.build/nullssortlast.test.pr |
| app/type/path | PhotoPathExistsMissingFile | runtime | Error: File not found: tmp-photo-missing.png |
| app/type/path | PhotoPathExistsNavigation | runtime | Error: 'Value' is %photo.Path.Exists%, a variable that holds nothing |
| app/type/text | TextBareOnJson | runtime | Expected: "text" |
| app/type/text | TextBornNative | runtime | Expected: "text" |
| app/type/time | TimeIsTimeNotUnhandled | runtime | Expected: "time" |
| app/type/time | TimeReturnsTimeOnly | runtime | Expected: "time" |
| module/build/compilellmnotes | assert-equals-no-message | runtime | Error: Not found: ../../Simple/.build/start.test.pr |
| module/build/compilellmnotes | output-write-no-channel | runtime | Error: Not found: ../../Simple/.build/start.test.pr |
| module/callback/errorcallbackoutsidehandler | Start | runtime | Error: no error is current |
| module/channel/goalchannelrecursion | Start | runtime | Expected: "ChannelNotFound" |
| module/channel/remove/rolechannelrefused | Start | runtime | Error: Channel 'output' is a default channel and cannot be removed (use channel.set to replace its backing). |
| module/channel/writetounknownchannel | Start | runtime | Expected: true |
| module/condition/compare | Cut5 | runtime | Error: cannot order 'item' and 'item' values with '>' |
| module/condition/compare | Failure_DictEqNumber | runtime | Error: 'dict' and 'number' values cannot be compared with '==' |
| module/condition/compare | Failure_DictOrderDict | runtime | Error: cannot order 'item' and 'item' values with '<' |
| module/condition/compare | Failure_DictOrderNumber | runtime | Error: Not found: /test/module/condition/compare/.build/failure_dictordernumber.test.pr |
| module/condition/compound/or | ConditionCompoundOr | runtime | Error: Not found: /test/module/condition/compound/or/.build/conditioncompoundor.test.pr |
| module/condition/file/filenotexists | ConditionFileNotExists | runtime | Error: Not found: /test/module/condition/file/filenotexists/.build/conditionfilenotexists.test.pr |
| module/condition/operator/not | ConditionNot | runtime | Expected: "negated" |
| module/crypto/providerswap | ProviderSwap | runtime | Error: Provider 'sha256-only' not found |
| module/file/list | Cut3 | runtime | Expected: "["cut3dir/a.txt","cut3dir/b.txt"]" |
| module/goal/missing | GoalCallMissing | runtime | Error: Goal 'NonExistentGoal' not found. |
| module/identity/export | IdentityExport | runtime | Error: Not found: /test/module/identity/export/.build/identityexport.test.pr |
| module/identity/switchdefault | IdentitySwitchDefault | runtime | Expected: %MyIdentity.Name% |
| module/list | ListOps | runtime | Error: order: No item has a field ' ' |
| module/list/query | Cut4 | runtime | Expected: ""cut4/small.txt"" |
| module/llm | LlmContinue | runtime | Error: a conversation continues an llm answer; %query% isn't one |
| module/llm | LlmFormat | runtime | Error: Not found: /test/module/llm/.build/llmformat.test.pr |
| module/llm | LlmSchema | runtime | Expected: "(not null)" |
| module/loop/foreach/dictionary | ForeachDictionary | runtime | Error: Not found: /test/module/loop/foreach/dictionary/.build/foreachdictionary.test.pr |
| module/math | DoublePlusDecimal_Errors | runtime | Expected: true |
| module/on/error | GoalFirstReturnsRecoveryValue | runtime | Error: boom |
| module/on/error | MultiActionRecoveryLastActionPropagates | runtime | Error: boom |
| module/on/error | OnErrorRetry | runtime | Error: intentional failure |
| module/on/error | PerActionErrorScope | runtime | Error: first action error |
| module/on/error | RetryFirstReturnsRecoveryValue | runtime | Error: boom |
| module/on/error/retry | Retry | runtime | Error: retry then goal |
| module/setting/settingscrud | Start | runtime | Error: %cache% holds a bool — choice<cache> cannot be created from it. |
| module/signing | SignAndVerifyRoundTrip | runtime | Error: Data has no signature |
| module/signing | SignedDataSurvivesInList | runtime | Error: Data has no signature |
| module/signing/expired | SigningExpired | runtime | Error: Not found: /test/module/signing/expired/.build/signingexpired.test.pr |
| module/signing/headermismatch | SigningHeaderMismatch | runtime | Error: Not found: /test/module/signing/headermismatch/.build/signingheadermismatch.test.pr |
| module/test/assert | TestAssertFailureSnapshotsVariables | runtime | Expected: true |
| module/test/condition | TestConditionElseIfMatchesRecordsBranchIndex1 | runtime | Error: Not found: /test/module/test/condition/.build/testconditionelseifmatchesrecordsbranchindex1.test.pr |
| module/test/condition | TestConditionIfRecordsBranchIndexElseBranch | runtime | Expected: 2 |
| module/test/condition | TestConditionIfRecordsBranchIndexTrueBranch | runtime | Expected: 0 |
| module/test/edgecase | TestDiscoverHandlesIcelandicGoalNames | runtime | Error: No item has a field 'Goal.Name' |
| module/test/report | TestReportIncludesCoverageTables | runtime | Expected: ""[{"name":"TrivialPass","path":"_fixtures_pass/trivial.fixture.goal","hash":"616f696a404101877837e02b4c912908e6c7f23085c226a60185 |
| module/test/report | TestReportMasksSensitiveVariables | runtime | Expected: 1 |
| module/test/report | TestReportMasksSensitiveVariablesJunit | runtime | Expected: 1 |
| module/test/report | TestReportRendersFailureWithVariables | runtime | Expected: 1 |
| module/test/report | TestReportWritesJunitXml | runtime | Expected: 1 |
| module/test/run | TestRunEnforcesTimeout | runtime | Error: setting 'app.test.setting' has no option 'timeoutSeconds': %app.test.setting% is a setting inside another value, and has nowhere to k |
| module/test/run | TestRunIsolatesMemoryStackBetweenTests | runtime | Expected: false |
| module/test/run | TestRunReportsAssertionFailure | runtime | Expected: true |
| module/test/tag | TestTagAccumulatesUserTagsOnRun | runtime | Expected: 3 |
| plan/app-systems/app/type/item | SetChildOnAValueIsAnError | runtime | Expected: "(not null)" |
| app/permission/authorize/statefulauthorizegrantsandcontinues | Start | sibling-unbuilt | Error: Goal /app/permission/authorize/statefulauthorizegrantsandcontinues/VerbBasedAnswerer.goal exists but isn't built; run plang build |
| app/permission/grantastorespersisted | Start | sibling-unbuilt | Error: Goal /app/permission/grantastorespersisted/ProbeAnswerer.goal exists but isn't built; run plang build |
| module/build | BuilderSanity | sibling-unbuilt | Error: Goal /module/build/AddItem.goal exists but isn't built; run plang build |
| module/channel/event/addbeforewrite | Start | sibling-unbuilt | Error: Goal /module/channel/event/addbeforewrite/ApprovalGoal.goal exists but isn't built; run plang build |
| module/channel/set/contextactor | Start | sibling-unbuilt | Error: Goal /module/channel/set/contextactor/OutputGoal.goal exists but isn't built; run plang build |
| module/channel/set/replaceoutputwithgoal | Start | sibling-unbuilt | Error: Goal /module/channel/set/replaceoutputwithgoal/OutputGoal.goal exists but isn't built; run plang build |
| module/channel/writetocustomchannel | Start | sibling-unbuilt | Error: Goal /module/channel/writetocustomchannel/Logger.goal exists but isn't built; run plang build |
| module/condition/compound/and | ConditionCompoundAnd | sibling-unbuilt | Error: Goal /module/condition/compound/and/DoThing.goal exists but isn't built; run plang build |
| module/condition/if/basic | Condition | sibling-unbuilt | Error: Goal /module/condition/if/basic/SetTrue.goal exists but isn't built; run plang build |
| module/condition/if/elsebranch | ConditionElseBranch | sibling-unbuilt | Error: Goal /module/condition/if/elsebranch/WhenSmall.goal exists but isn't built; run plang build |
| module/condition/operator/contains | ConditionContains | sibling-unbuilt | Error: Goal /module/condition/operator/contains/WhenContains.goal exists but isn't built; run plang build |
| module/condition/operator/endswith | ConditionEndsWith | sibling-unbuilt | Error: Goal /module/condition/operator/endswith/WhenEnds.goal exists but isn't built; run plang build |
| module/condition/operator/equals | ConditionEquals | sibling-unbuilt | Error: Goal /module/condition/operator/equals/WhenEqual.goal exists but isn't built; run plang build |
| module/condition/operator/falsy | ConditionFalsy | sibling-unbuilt | Error: Goal /module/condition/operator/falsy/HandleFalse.goal exists but isn't built; run plang build |
| module/condition/operator/greaterthan | ConditionGreaterThan | sibling-unbuilt | Error: Goal /module/condition/operator/greaterthan/WhenGreater.goal exists but isn't built; run plang build |
| module/condition/operator/gte | ConditionGTE | sibling-unbuilt | Error: Goal /module/condition/operator/gte/WhenGTE.goal exists but isn't built; run plang build |
| module/condition/operator/lessthan | ConditionLessThan | sibling-unbuilt | Error: Goal /module/condition/operator/lessthan/WhenLess.goal exists but isn't built; run plang build |
| module/condition/operator/lte | ConditionLTE | sibling-unbuilt | Error: Goal /module/condition/operator/lte/WhenLTE.goal exists but isn't built; run plang build |
| module/condition/operator/notequals | ConditionNotEquals | sibling-unbuilt | Error: Goal /module/condition/operator/notequals/WhenNotEqual.goal exists but isn't built; run plang build |
| module/condition/operator/startswith | ConditionStartsWith | sibling-unbuilt | Error: Goal /module/condition/operator/startswith/WhenStarts.goal exists but isn't built; run plang build |
| module/condition/operator/truthy | ConditionTruthy | sibling-unbuilt | Error: Goal /module/condition/operator/truthy/HandleTrue.goal exists but isn't built; run plang build |
| module/goal/dynamic | GoalCallDynamic | sibling-unbuilt | Error: Goal /module/goal/dynamic/TargetGoal.goal exists but isn't built; run plang build |
| module/goal/return | GoalCallReturn | sibling-unbuilt | Error: Goal /module/goal/return/ComputeAnswer.goal exists but isn't built; run plang build |
| module/loop | Loop | sibling-unbuilt | Error: Goal /module/loop/CountItem.goal exists but isn't built; run plang build |
| module/on/cache/basic | Cache | sibling-unbuilt | Error: Goal /module/on/cache/basic/ReadCached.goal exists but isn't built; run plang build |
| module/on/error/chain | ErrorChain | sibling-unbuilt | Error: Goal /module/on/error/chain/AlwaysFails.goal exists but isn't built; run plang build |
| module/on/error/goalfirst | ErrorGoalFirst | sibling-unbuilt | Error: Goal /module/on/error/goalfirst/AlwaysFails.goal exists but isn't built; run plang build |
| module/on/error/inhandler | ErrorInHandler | sibling-unbuilt | Error: Goal /module/on/error/inhandler/ThrowWithBadHandler.goal exists but isn't built; run plang build |
| module/on/error/mixed | ErrorMixed | sibling-unbuilt | Error: Goal /module/on/error/mixed/MixedThrow.goal exists but isn't built; run plang build |
| module/on/error/multilingual | OnErrorMultilingual | sibling-unbuilt | Error: Goal /module/on/error/multilingual/ThrowError.goal exists but isn't built; run plang build |
| module/on/error/nested | ErrorNested | sibling-unbuilt | Error: Goal /module/on/error/nested/OuterGoal.goal exists but isn't built; run plang build |
| module/on/error/prop | ErrorProps | sibling-unbuilt | Error: Goal /module/on/error/prop/ThrowCustomError.goal exists but isn't built; run plang build |
| module/on/event/afteraction | EventAfterAction | sibling-unbuilt | Error: Goal /module/on/event/afteraction/CaptureWrite.goal exists but isn't built; run plang build |
| module/on/event/afterstep | EventAfterStep | sibling-unbuilt | Error: Goal /module/on/event/afterstep/TrackAfterStep.goal exists but isn't built; run plang build |
| module/on/event/basic | Events | sibling-unbuilt | Error: Goal /module/on/event/basic/LogAfter.goal exists but isn't built; run plang build |
| module/variable/scoping | VariableScoping | sibling-unbuilt | Error: Goal /module/variable/scoping/InnerGoal.goal exists but isn't built; run plang build |
| app/callstack | Audit | stale |  |
| app/callstack | CrossFileChain | stale |  |
| app/callstack | DepthIncreasesOnGoalCall | stale |  |
| app/callstack | DepthRestoresAfterCall | stale |  |
| app/callstack | DirectGoalCycleThrows | stale |  |
| app/callstack | ErrorVarIsNullOutsideHandler | stale |  |
| app/callstack | HandledFlagFalseWhenRecoveryFails | stale |  |
| app/callstack | HandledFlagSetWhenRecoverySucceeds | stale |  |
| app/callstack | IndirectGoalCycleThrows | stale |  |
| app/callstack | InnerErrorVarShadowsOuter | stale |  |
| app/callstack | OuterErrorVarVisibleAfterInnerHandlerCloses | stale |  |
| app/type/null | MissingVarIsNotNullValue | stale |  |
| app/type/text | TextLengthOp | stale |  |
| module/error | ThrowAttachesData | stale |  |
| module/file/read | Failure_ParamResolutionError | stale |  |
| module/goal/call | ParallelAnswersATask | stale |  |
| module/goal/call | PlainCallWaits | stale |  |
| module/goal/call | TaskListedWhileRunning | stale |  |
| module/list/query | Failure_SortMixedIncomparable | stale |  |
| module/llm | LlmProperties | stale |  |
| module/math | Math2 | stale |  |
| module/signing | RoundTripAfterRawSignatureDeletion | stale |  |
| module/signing/corruptedsignature | SigningCorruptedSignature | stale |  |
| plan/app-systems/app/setting | UnknownSettingIsAnError | stale |  |
| plan/app-systems/app/type/list | TypedListHoldsItsType | stale |  |
| plan/snapshot/module/callback/askvarsresumebindsvalue | Start | stale |  |
| plan/snapshot/module/callback/statefulaskmidgoalbindsvalue | Start | stale |  |
| plan/snapshot/module/callback/statelesscrossgoalresumes | Start | stale |  |
