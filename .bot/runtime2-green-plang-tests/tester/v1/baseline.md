# Phase 2 Baseline — v1

Timestamp: 2026-04-21T13:44:18.031810+00:00
Artefacts: `Tests/.test/junit.xml`, `/tmp/plang_test_post.log`, `/tmp/per_folder_build_out.log`
Commands:
- Build: per-folder loop via `/tmp/per_folder_build.sh` (135/141 folders ok, 6 fail)
- Test:  `plang --test={"format":"junit","timeout":5}` from `/workspace/plang/Tests/`
- Runner summary: `Test summary: 161 total, 109 pass, 48 fail, 0 timeout, 4 stale, 0 skipped`

## Why a per-folder build loop

`plang build` run monolithically from `Tests/` fail-fasts on the first LLM/ActionNotFound failure. That means one bad goal blocks the other 296 from rebuilding. Since every test folder already carries its own `.build/app.pr` (each test is its own plang app), we can build them independently — a `List/` build failure no longer prevents `Math/` from rebuilding.

## Totals

| Category | Count | Notes |
|---|---|---|
| Pass | 109 | |
| Fail — assertion | 19 | test assertions failed at runtime |
| Fail — runtime error | 29 | handler error before/during assertion |
| Timeout | 0 | 5s per-test timeout |
| Stale | 4 | hash mismatch; build failed or wasn't re-run |
| **Total tests** | **161** | |
| Build-failure folders | 6 | see below |

## Pre-baseline vs post-restructure

| | Pre | Post | Delta |
|---|---|---|---|
| Total | 162 | 161 | -1 (FromJson removed) |
| Pass | 96 | 109 | +13 |
| Fail | 48 | 48 | +0 |
| Stale | 18 | 4 | -14 |

Restructure added +13 passes — improved tests via per-folder rebuilds that wouldn't happen under the monolithic build. Fail count unchanged, which is what we'd hope for a pure restructure.

## Build failures (6) — root-causes for Phase 4

| Folder | Error (truncated) |
|---|---|
| `Tests/Modules/Http/DownloadFile` | Actions not found: text.write: Module 'text' not found. Did you mean: test, list, event, goal, output? |
| `Tests/Modules/List` | Response is not valid JSON |
| `Tests/Modules/Math` | Response is not valid JSON |
| `Tests/Modules/Signing/Expired` | Actions not found: timeout.after.after: Module 'timeout.after' not found. Did you mean: timeout, timer, output, identity, module? |
| `Tests/Modules/Signing/NonceReplay` | Actions not found: signing.error.handle: Module 'signing' exists but action 'error.handle' not found. Did you mean: sign, verify? |
| `Tests/Modules/Signing/TimedOut` | Actions not found: timeout.after.after: Module 'timeout.after' not found. Did you mean: timeout, timer, output, identity, module? |

**Root-cause classes (for architect Phase 4):**
1. **Missing module `text`** — `Tests/Modules/Http/DownloadFile` uses `text.write`. No `text` module in the registry (available: test, list, event, goal, output). Either the module was removed or the test prompt maps wrong.
2. **Builder modifier-routing regression** — `Signing/Expired` and `Signing/TimedOut` produce `timeout.after.after` (double suffix). `Signing/NonceReplay` produces `signing.error.handle` (wrong module base). The `error.handle` / `timeout.after` modifier actions aren't being emitted as separate elements in the flat action list; the LLM concatenates the modifier onto the preceding action's module/action string. This is a BuildGoal.llm prompt bug.
3. **LLM JSON-parse on Modules/List and Modules/Math** — consistent `Response is not valid JSON` across retries. Not a flake. Something in the goal content forces the LLM into a bad serialization. Phase 4 should `!debug=BuildGoal:6` to see the raw response.

## Fail — assertion (19)

