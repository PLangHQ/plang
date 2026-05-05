# Callback — test plan (v1)

Translates the architect's `plan/test-coverage.md` matrix and `plan/test-strategy.md` integration cuts into concrete test stubs. Each stub is `Assert.Fail("Not implemented")` (C#) or `- throw "not implemented"` (PLang). Coder fills bodies stage by stage.

## Layer + path conventions

- **C# TUnit** → `PLang.Tests/App/<area>/`. Folders use `*Tests` suffix when the area name is a global alias (e.g. `CallbackTests`, `SnapshotTests`); otherwise neighbour-style (`Errors/`, `Modules/`, `Serializers/`).
- **PLang `.goal`** → `Tests/Callback/<scenario>/Start.goal` + `Start.test.goal` mirror `Tests/Signing/`, `Tests/Crypto/`.

## Stage tagging

Each test name is annotated with the stage that should make it green. Coder runs the suite stage-by-stage; tests for later stages stay red until their stage closes.

- `[S1]` Snapshot foundation (Stage 1)
- `[S2]` CallStack frames + Variables time-travel (Stage 2)
- `[S3]` Data lazy signing + per-mimetype serializers (Stage 3)
- `[S4]` Callback records + verbs (Stage 4)

## Batch index

| # | Area | Layer | Stage | File(s) |
|---|---|---|---|---|
| 1 | `ISnapshotted` foundation + per-`@this` round-trips | C# | S1 | `App/SnapshotTests/SnapshotInterfaceTests.cs`, `App/SnapshotTests/AppSnapshotTests.cs`, `App/VariablesTests/VariablesSnapshotTests.cs`, `App/Errors/ErrorsTrailSnapshotTests.cs`, `App/SnapshotTests/StaticsAndModesSnapshotTests.cs` |
| 2 | `App.Providers` two-step Restore + referent integrity | C# | S1 | `App/SnapshotTests/ProvidersSnapshotTests.cs` |
| 3 | `Call.@this` Capture/Restore + Goal-stub + hash-match | C# | S2 | `App/CallStackTests/CallSnapshotTests.cs` |
| 4 | `App.CallStack.@this` Capture/Restore + `EventsSince` + `BottomFrame` | C# | S2 | `App/CallStackTests/CallStackSnapshotTests.cs`, `App/CallStackTests/EventsSinceTests.cs` |
| 5 | `App.Variables.SnapshotAt(error)` + `Flags.Diff` auto-flip | C# | S2 | `App/VariablesTests/SnapshotAtErrorTests.cs`, `App/CallStackTests/FlagsDiffAutoFlipTests.cs` |
| 6 | `Data.@this.Signature` lazy + Context wiring | C# | S3 | `App/DataTests/DataLazySignatureTests.cs`, `App/DataTests/DataContextWiringTests.cs` |
| 7 | `JsonSerializer` + `PlangDataSerializer` round-trip + lazy expiry | C# | S3 | `App/Serializers/JsonSerializerRoundTripTests.cs`, `App/Serializers/PlangDataSerializerRoundTripTests.cs` |
| 8 | Channel routing + MIME + `SignedData` rename | C# | S3 | `App/Serializers/MimeRegistrationTests.cs`, `App/Modules/signing/SignatureRenameTests.cs` |
| 9 | `ICallback` + `AskCallback`/`ErrorCallback` records + Position + Run | C# | S4 | `App/CallbackTests/ICallbackPositionTests.cs`, `App/CallbackTests/AskCallbackTests.cs`, `App/CallbackTests/ErrorCallbackTests.cs` |
| 10 | `Error.@this.Callback` + `app.Callback.Signature` config | C# | S4 | `App/Errors/ErrorCallbackPropertyTests.cs`, `App/CallbackTests/AppCallbackConfigTests.cs` |
| 11 | `callback.run` action + `crypto.encrypt`/`decrypt` v1 | C# | S4 | `App/CallbackTests/CallbackRunActionTests.cs`, `App/Modules/crypto/CryptoV1PassThroughTests.cs` |
| 12 | PLang surfaces (`%!error.callback%`, `- run %callback%`, `vars:`) | goal | S4 | `Tests/Callback/<scenario>/Start.test.goal` × 8 |
| 13 | Failure matrix (consolidated negatives) | C# + goal | S2-S4 | `App/CallbackTests/FailureMatrixTests.cs`, `Tests/Callback/Tampered*/`, `Tests/Callback/RunNonCallback/` |
| 14 | Integration cuts (the two from test-strategy) | goal + helpers | S4 | `Tests/Callback/InProcessResume/`, `Tests/Callback/DurabilityRoundTrip/` |

