# coder summary — runtime2-callback

## Version
v5 — security/auditor v2 fixes pass

## What this is

Auditor v2 (PASS) + security v1 (PASS-with-findings) confirmed four findings on top of the v4 keystone branch. Per Ingi: fix S-F1 (Medium), S-F3 (Low), S-F4 (Low), and the auditor v1 N3 follow-up (CLR exceptions out of Restore). Skip S-F2 — Ingi: "not a security issue", architect call.

## What was done

### S-F1 (Medium) — `callback.run` always seals + verifies
**File:** `PLang/App/modules/callback/run.cs`

Replaced the `if (RawSignature != null) verify` gate with the auditor's option (2):
1. `Callback.EnsureSigned()` always runs first. In-process Data with a Context signs locally; wire-deserialized Data already carrying a signature short-circuits; wire Data with no Context throws `InvalidOperationException` (caught and surfaced as `MissingCallbackSignature`).
2. Defensive post-Ensure null-check: `if (Callback.RawSignature == null) → MissingCallbackSignature`.
3. `signing.verify` always runs. Tampered/wrong-identity surfaces as `CallbackSignatureMismatch`.

In-process and wire paths are now indistinguishable to the gate — absence-of-signature is rejection, never trust.

### N3 — wrap CLR exceptions out of dispatch
**File:** `PLang/App/modules/callback/run.cs`

`await cb.Run(Context)` is now wrapped to catch:
- `CallbackGoalNotFound` → `Data.FromError(..., "CallbackGoalNotFound", 404)`
- `CallbackGoalHashMismatch` → `Data.FromError(..., "CallbackGoalHashMismatch", 409)`
- `InvalidOperationException` → `Data.FromError(..., "CallbackDispatchError", 500)`

The public entry no longer leaks raw CLR exceptions to channels.

### S-F3 (Low) — wire size caps
**Files:** `PLang/App/Callback/AskCallback.cs`, `PLang/App/Callback/ErrorCallback.cs`

`AskCallback.MaxWireBytes = 1 MB`, `ErrorCallback.MaxWireBytes = 4 MB` (the latter carries snapshot, larger). `Deserialize` checks both pre-decrypt and post-decrypt sizes, throws `InvalidOperationException` on overflow. Defense-in-depth — channel layer remains the primary control.

### S-F4 (Low) — `[Sensitive]` strip on callback wires
**Files:** `PLang/App/Callback/AskCallback.cs`, `PLang/App/Callback/ErrorCallback.cs`, `PLang/App/Channels/Serializers/Serializer/PlangDataSerializer.cs`

Added `TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SensitivePropertyFilter.Strip } }` to all three `_options` blocks. Mirrors `Data.@this._envelopeJsonOptions`.

### Tests
**File:** `PLang.Tests/App/CallbackTests/CallbackRunActionTests.cs`

- `CallbackRun_VerifiesSignature_BeforeDispatch` comment updated — the in-process roundtrip now actually verifies (it didn't before). Assertion still pins `IsTrue`.
- New `CallbackRun_RejectsUnsignableData_WhenContextMissing` — Data with no signature AND no Context → `MissingCallbackSignature`. Pins the S-F1 hard gate.

## Tests

| Suite | Before v5 | After v5 |
|---|---|---|
| C# (TUnit) | 2720 / 2720 | **2721 / 2721** (+1 new) |
| Plang | 192 / 188 pass / 0 fail / 4 stale | **192 / 188 pass / 0 fail / 4 stale** |

The 4 stale (`AskVarsOnNonAsk`, `CallbackTimeoutSetting`, `DurabilityRoundTrip`, `TamperedSignature`) are preexisting handoff gaps, not v5 regressions.

## Code example

```csharp
// PLang/App/modules/callback/run.cs — the new gate
try { Callback.EnsureSigned(); }
catch (InvalidOperationException ex)
{
    return Data.@this.FromError(new ServiceError(
        $"Callback cannot be sealed for verification: {ex.Message}",
        "MissingCallbackSignature", 400));
}

if (Callback.RawSignature == null)
    return Data.@this.FromError(new ServiceError(
        "Callback has no signature after EnsureSigned — cannot verify",
        "MissingCallbackSignature", 400));

var verifyResult = await Context.App.RunAction<verify>(...);
if (!verifyResult.Success)
    return Data.@this.FromError(new ServiceError(..., "CallbackSignatureMismatch", 400));

try { return await cb.Run(Context); }
catch (CallbackGoalNotFound ex)     { return Data.@this.FromError(...); }
catch (CallbackGoalHashMismatch ex) { return Data.@this.FromError(...); }
catch (InvalidOperationException ex) { return Data.@this.FromError(...); }
```

## NOT in this version (per Ingi)

- **S-F2** — `Variables.@this.Restore` `!`-prefix filter. Ingi: "not a security issue". Note from auditor v2: security v1's suggested snippet had the `continue` condition inverted; if architect later decides to apply this filter, use auditor v2's option A or B verbatim.
- Stage 5 / HTTP wire transport, real symmetric crypto, the four stale plang tests — handoff items, separate branch.

## Suggested next bot

**codeanalyzer** — review the new try/catch shape in `run.cs` for OBP / simplification.