| Test | Expected vs Actual |
|---|---|
| `App/SetupGoal/Start.test.goal` | Expected: True, Actual: (null) |
| `App/StepResult/StepResult.test.goal` | Expected non-null value — Expected: "(not null)", Actual: (null) |
| `App/ReturnMapping/ReturnMapping.test.goal` | Expected non-null value — Expected: "(not null)", Actual: (null) |
| `Modules/List/ListOps2.test.goal` | updated item should equal 99 — Expected: 99, Actual: 20 |
| `Modules/Loop/Loop.test.goal` | foreach should iterate 3 times — Expected: 3, Actual: "0 + 1 + 1 + 1" |
| `App/Actors/Context/ActorContext.test.goal` | Expected to not be null — Expected: (null), Actual: (null) |
| `Modules/Error/Types/ErrorTypes.test.goal` | Expected: "TestKey", Actual: (null) |
| `Modules/Signing/DotNavigation/SigningDotNavigation.test.goal` | algorithm should be ed25519 — Expected: "ed25519", Actual: (null) |
| `Modules/Signing/NoIdentity/SigningNoIdentity.test.goal` | sign without identity should fail — Expected: True, Actual: (null) |
| `Modules/Goal/Return/GoalCallReturn.test.goal` | Expected non-null value — Expected: "(not null)", Actual: (null) |
| `Modules/Test/Run/TestRunEnforcesTimeout.test.goal` | Expected truthy value — Expected: True, Actual: (null) |
| `Modules/Test/Discover/TestDiscoverReportsStaleWhenPrMissing.test.goal` | Expected truthy value — Expected: True, Actual: False |
| `Modules/Variable/ContextVars/Basic/ContextVars.test.goal` | engine name should be set — Expected: "(not null)", Actual: (null) |
| `Modules/Variable/ContextVars/System/SystemVariables.test.goal` | Expected non-null value — Expected: "(not null)", Actual: (null) |
| `Modules/Loop/Foreach/Dictionary/ForeachDictionary.test.goal` | Expected: 3, Actual: "0 + 1" |
| `Modules/Condition/Compound/Mixed/ConditionCompound.test.goal` | Expected: "yes", Actual: (null) |
| `Modules/Condition/Compound/And/ConditionCompoundAnd.test.goal` | AND condition should pass when both sides true — Expected: "both-true", Actual: (null) |
| `Modules/Ui/RenderCallGoal/RenderCallGoal.test.goal` | Container does not contain value — Expected: ""Error"", Actual: "Result: {}" |
| `Modules/Ui/RenderWithParams/RenderWithParams.test.goal` | Container does not contain value — Expected: ""Title: My Page, Author: "", Actual: "Author: Ingi" |

## Fail — runtime error (29)

| Test | Error |
|---|---|
| `Modules/Signing/Expired/SigningExpired.test.goal` | Identity 'testSigner' already exists |
| `Modules/Signing/EmptyData/SigningEmptyData.test.goal` | Identity 'testSigner' already exists |
| `Modules/Signing/HeaderMismatch/SigningHeaderMismatch.test.goal` | Identity 'testSigner' already exists |
| `Modules/Signing/CustomContracts/SigningCustomContracts.test.goal` | Identity 'testSigner' already exists |
| `Modules/Signing/ProviderSwap/SigningProviderSwap.test.goal` | Identity 'testSigner' already exists |
| `Modules/Signing/Roundtrip/SigningRoundtrip.test.goal` | Identity 'testSigner' already exists |
| `Modules/Signing/TamperedData/SigningTamperedData.test.goal` | Identity 'testSigner' already exists |
| `Modules/Signing/WithHeaders/SigningWithHeaders.test.goal` | Identity 'testSigner' already exists |
| `Modules/Signing/TimedOut/SigningTimedOut.test.goal` | Identity 'testSigner' already exists |
| `Modules/Event/Remove/EventRemove.test.goal` | Unknown event type: 'output.write' |
| `Modules/Event/Basic/Events.test.goal` | Unknown event type: 'beforeGoalCall' |
| `Modules/Event/Priority/EventPriority.test.goal` | Unknown event type: 'before' |
| `Modules/Event/Override/EventOverride.test.goal` | File not found: nonexistent.json |
| `Modules/Goal/Relative/GoalCallRelative.test.goal` | File not found: .build/sub/subgoal.pr |
| `Modules/Builder/GetTypeInfo/BuilderGetTypeInfo.test.goal` | Building is not enabled |
| `Modules/Builder/GetActions/BuilderGetActions.test.goal` | Building is not enabled |
| `Modules/Builder/ParseGoal/BuilderParseGoal.test.goal` | Could not load file or assembly '/workspace/plang/Tests/Modules/Builder/ParseGoal/parse_test/'. Access is denied. |
| `Modules/Builder/ValidateValid/BuilderValidateValid.test.goal` | Building is not enabled |
| `Modules/Identity/Create/IdentityCreate.test.goal` | Identity 'TestCreate' already exists |
| `Modules/Identity/ArchiveNonDefault/IdentityArchiveNonDefault.test.goal` | Identity 'TestArcNonDefA' already exists |
| `Modules/Identity/DotNavigation/IdentityDotNavigation.test.goal` | Identity 'TestDotNav' already exists |
| `Modules/Identity/Unarchive/IdentityUnarchive.test.goal` | Identity 'TestUnarchiveId' already exists |
| `Modules/Identity/Rename/IdentityRename.test.goal` | Identity 'TestRenameOld' already exists |
| `Modules/Identity/SwitchDefault/IdentitySwitchDefault.test.goal` | Identity 'TestSwitchA' already exists |
| `Modules/Identity/Export/IdentityExport.test.goal` | Identity 'TestExportId' already exists |
| `Modules/Identity/ArchiveDefault/IdentityArchiveDefault.test.goal` | Identity 'TestArchiveDefault' already exists |
| `Modules/Identity/GetByName/IdentityGetByName.test.goal` | Identity 'TestGetByName' already exists |
| `Modules/Error/RetryOnly/ErrorRetryOnly.test.goal` | timed fail |
| `Modules/Crypto/HashBcryptVerify/HashBcryptVerify.test.goal` | Algorithm 'bcrypt' is not supported |