## Open decisions resolved with default

These coverage rows had ambiguity; defaults applied during this pass. Coder/architect override during review if needed:

| Row | Default | Rationale |
|---|---|---|
| `%!error.callback%` outside handler scope | throws `ErrorCallbackOutsideErrorScope` | matches PLang's referent-integrity discipline |
| Channel handed unregistered MIME | throws `UnregisteredMimeType` | avoids silent fallback; matches "names + integrity" trust model |
| `Error.@this.Callback` idempotency | reference equality (`IsSameReferenceAs`) | Stage 4 doc says "cached `Data` instance" |
| `crypto.encrypt`/`decrypt` "C# / goal" | both — split into one C# test + one goal test | explicit at both layers |
| `signature-rename.md` "callsites compile" | one test that fails if `SignedData` resolves | runtime-style assertion of the rename |

## Batches in detail

### Batch 1 — `ISnapshotted` foundation [S1]

```csharp
// App/SnapshotTests/SnapshotInterfaceTests.cs
ISnapshotted_Capture_AppendsTypedEntries_ToSnapshot
ISnapshotted_RestoreIsStaticFactory_NotInstanceMethod

// App/SnapshotTests/AppSnapshotTests.cs
App_Snapshot_WalksISnapshottedProperties_AndAggregatesIntoTree
App_Restore_DispatchesEachSubtree_ToMatchingThisRestore
App_Snapshot_OmitsReconstructOnBuildSubsystems
App_Cache_NotInSnapshot_FreshAppHasEmptyCache

// App/VariablesTests/VariablesSnapshotTests.cs
Variables_RoundTrip_PreservesValuesAndProperties_ForUserVars
Variables_Snapshot_ExcludesBangPrefixed_DynamicData_AndSettingsVariables

// App/Errors/ErrorsTrailSnapshotTests.cs
ErrorsTrail_RoundTrip_PreservesEntries
ErrorsTrail_AfterRestore_IsReadOnly

// App/SnapshotTests/StaticsAndModesSnapshotTests.cs
Statics_RoundTrip_PreservesNameValuePairs
Build_RoundTrip_PreservesIsEnabled
Testing_RoundTrip_PreservesIsEnabled
```

### Batch 2 — Providers Restore [S1]

```csharp
// App/SnapshotTests/ProvidersSnapshotTests.cs
Providers_RoundTrip_PreservesDefaultSelectionsAndRuntimeRegistrations
Providers_Restore_ReplaysRegistrationsBeforeApplyingDefaults
Providers_Restore_HardErrors_OnUnresolvableRuntimeRegistrationSource
Providers_Restore_HardErrors_OnUnresolvableDefaultSelectionName
Providers_BuiltInRegistrations_NotInSnapshot
Providers_OnlyRegistryLayer_Captured_ProviderInstancesAreReconstructed
```

### Batch 3 — `Call.@this` Capture/Restore [S2]

```csharp
// App/CallStackTests/CallSnapshotTests.cs
Call_Capture_EmitsGoalStub_PrPathPlusHash_NotFullGoal
Call_Capture_IncludesStepIndexAndActionIndex
Call_Restore_ResolvesGoalStubAgainstLiveRegistry
Call_Restore_HardErrors_OnGoalNotFound
Call_Restore_HardErrors_OnHashMismatch_RaisesCallbackGoalHashMismatch
Call_Restore_DoesNotMutateLiveGoal
Call_Restore_HashErrorIsTypedNotBoolean
Call_Capture_OmitsTimingTier_AndInFlightNetworkState
```

### Batch 4 — `App.CallStack.@this` snapshot + EventsSince [S2]

```csharp
// App/CallStackTests/CallStackSnapshotTests.cs
CallStack_Capture_WalksActiveFrameChain_OuterToBottom
CallStack_Capture_DropsCompletedChildren_AsHistoryNotState
CallStack_Restore_RebuildsChain_BottomFrameIsResumePoint
CallStack_BottomFrame_IdentifiesThrowingCall

// App/CallStackTests/EventsSinceTests.cs
EventsSince_ReturnsDiffEvents_WithTimestampGreaterThan
EventsSince_EmptyWhenNoMutations
```

