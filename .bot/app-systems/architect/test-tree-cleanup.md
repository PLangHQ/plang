# The plang test tree: what stays, what goes, what moves (2026-10-12)

## Why

Ingi stopped the rebuild: "I am not confident we need all the tests, are there duplicates, and are we missing some. It is all a bit black box to me." The architect read every test's header, and its steps where the header wasn't enough. Then Ingi: "I trust you which tests should stay and which should be remove, you can send the instructions to builder, he can then build them and test them."

The tree had 347 test files. **63 go** (dead, stale, or a weaker copy of another), **about 80 of the kept move** to the path of what they test (decision 670 and the existing rule), and **284 stay** in all. Then the kept tree is built and run.

## How

- **Step 1, one commit, no builds:** do the deletes and the moves below. A deleted test takes with it its folder (a folder-per-test layout), its `.build/` output, and any helper goal or fixture that nothing else uses. A moved test takes its helper goals and fixtures with it. Its old `.pr` is deleted, never hand-edited, and it's rebuilt at the new path in step 2. Grep for callers of every moved or deleted goal (`call /…`, the plans' `start.goal`) and fix each. Report the commit to the architect.
- **Step 2, batches:** build every kept test, one folder per batch as before, with a start and end message to the architect. A refusal is a build bug for you (teaching yours; Cover, writer or formal-reader changes come to the architect with evidence; the locked core is a proposal).
- **Step 3, run:** `cd test && ../PlangConsole/bin/Debug/net10.0/plang --test` on a fresh binary. Report pass, fail and unbuilt per folder. A **runtime** failure is listed for the architect with its message; don't send it to the coder (the coder is stopped until Ingi says).
- The `module/llm/*` tests call a real LLM when they run. Build them, but tell the architect what a run of them costs before running them more than once.

## Delete (63)

Dead or stale: they test nothing, or an action that no longer exists.
- `module/http/{configbaseurl,configheaders,getrequest,postrequest,signedrequest,streamcallback,unsignedrequest,uploadfile}/`: switched off (httpbin.org 503), one placeholder step each.
- `app/data/HttpStatusRead_DoesNotMaterialiseBody`: switched off; the real check is C# HttpChannelTests.
- `app/data/DebugModePayloadIncludesNonOut`: a no-op ("covered by C# tests").
- `module/mock/Mock`, `module/builder/MockLlmSmoke`: `mock.intercept` is gone (mocks are `on` bindings now).
- `module/builder/getactions/`, `module/builder/gettypeinfo/`, `module/builder/ValidateActionsOnly`: the build module has no getActions, types or validate.
- `module/test/integration/TestSystemTestGoalReportsTimeout`, `…IncludesAllThreePhases`: they assert `/system/test.goal` contains `test.run`; it says `test.start` (`…ReportsTimeoutViaTestStart` covers it).
- `module/on/timeout/TimeoutOnSlowAction`: no timeout in its steps (`timer.sleep ms=2000`, the old `ms`), so it can't fail.
- `module/goal/call/GoalBuildAndRun`: a set and an assert; no call.
- `app/startupparams/StartupParams`: a set and an interpolation; no startup parameter.
- `app/callstack/Diffs`: asserts null without the debug flag.
- `plan/typedreturn/` (the whole folder, 12 tests, its `start.md` and `start.goal`): an old plan whose tests use `builder.goals` and `goal.getTypes`, which don't exist. Its two C# tests (`BuilderValidate_*`) are the coder's, later.

