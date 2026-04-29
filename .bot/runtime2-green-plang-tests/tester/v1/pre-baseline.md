# Pre-Baseline Snapshot — v1

Timestamp: 2026-04-21T12:52:50.520138+00:00
Source: `plang --test={'format':'junit','timeout':5}` from `/workspace/plang/Tests/`
Artefact: `Tests/.test/junit.xml`

## Totals

- Total: 162
- Pass:  96
- Fail:  48
- Stale: 18  (`<skipped>` in junit)
- Timeout: 0  (runner summary line)
- Skipped: 0  (tag-based)

## Build context

- Project-root `plang build` aborts on first ActionNotFound (pre-existing `Publish/Nuget.goal` uses `dotnet.nugetPush`, `Tests/FromJson/` uses `json.parse`). These modules don't exist.
- `plang build` scoped to `Tests/` with `--app={"create":true}` also fail-fasts at the first ActionNotFound inside Tests/ (e.g. `json.parse` in `FromJson`).
- As a result, only 19 goals were freshly built in this snapshot run; the rest rely on pre-existing `.pr` files. **18 tests are Stale**, which matches the 18 skipped.
- Phase 1 restructure + Phase 2 rebuild will need per-folder `plang build` loops that tolerate per-folder failures, otherwise the same blocker reoccurs.

## Stale (18)

- `Retry/Retry.test.goal`
- `Mock/Mock.test.goal`
- `Variable/Basic/Variables.test.goal`
- `Variable/Indexing/VariableIndexing.test.goal`
- `Signing/DotNavigation/SigningDotNavigation.test.goal`
- `Signing/ContractMismatch/SigningContractMismatch.test.goal`
- `Signing/CorruptedSignature/SigningCorruptedSignature.test.goal`
- `Signing/NonceReplay/SigningNonceReplay.test.goal`
- `Event/BeforeStep/EventBeforeStep.test.goal`
- `Event/Basic/Events.test.goal`
- `Event/Priority/EventPriority.test.goal`
- `Event/Override/EventOverride.test.goal`
- `Condition/FileNotExists/ConditionFileNotExists.test.goal`
- `Condition/FileExists/ConditionFileExists.test.goal`
- `GoalCall/Missing/GoalCallMissing.test.goal`
- `Http/DownloadFile/DownloadFile.test.goal`
- `Crypto/HashSHA256/HashSHA256.test.goal`
- `Crypto/ProviderSwap/ProviderSwap.test.goal`

## Fail (48)

