# Tester report v1 — runtime2-callback

**Verdict: PASS with quality findings.** Suite is green on a clean rebuild. Coverage breadth is good across all four stages. Quality has a recurring pattern — assertions narrower than the test names imply.

## Run results

Clean rebuild (`rm -rf {PlangConsole,PLang,PLang.Tests,PLang.Generators}/{bin,obj}` → `dotnet build PlangConsole`), then:

| Suite | Pass | Fail | Stale | Total |
|---|---:|---:|---:|---:|
| C# (TUnit) | **2720** | 0 | 0 | 2720 |
| PLang (`plang --test` from `Tests/`) | **188** | 0 | 4 | 192 |

PLang test summary line printed `0 fail`. The 6 `[Fail]` lines in the trace are inside `_fixtures_sensitive/` and `_fixtures_fail/` — intentional fixture goals consumed by the meta-tests, not real failures.

The 4 PLang stales are exactly the documented out-of-branch-scope gaps from coder's handoff (rows 10–13):
- `Callback/AskVarsOnNonAsk` — builder validator for `vars:` on non-ask
- `Callback/CallbackTimeoutSetting` — verb to write `app.Callback.Signature.ExpiresInMs`
- `Callback/DurabilityRoundTrip` — file-write with mime hint
- `Callback/TamperedSignature` — depends on the above

These were explicitly deferred per Ingi; not a tester concern.