### Batch 5 — `SnapshotAt(error)` + `Flags.Diff` auto-flip [S2]

```csharp
// App/VariablesTests/SnapshotAtErrorTests.cs
SnapshotAt_ReturnsVariablesProjection_AtThrowTime
SnapshotAt_ConsultsCallStackEventsSince_AndReverseApplies
SnapshotAt_ExcludesPostErrorMutationsByHandler
SnapshotAt_NoMutations_ReturnsCurrentState
SnapshotAt_IsPure_SameInputsSameResult

// App/CallStackTests/FlagsDiffAutoFlipTests.cs
FlagsDiff_AutoFlipsOn_DuringErrorProcessing
FlagsDiff_RestoredToPriorState_AfterErrorPathCompletes
```

### Batch 6 — `Data.@this.Signature` lazy + Context [S3]

```csharp
// App/DataTests/DataLazySignatureTests.cs
DataSignature_FirstAccess_PopulatesViaSigningSignAsync
DataSignature_CachedAfterFirstPopulate_ReturnsSameInstance
DataSignature_Expires_SeededFromAppCallbackConfig_OnlyForICallbackValues
DataSignature_Expires_NullForNonICallbackValues_EvenWhenConfigSet

// App/DataTests/DataContextWiringTests.cs
Data_Constructor_AcceptsContext_AndStoresPrivately
Data_LazySignature_ReadsExpiryFromContextAppCallbackSignature
Data_BareConstructorWithoutContext_NoLongerCompiles_OrThrowsOnSignatureRead
```

### Batch 7 — Per-MIME serializers round-trip [S3]

```csharp
// App/Serializers/JsonSerializerRoundTripTests.cs
JsonSerializer_Write_EmitsValueOnly_NeverReadsSignature
JsonSerializer_Read_ProducesData_WithoutPopulatingSignature
JsonSerializer_HandlesTextHtml_AndApplicationJson_MimeTypes

// App/Serializers/PlangDataSerializerRoundTripTests.cs
PlangDataSerializer_Write_EmitsTypePlusValuePlusSignature
PlangDataSerializer_Write_TriggersLazySigning_OnFirstSignatureRead
PlangDataSerializer_RoundTrip_SignaturePopulatedUnverifiedOnRead
PlangDataSerializer_RoundTrip_DoesNotAutoVerify
PlangDataSerializer_HandlesApplicationPlangDataMimeType
```

### Batch 8 — Channel routing + rename [S3]

```csharp
// App/Serializers/MimeRegistrationTests.cs
Channels_LookupSerializerByMimeType_RoutesAccordingly
Channels_UnregisteredMimeType_RaisesError
ApplicationPlangData_Mime_RegisteredAtAppBoot

// App/Modules/signing/SignatureRenameTests.cs
SignedDataTypeAlias_DoesNotResolve_AfterRename
SigningSignatureType_ExistsUnderNewName
```

### Batch 9 — Callback records [S4]

```csharp
// App/CallbackTests/ICallbackPositionTests.cs
ICallback_Position_ReturnsCallFrame_OnAskCallback
ICallback_Position_ReturnsBottomFrame_OnErrorCallback

// App/CallbackTests/AskCallbackTests.cs
AskCallback_RoundTrip_PreservesPositionActorAndVariables
AskCallback_Serialize_CallsCryptoEncrypt_AndReturnsEncryptedBytes
AskCallback_Deserialize_CallsCryptoDecrypt_AndReconstructsRecord
AskCallback_Run_BindsVariables_AndDispatchesAskActionWithBoundValue
AskCallback_Run_ReturnsResumedActionResult_AsTaskOfData
AskCallback_Run_HardErrors_OnGoalStubNotFound

// App/CallbackTests/ErrorCallbackTests.cs
ErrorCallback_RoundTrip_PreservesAppSnapshotSubtree
ErrorCallback_Position_ReadsAppCallStackBottomFrame
ErrorCallback_Run_ConstructsFreshApp_AndDispatchesRestore
ErrorCallback_Run_LandsAtBottomFrame_AndReExecutesFailedAction
ErrorCallback_DispatchByTypedEnvelope_SelectsRightDeserialize
```