| Test | Failure message (truncated) |
|---|---|
| `SetupGoal/Start.test.goal` | Expected: True, Actual: (null) |
| `Loop/Loop.test.goal` | foreach should iterate 3 times — Expected: 3, Actual: "0 + 1 + 1 + 1" |
| `StepResult/StepResult.test.goal` | Expected non-null value — Expected: "(not null)", Actual: (null) |
| `ReturnMapping/ReturnMapping.test.goal` | Expected non-null value — Expected: "(not null)", Actual: (null) |
| `ListOps/ListOps2.test.goal` | updated item should equal 99 — Expected: 99, Actual: 20 |
| `Error/Multilingual/OnErrorMultilingual.test.goal` | Expected: True, Actual: (null) |
| `Error/Types/ErrorTypes.test.goal` | Expected: "TestKey", Actual: (null) |
| `Signing/Expired/SigningExpired.test.goal` | Identity 'testSigner' already exists |
| `Signing/EmptyData/SigningEmptyData.test.goal` | Identity 'testSigner' already exists |
| `Signing/HeaderMismatch/SigningHeaderMismatch.test.goal` | Identity 'testSigner' already exists |
| `Signing/CustomContracts/SigningCustomContracts.test.goal` | Identity 'testSigner' already exists |
| `Signing/ProviderSwap/SigningProviderSwap.test.goal` | Identity 'testSigner' already exists |
| `Signing/Roundtrip/SigningRoundtrip.test.goal` | Identity 'testSigner' already exists |
| `Signing/NoIdentity/SigningNoIdentity.test.goal` | sign without identity should fail — Expected: True, Actual: (null) |
| `Signing/TamperedData/SigningTamperedData.test.goal` | Identity 'testSigner' already exists |
| `Signing/WithHeaders/SigningWithHeaders.test.goal` | Identity 'testSigner' already exists |
| `Signing/TimedOut/SigningTimedOut.test.goal` | Identity 'testSigner' already exists |
| `Event/Wildcard/EventWildcard.test.goal` | Object must be of type String. |
| `Event/AfterStep/EventAfterStep.test.goal` | Object must be of type String. |
| `Event/Remove/EventRemove.test.goal` | Unknown event type: 'output.write' |
| `Builder/GetTypeInfo/BuilderGetTypeInfo.test.goal` | Building is not enabled |
| `Builder/GetActions/BuilderGetActions.test.goal` | Action 'builder.getActions' not found |
| `Builder/ParseGoal/BuilderParseGoal.test.goal` | Could not load file or assembly '/workspace/plang/Tests/Builder/ParseGoal/parse_test/'. Access is denied. |
| `Builder/ValidateValid/BuilderValidateValid.test.goal` | Building is not enabled |
| `Condition/Falsy/ConditionFalsy.test.goal` | GoalIfFalse should have been called — Expected: "false-branch", Actual: (null) |
| `Condition/Basic/Condition.test.goal` | else branch should execute — Expected: True, Actual: (null) |
| `Condition/CompoundAnd/ConditionCompoundAnd.test.goal` | AND condition should pass when both sides true — Expected: "both-true", Actual: (null) |
| `Condition/ElseBranch/ConditionElseBranch.test.goal` | else branch should execute when condition is false — Expected: "small", Actual: (null) |
| `Condition/LessThan/ConditionLessThan.test.goal` | GoalIfTrue should have been called for y < 10 — Expected: "less", Actual: (null) |
| `ContextVars/Basic/ContextVars.test.goal` | engine name should be set — Expected: "(not null)", Actual: (null) |
| `ContextVars/System/SystemVariables.test.goal` | Expected non-null value — Expected: "(not null)", Actual: (null) |
| `Actor/Context/ActorContext.test.goal` | Expected  to not be null — Expected: (null), Actual: (null) |
| `GoalCall/Relative/GoalCallRelative.test.goal` | File not found: .build/sub/subgoal.pr |
| `GoalCall/Return/GoalCallReturn.test.goal` | Expected non-null value — Expected: "(not null)", Actual: (null) |
| `Foreach/Dictionary/ForeachDictionary.test.goal` | Expected: 3, Actual: "0 + 1" |
| `Error/RetryOnly/ErrorRetryOnly.test.goal` | timed fail |
| `Identity/Create/IdentityCreate.test.goal` | Identity 'TestCreate' already exists |
| `Identity/ArchiveNonDefault/IdentityArchiveNonDefault.test.goal` | Identity 'TestArcNonDefA' already exists |
| `Identity/DotNavigation/IdentityDotNavigation.test.goal` | Identity 'TestDotNav' already exists |
| `Identity/Unarchive/IdentityUnarchive.test.goal` | Identity 'TestUnarchiveId' already exists |
| `Identity/Rename/IdentityRename.test.goal` | Identity 'TestRenameOld' already exists |
| `Identity/SwitchDefault/IdentitySwitchDefault.test.goal` | Identity 'TestSwitchA' already exists |
| `Identity/Export/IdentityExport.test.goal` | Identity 'TestExportId' already exists |
| `Identity/ArchiveDefault/IdentityArchiveDefault.test.goal` | Identity 'TestArchiveDefault' already exists |
| `Identity/GetByName/IdentityGetByName.test.goal` | Identity 'TestGetByName' already exists |
| `Crypto/HashBcryptVerify/HashBcryptVerify.test.goal` | Algorithm 'bcrypt' is not supported |
| `Ui/RenderCallGoal/RenderCallGoal.test.goal` | Container does not contain value — Expected: ""Error"", Actual: "Result: {}" |
| `Ui/RenderWithParams/RenderWithParams.test.goal` | Container does not contain value — Expected: ""Author: Ingi"", Actual: "Title: My Page, Author: " |

## Pass (96)

