# test-designer summary — runtime2-callback

## Version
v1

## What this is

Translation of the architect's Stage 1–4 design (`.bot/runtime2-callback/architect/plan.md` + topic files + four `stage-N-*.md` docs) into concrete TUnit and PLang test stubs. The architect already produced a comprehensive coverage matrix (`plan/test-coverage.md`) and two integration-cut shapes (`plan/test-strategy.md`); test-designer's job here was to name each test, place it on the right layer, tag it with the stage that should turn it green, and write the stub file. All bodies are `Assert.Fail("Not implemented")` (C#) or `- throw "not implemented"` (PLang). Coder fills bodies stage by stage.

## What was done

- **Plan**: `.bot/runtime2-callback/test-designer/v1/plan.md` — approach, layout, open decisions resolved with defaults, batch index.
- **Test plan**: `.bot/runtime2-callback/test-designer/v1/test-plan.md` — 14 batches, ~85 C# tests + ~12 goal tests + 2 integration cuts. Each batch lists every test name. Stage tags `[S1]`–`[S4]` added so coder can run progressively.
- **C# stubs** under `PLang.Tests/App/`:
  - `SnapshotTests/` — Interface, AppSnapshot, StaticsAndModes, Providers (Stage 1)
  - `VariablesTests/VariablesSnapshotTests.cs`, `VariablesTests/SnapshotAtErrorTests.cs` (S1, S2)
  - `Errors/ErrorsTrailSnapshotTests.cs`, `Errors/ErrorCallbackPropertyTests.cs` (S1, S4)
  - `CallStackTests/CallSnapshotTests.cs`, `CallStackSnapshotTests.cs`, `EventsSinceTests.cs`, `FlagsDiffAutoFlipTests.cs` (S2)
  - `DataTests/DataLazySignatureTests.cs`, `DataContextWiringTests.cs` (S3)
  - `Serializers/JsonSerializerRoundTripTests.cs`, `PlangDataSerializerRoundTripTests.cs`, `MimeRegistrationTests.cs` (S3)
  - `Modules/signing/SignatureRenameTests.cs` (S3)
  - `Modules/crypto/CryptoV1PassThroughTests.cs` (S4)
  - `CallbackTests/` — ICallbackPosition, AskCallback, ErrorCallback, AppCallbackConfig, CallbackRunAction, FailureMatrix (S4)
- **PLang `.goal` stubs** under `Tests/Callback/`: 11 scenario folders, each with a single `Start.test.goal` carrying the spec comment and a `- throw "not implemented"` body. Includes the two integration cuts (`InProcessResume/`, `DurabilityRoundTrip/`) and surface tests (ErrorCallbackSurface, RunCallbackVerb, AskWithVars, AskVarsResumeBindsValue, CallbackTimeoutSetting, AskVarsOnNonAsk, ErrorCallbackOutsideHandler, RunNonCallback, TamperedSignature).

### Open decisions resolved with default

Five matrix rows had ambiguity. Defaults applied (and noted in test stubs); coder/architect can override during review:

1. `%!error.callback%` outside handler → throws `ErrorCallbackOutsideErrorScope`
2. Channel handed unregistered MIME → throws `UnregisteredMimeType`
3. `Error.@this.Callback` idempotency → reference equality (cached `Data` instance)
4. `crypto.encrypt`/`decrypt` "C# / goal" → split into one C# test + one goal test
5. `signature-rename.md` "compiles cleanly" row → reformulated as a runtime reflection test (`SignedData` doesn't resolve)

## Code example

A typical C# stub:

```csharp
namespace PLang.Tests.App.CallbackTests;

public class AskCallbackTests
{
    [Test]
    public async Task AskCallback_Run_BindsVariables_AndDispatchesAskActionWithBoundValue()
    {
        // Run binds Variables into the resumed App and dispatches the ask at Position
        // — the action returns the bound value rather than issuing a fresh ask.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
```

A typical PLang stub:

```
Start
/ Reading %!error.callback% inside an error handler resolves through
/ app.Errors.Current.Callback to a Data<ErrorCallback>; assert it is not null
/ and its Position lands at the throwing (goal, step, action).
- throw "not implemented"
```

The body is the spec — the comment names exactly what coder must make true.

## Next

Run **coder** next. Stage-by-stage:
1. Stage 1 (Snapshot foundation) — turns `[S1]` rows green.
2. Stage 2 (CallStack frames + Variables time-travel) — turns `[S2]` rows green.
3. Stage 3 (Data lazy signing + serializers) — turns `[S3]` rows green.
4. Stage 4 (Callback records + verbs) — turns `[S4]` rows green and unlocks both integration cuts.