Weaker copies: the stronger test of the pair stays.
- `app/actor/context/ActorContext` (`%!app%`, `%!call%` not null): every test reads those.
- `app/actor/datasource/ActorDatasource` = `module/setting/settingscrud`.
- `app/returnmapping/ReturnMapping` = `module/goal/return/GoalCallReturn`.
- `app/recursiondepth/RecursionDepthLimit` = `app/callstack/DirectGoalCycleThrows`.
- `app/channel/channelwritethroughaccessor/` and `module/channel/add/basic/` = `module/channel/writetocustomchannel`.
- `app/data/ValueVsPropertiesDisjoint` = `app/data/Plane_DataKeyVsProperty`.
- `module/cache/key/CacheKey` = `module/cache/basic/Cache`; `module/on/cache/CacheOnFileRead` (two reads compare equal with or without a cache) is weaker still.
- `module/code/load/LoadDllOverwritesBuiltIn` and `LoadDllRegistersType` (identical steps: load, no error) < `plan/app-systems/app/type/PluginTypeJoinsTheApp`.
- `module/error/handling/ErrorHandling` = the first half of `module/error/mixed/ErrorMixed`.
- `module/error/ordering/ErrorOrdering` (asserts a string contains "error") < `module/error/chain/ErrorChain`.
- `module/on/error/OnErrorCallGoal` = `module/error/prop/ErrorProps`.
- `module/signing/SensitivePropertyDoesNotLeak`: the same steps as `RoundTripAfterRawSignatureDeletion`, and no leak check in it.
- `type/PolymorphicMathAddHasNoKind` and `app/type/scalar/stage7/NumberWideningCrossesKinds` = `module/math/Cut1_LiteralKindArithmeticOutput` (int + decimal).
- `type/SetDecimalLiteralStampsKind`: set 3.5, assert 3.5.
- `app/type/scalar/stage1/NumberArithmeticUnchanged` = `module/math/Math`.
- `app/type/scalar/stage2/TextReturnsString` = `TextBareOnJson`.
- `app/type/scalar/stage3/DatetimeReturnsDateTimeOffset` = `DatetimeBornNative`.
- `app/type/scalar/stage4/DurationBornNative`, `DurationBareOnJson`, `DurationReturnsTimeSpan` = `DurationPartsAndCompare` + `app/type/duration/DurationRoundTrip`.
- `app/type/scalar/stage5/BoolReturnsBool` = `BoolBareOnJson`; `AssertIsTrueOnBoolResult` = `module/assert/AssertComplete`.
- `app/type/scalar/stage6/NullValueIsSingleton` = `MissingVarIsNotNullValue`.
- `app/type/scalar/stage7/DictStillThrowsOnSort` = `stage1/DictIsItemKeepsNoOrder`; `FiveEqualsFiveStillCoerces` = `module/condition/compare/Cut1`.

## Move (to the path of what each tests)

- **Done plans (decision 670):** `plan/task/module/**` → `module/**` (11) and `plan/list-query/module/**` → `module/**` (20), same relative paths. Each plan keeps `start.md` and `start.goal`; its `start.goal` calls the new paths. `plan/app-systems` stays (10b is open). `plan/snapshot` stays (a plan only).
- **on.error lives in `on`:** from `module/error/`, everything except `ThrowAttachesData` (which is `error.throw`) → `module/on/error/`: `chain/`, `goalfirst/`, `inhandler/`, `mixed/`, `nested/`, `prop/`, `multilingual/`, `GoalFirstReturnsRecoveryValue`, `RetryFirstReturnsRecoveryValue`, `MultiActionRecoveryLastActionPropagates`. Also `app/retry/Retry` → `module/on/error/retry/`.
- **cache, event and timeout live in `on`:** `module/cache/basic/` → `module/on/cache/basic/`; `module/event/*` → `module/on/event/*`; `app/callstack/TimeoutProducesCancellation` → `module/on/timeout/`.
- **debug.tag:** `app/callstack/TagBareLabelWritesTrue`, `TagWritesPairsOntoCurrentCall` → `module/debug/tag/`.
- **The build module is `build`:** `module/builder/parsegoal/` → `module/build/goals/`; `module/builder/compilellmnotes/` → `module/build/compilellmnotes/`.
- **channel.set:** `module/channel/add/withconfig/` → `module/channel/set/withconfig/` (channel.set takes Buffer, Timeout, Mime).
- **Types by type, not by an old plan's stage:** `app/type/scalar/stage1…stage7/*` → `app/type/<type>/`: the text tests → `app/type/text/`, date → `date/`, datetime → `datetime/`, time → `time/`, duration → `duration/`, bool → `bool/`, null → `null/`, `DictIsItemKeepsNoOrder` → `dict/`, `ListIsItemSortsAsBefore` → `list/`, `JsonReadIsItemUntilTouch` → `item/`, `BareLiteralJudgedByForm` → `item/`; `stage*` folders then go. `type/ReadPhotoStampsImage` → `app/type/image/`; `type/PhotoPathExists*` → `app/type/path/`; the top-level `type/` folder then goes.

## Stays

Everything else (284 in all), at its path.

## Missing (for later, not the builder's now)

No plang test for: crypto encrypt and decrypt; environment.start; module add and remove; on.cancel, on.unbind and on.input; identity list and unarchive; code list, remove and setDefault; the os modules (browser, screen, terminal, window: C# only). A mock is now an `on` binding with `cancel it, value = …`, so the deleted mock tests' intent becomes an on.cancel test when these are written.