### Batch 10 — Error.Callback property + config [S4]

```csharp
// App/Errors/ErrorCallbackPropertyTests.cs
ErrorCallback_Property_TriggersAppSnapshot_OnFirstRead
ErrorCallback_Property_ReturnsDataOfErrorCallback
ErrorCallback_Property_ReadTwice_ReturnsSameDataInstance
ErrorCallback_Property_ReturnsTwoIndependentCalls_ForTwoErrors

// App/CallbackTests/AppCallbackConfigTests.cs
AppCallback_IsConfigThis_NotAnICallback
AppCallbackSignature_ExpiresInMs_DefaultsToNull
AppCallbackSignature_AcceptsTimeoutValueInMs
```

### Batch 11 — `callback.run` action + crypto v1 [S4]

```csharp
// App/CallbackTests/CallbackRunActionTests.cs
CallbackRun_VerifiesSignature_BeforeDispatch
CallbackRun_HardErrors_WhenSigningVerifyFails
CallbackRun_DispatchesIntoCallbackRun_AndPropagatesData
CallbackRun_OnNonICallbackData_RaisesTypeError
CallbackRun_HandlerSignature_TakesDataOfICallback

// App/Modules/crypto/CryptoV1PassThroughTests.cs
CryptoEncrypt_V1_ReturnsInputUnchanged
CryptoDecrypt_V1_ReturnsInputUnchanged
CryptoEncryptDecrypt_V1_RoundTripIsByteIdentical
CryptoEncrypt_AndCryptoDecrypt_AreAsync
```

### Batch 12 — PLang surfaces [S4]

```
Tests/Callback/ErrorCallbackSurface/Start.test.goal      // %!error.callback% inside handler resolves to Data<ErrorCallback>
Tests/Callback/RunCallbackVerb/Start.test.goal           // - run %callback% dispatches and returns data
Tests/Callback/AskWithVars/Start.test.goal               // - ask user, vars: %x% issues AskCallback with only %x%
Tests/Callback/AskVarsResumeBindsValue/Start.test.goal   // resume of ask binds vars; no fresh ask
Tests/Callback/CallbackTimeoutSetting/Start.test.goal    // - set callback timeout to 5 minutes writes 300000
Tests/Callback/AskVarsOnNonAsk/Start.test.goal           // builder rejects vars: on non-ask
Tests/Callback/ErrorCallbackOutsideHandler/Start.test.goal // %!error.callback% outside handler throws
Tests/Callback/RunNonCallback/Start.test.goal            // - run %x% where %x% is not ICallback throws type error
```

### Batch 13 — Failure matrix [S2-S4]

```csharp
// App/CallbackTests/FailureMatrixTests.cs
FailureMatrix_TamperedBytes_DetectedBySigningVerify_RaisesSignatureMismatch
FailureMatrix_ExpiredSignature_DetectedBySigningVerify_RaisesSignatureExpired
FailureMatrix_GoalFileDeletedBetweenIssueAndResume_RaisesReferentIntegrityError
FailureMatrix_GoalHashDiffers_RaisesCallbackGoalHashMismatch
FailureMatrix_ProviderDllMissing_RaisesReferentIntegrityError
FailureMatrix_ProviderDefaultSelectionNameMissing_RaisesReferentIntegrityError
FailureMatrix_IdentityNameUnresolvable_RaisesReferentIntegrityError
FailureMatrix_DataReadDoesNotAutoVerify_AssertsAbsenceOfVerifyCall
```

```
Tests/Callback/TamperedSignature/Start.test.goal         // PLang on-error sees signature failure
```

### Batch 14 — Integration cuts [S4]

```
Tests/Callback/InProcessResume/Start.test.goal           // First cut — bind-jump-run, %x%==2 after resume
Tests/Callback/DurabilityRoundTrip/Start.test.goal       // Second cut — serialize, fresh process, verify, run
```

Both are PLang goal tests; minor C# helpers in the test project may be needed for two-process simulation.

## Done definition

When this plan's stubs are written and pushed, coder:
1. Implements Stage 1; runs `[S1]` tests; expects green.
2. Implements Stage 2; runs `[S1]+[S2]`; expects green.
3. Same for Stage 3, Stage 4.
4. After Stage 4, both integration cuts go green.
