# coder v5 plan — security fixes pass

Responding to **auditor v2** (and security v1) findings on `runtime2-callback`. Per Ingi: fix S-F1, S-F3, S-F4, plus N3. Skip S-F2 (Ingi: "not a security issue", leave for architect).

## Baseline (locked in)

C# 2720/2720, Plang 192/188 pass / 0 fail / 4 stale. See `baseline-tests.md`.

## Changes

### 1. S-F1 (Medium) — `callback.run` always signs+verifies
**File:** `PLang/App/modules/callback/run.cs`

Replace the `if (RawSignature != null) verify` gate with:
- Call `Callback.EnsureSigned()` first. For in-process Data with a Context, this signs locally with the runtime identity. For wire-deserialized Data without Context, EnsureSigned throws `InvalidOperationException`.
- Catch the throw and return `Data.FromError(ServiceError "callback cannot be signed", "MissingCallbackSignature", 400)`.
- After EnsureSigned, treat `RawSignature == null` as a hard reject (defensive — should not happen).
- Always invoke `signing.verify`. Tampered/wrong-identity signatures still surface as `CallbackSignatureMismatch`.

Net effect: in-process and wire paths look identical to the gate; absence-of-signature is no longer trust.

### 2. N3 — wrap CLR exceptions from Restore at the public entry
**File:** `PLang/App/modules/callback/run.cs`

Wrap `await cb.Run(Context)` in a try/catch for `CallbackGoalNotFound` / `CallbackGoalHashMismatch` / `InvalidOperationException`. Surface as `Data.FromError(ServiceError ..., "...", 400)`. Channel/handler caller no longer sees raw CLR exceptions out of dispatch.

### 3. S-F3 (Low) — wire-size cap on callback deserialization
**Files:** `PLang/App/Callback/AskCallback.cs`, `PLang/App/Callback/ErrorCallback.cs`

Add a const `MaxWireBytes = 1 * 1024 * 1024` (1 MB — defense-in-depth; channel layer is the primary control). On `Deserialize`, if `bytes.Length > MaxWireBytes`, throw `InvalidOperationException("callback wire payload exceeds N MB")`. Cheap, no JsonReaderOptions surgery needed.

### 4. S-F4 (Low) — `[Sensitive]` strip on callback wires
**Files:** `PLang/App/Callback/AskCallback.cs`, `PLang/App/Callback/ErrorCallback.cs`, `PLang/App/Channels/Serializers/Serializer/PlangDataSerializer.cs`

Add `TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SensitivePropertyFilter.Strip } }` to each `_options` block. One-line each. Mirrors `Data.@this._envelopeJsonOptions`.

### 5. Test updates
**File:** `PLang.Tests/App/CallbackTests/CallbackRunActionTests.cs`

- `CallbackRun_VerifiesSignature_BeforeDispatch`: comment claims "skips verify"; behaviour after fix is "auto-signs locally, verify passes". Update the comment + keep `IsTrue()` — the assertion already pins the right shape (in-process roundtrip succeeds).
- Add `CallbackRun_RejectsUnsignableData_WhenContextMissing`: Data with no Context → EnsureSigned can't run → returns `MissingCallbackSignature`.

## Out of scope this pass
- S-F2 (Variables.Restore !-prefix filter) — Ingi says not a security issue.
- HTTP/channel callback wiring (Stage 5 architect work).
- Real symmetric crypto.
- Stale plang tests (handoff items 10-13).

## Test plan
1. Clean rebuild.
2. `dotnet run --project PLang.Tests` — must remain 2720/2720.
3. `cd Tests && plang --test` — 188 pass / 0 fail / 4 stale unchanged.
4. Commit, update summary.md.