**Runtime-error clusters to flag for architect:**
- **`Identity 'testSigner' already exists`** appears in 9 signing tests. Looks like cross-test state leakage — the identity isn't cleaned up between tests, or each test should use a unique name. Same SystemDirectory inherited from parent App (per testing.md) could be the carrier.

## Stale (4)

- `Modules/Signing/NonceReplay/SigningNonceReplay.test.goal`
- `Modules/Http/DownloadFile/DownloadFile.test.goal`
- `Modules/Condition/Files/FileNotExists/ConditionFileNotExists.test.goal`
- `Modules/Condition/Files/FileExists/ConditionFileExists.test.goal`

## Pass (109)

- `App/CallStack/CallStack.test.goal`
- `App/DeepNavigation/DeepNavigation.test.goal`
- `App/StartupParams/StartupParams.test.goal`
- `App/RecursionDepth/RecursionDepthLimit.test.goal`
- `App/Retry/Retry.test.goal`
- `Modules/List/ListOps.test.goal`
- `Modules/Llm/LlmSchema.test.goal`
- `Modules/Llm/LlmFormat.test.goal`
- `Modules/Llm/LlmCache.test.goal`
- `Modules/Llm/LlmQuery.test.goal`
- `Modules/Llm/LlmContinue.test.goal`
- `Modules/Llm/LlmProperties.test.goal`
- `Modules/Output/Output.test.goal`
- `Modules/Assert/AssertComplete.test.goal`
- `Modules/Assert/Assert.test.goal`
- `Modules/File/File.test.goal`
- `Modules/Math/Math2.test.goal`
- `Modules/Math/Math.test.goal`
- `Modules/Mock/Mock.test.goal`
- `App/Actors/Switch/ActorSwitch.test.goal`
- `App/Actors/Datasource/ActorDatasource.test.goal`
- `Modules/Error/Multilingual/OnErrorMultilingual.test.goal`
- `Modules/Error/Mixed/ErrorMixed.test.goal`
- `Modules/Error/Props/ErrorProps.test.goal`
- `Modules/Error/Ordering/ErrorOrdering.test.goal`
- `Modules/Error/Call/ErrorCall.test.goal`
- `Modules/Error/Handling/ErrorHandling.test.goal`
- `Modules/Error/Nested/ErrorNested.test.goal`
- `Modules/Error/InHandler/ErrorInHandler.test.goal`
- `Modules/Error/Chain/ErrorChain.test.goal`
- `Modules/Error/GoalFirst/ErrorGoalFirst.test.goal`
- `Modules/Variable/Basic/Variables.test.goal`
- `Modules/Variable/Ops/VariableOps.test.goal`
- `Modules/Variable/Scoping/VariableScoping.test.goal`
- `Modules/Variable/Indexing/VariableIndexing.test.goal`
- `Modules/Signing/ContractMismatch/SigningContractMismatch.test.goal`
- `Modules/Signing/CorruptedSignature/SigningCorruptedSignature.test.goal`
- `Modules/Signing/VerifyUnsigned/SigningVerifyUnsigned.test.goal`
- `Modules/Event/Wildcard/EventWildcard.test.goal`
- `Modules/Event/AfterStep/EventAfterStep.test.goal`
- `Modules/Event/BeforeStep/EventBeforeStep.test.goal`
- `Modules/Event/AfterAction/EventAfterAction.test.goal`
- `Modules/Event/Multiple/EventMultiple.test.goal`
- `Modules/Goal/Basic/GoalCall.test.goal`
- `Modules/Goal/Missing/GoalCallMissing.test.goal`
- `Modules/Goal/Dynamic/GoalCallDynamic.test.goal`
- `Modules/Identity/AutoCreate/IdentityAutoCreate.test.goal`
- `Modules/Cache/Basic/Cache.test.goal`
- `Modules/Cache/Sliding/CacheSliding.test.goal`
- `Modules/Cache/DynamicKey/CacheDynamicKey.test.goal`
- `Modules/Cache/Key/CacheKey.test.goal`
- `Modules/Http/DownloadSkip/DownloadSkip.test.goal`
- `Modules/Http/SignedRequest/SignedRequest.test.goal`
- `Modules/Http/UnsignedRequest/UnsignedRequest.test.goal`
- `Modules/Http/PostRequest/PostRequest.test.goal`
- `Modules/Http/GetRequest/GetRequest.test.goal`
- `Modules/Http/ConfigHeaders/ConfigHeaders.test.goal`
- `Modules/Http/UploadFile/UploadFile.test.goal`
- `Modules/Test/Integration/TestSystemTestGoalReportsTimeout.test.goal`
- `Modules/Test/Integration/TestSystemTestGoalRespectsTagFilter.test.goal`
- `Modules/Test/Integration/TestSystemTestGoalIncludesAllThreePhases.test.goal`
- `Modules/Test/Integration/TestSystemTestGoalDoesNotUseForeach.test.goal`
- `Modules/Test/Condition/TestConditionIfRecordsBranchIndexTrueBranch.test.goal`
- `Modules/Test/Condition/TestConditionIfRecordsBranchIndexElseBranch.test.goal`
- `Modules/Test/Condition/TestConditionElseIfMatchesRecordsBranchIndex1.test.goal`
- `Modules/Http/StreamCallback/StreamCallback.test.goal`
- `Modules/Test/Tag/TestTagAccumulatesUserTagsOnRun.test.goal`
- `Modules/Test/Run/TestRunReportsAssertionFailure.test.goal`
- `Modules/Test/Run/TestRunIsolatesMemoryStackBetweenTests.test.goal`
- `Modules/Test/Assert/TestAssertFailureSnapshotsVariables.test.goal`
- `Modules/Test/Discover/TestDiscoverFindsTestGoals.test.goal`
- `Modules/Test/EdgeCase/TestDiscoverHandlesIcelandicGoalNames.test.goal`
- `Modules/Crypto/HashDefault/HashDefault.test.goal`
- `Modules/Test/Report/TestReportMasksSensitiveVariablesJunit.test.goal`
- `Modules/Test/Report/TestReportWritesJunitXml.test.goal`
- `Modules/Test/Report/TestReportIncludesCoverageTables.test.goal`
- `Modules/Test/Report/TestReportMasksSensitiveVariables.test.goal`
- `Modules/Test/Report/TestReportRendersFailureWithVariables.test.goal`
- `Modules/Crypto/HashObject/HashObject.test.goal`
- `Modules/Crypto/HashSHA256/HashSHA256.test.goal`
- `Modules/Crypto/VerifyWrongHash/VerifyWrongHash.test.goal`
- `Modules/Crypto/ProviderSwap/ProviderSwap.test.goal`
- `Modules/Settings/SetMaxGzipSize/Start.test.goal`
- `Modules/Settings/SettingsCrud/Start.test.goal`
- `Modules/Variable/ContextVars/Advanced/ContextVars2.test.goal`
- `Modules/Loop/Foreach/Empty/ForeachEmpty.test.goal`
- `Modules/Condition/Operators/Falsy/ConditionFalsy.test.goal`
- `Modules/Condition/Operators/NotEquals/ConditionNotEquals.test.goal`
- `Modules/Condition/Operators/LTE/ConditionLTE.test.goal`
- `Modules/Http/ConfigBaseUrl/ConfigBaseUrl.test.goal`
- `Modules/Condition/Operators/Contains/ConditionContains.test.goal`
- `Modules/Condition/Operators/Equals/ConditionEquals.test.goal`
- `Modules/Condition/Operators/EndsWith/ConditionEndsWith.test.goal`
- `Modules/Condition/Operators/Truthy/ConditionTruthy.test.goal`
- `Modules/Condition/Operators/GTE/ConditionGTE.test.goal`
- `Modules/Condition/Operators/GreaterThan/ConditionGreaterThan.test.goal`
- `Modules/Condition/Operators/Not/ConditionNot.test.goal`
- `Modules/Condition/Operators/StartsWith/ConditionStartsWith.test.goal`
- `Modules/Condition/Operators/LessThan/ConditionLessThan.test.goal`
- `Modules/Condition/Files/FileExistsSubSteps/ConditionFileExistsSubSteps.test.goal`
- `Modules/Condition/Compound/Or/ConditionCompoundOr.test.goal`
- `Modules/Condition/If/SubStepsFalse/ConditionSubStepsFalse.test.goal`
- `Modules/Condition/If/Nested/ConditionNested.test.goal`
- `Modules/Condition/If/Basic/Condition.test.goal`
- `Modules/Condition/If/SubStepsTrue/ConditionSubStepsTrue.test.goal`
- `Modules/Condition/If/ElseBranch/ConditionElseBranch.test.goal`
- `Modules/Ui/RenderInline/RenderInline.test.goal`
- `Modules/Ui/RenderFile/RenderFile.test.goal`
- `Modules/Ui/RenderInclude/RenderInclude.test.goal`