- `Llm/LlmSchema.test.goal`
- `Llm/LlmFormat.test.goal`
- `Llm/LlmCache.test.goal`
- `Llm/LlmQuery.test.goal`
- `Llm/LlmContinue.test.goal`
- `Llm/LlmProperties.test.goal`
- `Output/Output.test.goal`
- `File/File.test.goal`
- `CallStack/CallStack.test.goal`
- `FromJson/FromJson.test.goal`
- `DeepNavigation/DeepNavigation.test.goal`
- `StartupParams/StartupParams.test.goal`
- `Assert/AssertComplete.test.goal`
- `Assert/Assert.test.goal`
- `RecursionDepthLimit/RecursionDepthLimit.test.goal`
- `Math/Math2.test.goal`
- `Math/Math.test.goal`
- `ListOps/ListOps.test.goal`
- `Error/Mixed/ErrorMixed.test.goal`
- `Error/Props/ErrorProps.test.goal`
- `Error/Ordering/ErrorOrdering.test.goal`
- `Error/Call/ErrorCall.test.goal`
- `Error/Handling/ErrorHandling.test.goal`
- `Error/Nested/ErrorNested.test.goal`
- `Error/InHandler/ErrorInHandler.test.goal`
- `Error/Chain/ErrorChain.test.goal`
- `Error/GoalFirst/ErrorGoalFirst.test.goal`
- `Variable/Ops/VariableOps.test.goal`
- `Variable/Scoping/VariableScoping.test.goal`
- `Signing/VerifyUnsigned/SigningVerifyUnsigned.test.goal`
- `Event/AfterAction/EventAfterAction.test.goal`
- `Event/Multiple/EventMultiple.test.goal`
- `Condition/NotEquals/ConditionNotEquals.test.goal`
- `Condition/Compound/ConditionCompound.test.goal`
- `Condition/SubStepsFalse/ConditionSubStepsFalse.test.goal`
- `Condition/LTE/ConditionLTE.test.goal`
- `Condition/CompoundOr/ConditionCompoundOr.test.goal`
- `Condition/Contains/ConditionContains.test.goal`
- `Condition/Equals/ConditionEquals.test.goal`
- `Condition/EndsWith/ConditionEndsWith.test.goal`
- `Condition/FileExistsSubSteps/ConditionFileExistsSubSteps.test.goal`
- `Condition/Truthy/ConditionTruthy.test.goal`
- `Condition/Nested/ConditionNested.test.goal`
- `Condition/GTE/ConditionGTE.test.goal`
- `Condition/GreaterThan/ConditionGreaterThan.test.goal`
- `Condition/SubStepsTrue/ConditionSubStepsTrue.test.goal`
- `Condition/Not/ConditionNot.test.goal`
- `Condition/StartsWith/ConditionStartsWith.test.goal`
- `ContextVars/Advanced/ContextVars2.test.goal`
- `Actor/Switch/ActorSwitch.test.goal`
- `Actor/Datasource/ActorDatasource.test.goal`
- `GoalCall/Basic/GoalCall.test.goal`
- `GoalCall/Dynamic/GoalCallDynamic.test.goal`
- `TestModule/Integration/TestSystemTestGoalDoesNotUseForeach.test.goal`
- `TestModule/Integration/TestSystemTestGoalReportsTimeout.test.goal`
- `TestModule/Integration/TestSystemTestGoalRespectsTagFilter.test.goal`
- `TestModule/Integration/TestSystemTestGoalIncludesAllThreePhases.test.goal`
- `TestModule/Tag/TestTagAccumulatesUserTagsOnRun.test.goal`
- `TestModule/Condition/TestConditionIfRecordsBranchIndexTrueBranch.test.goal`
- `TestModule/Condition/TestConditionIfRecordsBranchIndexElseBranch.test.goal`
- `TestModule/Condition/TestConditionElseIfMatchesRecordsBranchIndex1.test.goal`
- `TestModule/Run/TestRunReportsAssertionFailure.test.goal`
- `TestModule/Run/TestRunIsolatesMemoryStackBetweenTests.test.goal`
- `TestModule/Run/TestRunEnforcesTimeout.test.goal`
- `TestModule/Assert/TestAssertFailureSnapshotsVariables.test.goal`
- `TestModule/Report/TestReportMasksSensitiveVariablesJunit.test.goal`
- `TestModule/Report/TestReportWritesJunitXml.test.goal`
- `TestModule/Report/TestReportIncludesCoverageTables.test.goal`
- `TestModule/Report/TestReportRendersFailureWithVariables.test.goal`
- `TestModule/Report/TestReportMasksSensitiveVariables.test.goal`
- `TestModule/Discover/TestDiscoverReportsStaleWhenPrMissing.test.goal`
- `TestModule/Discover/TestDiscoverFindsTestGoals.test.goal`
- `TestModule/EdgeCase/TestDiscoverHandlesIcelandicGoalNames.test.goal`
- `Foreach/Empty/ForeachEmpty.test.goal`
- `Identity/AutoCreate/IdentityAutoCreate.test.goal`
- `Cache/Sliding/CacheSliding.test.goal`
- `Cache/Key/CacheKey.test.goal`
- `Cache/Basic/Cache.test.goal`
- `Cache/DynamicKey/CacheDynamicKey.test.goal`
- `Http/DownloadSkip/DownloadSkip.test.goal`
- `Http/ConfigBaseUrl/ConfigBaseUrl.test.goal`
- `Http/ConfigHeaders/ConfigHeaders.test.goal`
- `Http/UnsignedRequest/UnsignedRequest.test.goal`
- `Http/UploadFile/UploadFile.test.goal`
- `Crypto/HashDefault/HashDefault.test.goal`
- `Http/GetRequest/GetRequest.test.goal`
- `Crypto/HashObject/HashObject.test.goal`
- `Crypto/VerifyWrongHash/VerifyWrongHash.test.goal`
- `Settings/SetMaxGzipSize/Start.test.goal`
- `Settings/SettingsCrud/Start.test.goal`
- `Ui/RenderFile/RenderFile.test.goal`
- `Ui/RenderInline/RenderInline.test.goal`
- `Ui/RenderInclude/RenderInclude.test.goal`
- `Http/StreamCallback/StreamCallback.test.goal`
- `Http/SignedRequest/SignedRequest.test.goal`
- `Http/PostRequest/PostRequest.test.goal`