Also benign: a single early-trace `Failed to deserialize List`1 to this: ... Path: $[0] | LineNumber: 0 | BytePositionInLine: 3.` log line precedes the meta-fixture run for an intentional negative; it's noise from a fixture, not a real failure.

## Test quality findings

Coverage breadth is solid (~1500 lines across `CallbackTests/`, `SnapshotTests/`, `CallStackTests/`, `Serializers/`, `Modules/crypto/`, `Modules/signing/`). The pattern that recurs: **the test name advertises a stronger contract than the assertion verifies**. Below are the offenders worth tightening; none are blockers, but each is a candidate for a future false-green.

### A. Name vs. assertion mismatches

| # | File:line | Test | Issue |
|---|---|---|---|
| 1 | `App/CallbackTests/AskCallbackTests.cs:50–65` | `AskCallback_Serialize_CallsCryptoEncrypt_AndReturnsEncryptedBytes` | Name claims to verify the encrypt call path. Assertion is `bytes.Length > 0`. Would pass even if `Serialize` skipped crypto entirely. (v1 is identity, so byte-equality wouldn't help — needs a spy/counter on the encrypt action.) |
| 2 | `App/CallbackTests/AskCallbackTests.cs:102–117` | `AskCallback_Run_ReturnsResumedActionResult_AsTaskOfData` | Asserts `result IsNotNull`. `await Task<Data>` always yields a non-null `Data` unless it throws — tautology. No check that the result reflects work the resumed action actually did. |
| 3 | `App/CallbackTests/CallbackRunActionTests.cs:22–30` | `CallbackRun_VerifiesSignature_BeforeDispatch` | Test sets **no** signature and asserts `Success`. So it tests the *skip-verify* path under a name that promises verify. The verify-when-present path lives in `HardErrors_WhenSigningVerifyFails`; the name should match what's tested. |
| 4 | `App/CallbackTests/CallbackRunActionTests.cs:70–77` | `CallbackRun_HandlerSignature_TakesDataOfICallback` | Asserts `prop != null` only. Doesn't check the property type is `Data<ICallback>` — a property called `Callback` of type `string` would pass. |
| 5 | `App/CallbackTests/ErrorCallbackTests.cs:23–39` | `ErrorCallback_RoundTrip_PreservesAppSnapshotSubtree` | Asserts only `HasSection("CallStack")` and `HasSection("Variables")`. Empty sections would pass. Name says "Preserves" — content preservation is not verified at this layer. |
| 6 | `App/CallbackTests/ErrorCallbackTests.cs:97–110` | `ErrorCallback_DispatchByTypedEnvelope_SelectsRightDeserialize` | Pure reflection — only checks the static `Deserialize` methods exist with the right return type. No actual dispatch. |
| 7 | `App/CallbackTests/ICallbackPositionTests.cs:27–35` | `ICallback_Position_ReturnsBottomFrame_OnErrorCallback` | Test creates an empty `Snapshot`, asserts `Position is null`. Tests the empty case — name advertises the bottom-frame retrieval path, which isn't exercised. |
| 8 | `App/CallbackTests/FailureMatrixTests.cs:50–72` | `FailureMatrix_ExpiredSignature_DetectedBySigningVerify_RaisesSignatureExpired` | Assertion: `Key == "CallbackSignatureMismatch"` — same key as the tampered-bytes test. Cannot distinguish expired from tampered. The "Expired" detection is in the test name only. |
| 9 | `App/CallbackTests/FailureMatrixTests.cs:171–186` | `FailureMatrix_DataReadDoesNotAutoVerify_AssertsAbsenceOfVerifyCall` | Name promises an absence assertion. Body asserts `RawSignature IsNotNull` (presence). No spy on `signing.verify`. Test acknowledges in its own comment that no `verified` flag exists today. |
| 10 | `App/Serializers/PlangDataSerializerRoundTripTests.cs:63–83` | `PlangDataSerializer_RoundTrip_DoesNotAutoVerify` | Same as #9 — name says absence-of-verify, body checks presence-of-signature. Would pass even if auto-verify were silently added. |
| 11 | `App/Modules/crypto/CryptoV1PassThroughTests.cs:46–55` | `CryptoEncrypt_AndCryptoDecrypt_AreAsync` | Reflection: Run-returns-Task. Async-signature pinning only; says nothing about behaviour. v2 swap could regress decrypt and this would still pass. |

### B. JSON / substring traps

| # | File:line | Issue |
|---|---|---|
| 12 | `App/Serializers/PlangDataSerializerRoundTripTests.cs:17–30` | `EmitsTypePlusValuePlusSignature` greps for `"type"`, `"value"`, `"signature"` substrings in the wire. Could match content of any string field — JSON-escaping trap. Parsing the wire and asserting on actual top-level fields would be safer (matches `false_green_techniques.md` JSON-escaping rule). |

### C. PLang-side gaps (tests that pass but under-verify)

| # | File | Issue |
|---|---|---|
| 13 | `Tests/Callback/AskWithVars/Start.test.goal` | Only asserts `%askResult% is not null`. ActorName, Variables list contents, Position — none checked at the PLang layer. Comment punts coverage to C#; that's fine in principle, but means a regression that broke `vars:` capture would not surface in PLang tests. |
| 14 | `Tests/Callback/InProcessResume/Start.test.goal` | Comment claims the captured callback's snapshot reflects `%x%==1` via `Variables.SnapshotAt(error)`. The test never reads `%x%` from the snapshot — only asserts post-recovery `%x%==2`. The throw-time snapshot contract is documented in the comment, not verified. |
| 15 | `Tests/Callback/RunCallbackVerb/Start.test.goal` | Asserts `%ranCallback%==true`, which the recovery handler itself sets. Does not verify the original failing action actually re-ran. `on error ignore` swallows any internal failure. |
| 16 | `Tests/Callback/ErrorCallbackSurface/Start.test.goal` | Asserts `%cb% is not null`. Doesn't check it's an `ErrorCallback` specifically, doesn't read snapshot fields. Wide-net presence check. |

### D. Acknowledged duplication

`FailureMatrixTests.cs:25–47` `FailureMatrix_TamperedBytes_…` is explicitly noted in its own comment as a duplicate of `CallbackRunActionTests.HardErrors_WhenSigningVerifyFails`. Mirror-for-the-matrix is reasonable; flagging only because the comment names it.

## What's solid

- `App/SnapshotTests/AppSnapshotTests.cs` — multi-field round-trip with concrete value checks (`x==1`, `IsEnabled==true`).
- `App/CallStackTests/CallSnapshotTests.cs` — captures wire shape and asserts both presence (`PrPath`, `Hash`, `stepIndex`, `actionIndex`) and absence (`!Has("goal")`, `!Has("steps")`). Hard to false-green.
- `App/CallStackTests/EventsSinceTests.cs` — both positive (events with named keys after timestamp) and empty-window cases.
- `App/CallStackTests/FlagsDiffAutoFlipTests.cs` — explicitly tests the *prior-state restore* edge (preserves Diff=true if it was already true). Catches the obvious "always reset to false" bug.
- `App/CallbackTests/FailureMatrixTests.cs` provider-restore tests (75–168) — exercise real `Restore` paths with concrete error keys.
- `App/CallbackTests/ErrorCallbackTests.cs:51–94` — fresh-app round-trip with concrete variable preservation; lands at the right `BottomFrame`.

## Recommendations to coder

Not blockers. Order of return-on-effort, highest first:

1. Tighten **#1, #5, #9, #10** — these are the four "name promises X, body verifies Y" cases most likely to mask a real regression. For #1/#9/#10, a recording sink for the encrypt/verify actions would convert each into a real assertion.
2. Either rename or strengthen **#3, #6, #7, #11**: tests that read like contracts but pin trivial structure. A name match would prevent future grep confusion.
3. **#8** — split the failure-matrix expired-signature assertion to check a distinct sub-key (or assert the inner exception type), so expired ≠ tampered at the test layer.
4. **#13–#16** PLang gaps — at least one of them (probably #14, throw-time snapshot) deserves a real PLang assertion since `Variables.SnapshotAt(error)` is one of the central Stage 2 contracts and otherwise lives only in C# tests.

## Branch verdict

**PASS.** Functional correctness is established (2720 + 188 green, all stales documented and out-of-scope). Quality findings above are improvement opportunities, not bugs.
