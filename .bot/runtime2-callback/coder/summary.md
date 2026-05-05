# coder summary — runtime2-callback

## Version
v3 — Stage 3 (Data Lazy Signing + Per-Mimetype Serializers)

## What this is

Stage 3 of the architect's 4-stage callback design. After this stage, any Data crossing IO can transparently sign itself when a serializer reads its `Signature`; debug/value-only paths pay zero crypto cost. Builds on Stages 1+2.

## What was done

### Type rename
- `App.modules.signing.SignedData` → `App.modules.signing.Signature` (file moved + class renamed). The inner property formerly called `Signature` (the base64 sig string) renamed to `Value` to avoid the member-name-equals-enclosing-type compile error; wire JSON key stays "signature" via `[JsonPropertyName("signature")]` for backwards compatibility. Updated 45+ call sites across PLang and PLang.Tests.

### New types
- `App.Callback.@this` + `App.Callback.Signature.@this` — config holders. `app.Callback.Signature.ExpiresInMs` is the only field today (default null); Stage 4 expands.
- `App.Callback.ICallback` — empty marker interface; Stage 4 fills with `AskCallback`/`ErrorCallback` records.
- `App.Channels.Serializers.UnregisteredMimeType` — typed exception raised by `GetByMimeType` on a missing registration. Sibling-shape to ProviderRestoreException's referent-integrity model.
- `App.Channels.Serializers.Serializer.PlangDataSerializer` — new serializer for `application/plang+data`. Emits the full envelope (Type+Value+Signature). Calls `data.EnsureSigned()` before reading Signature on Write so non-callback Data still signs.

### Data lazy signature
- `Data.@this.Signature` — backing field `_signature`; getter lazy-populates ONLY when `_value is ICallback` (read directly from the field, NOT via `Value` property — DynamicData's lazy factory must not be force-computed just to check ICallback-ness; a stale read there hit the filesystem and broke unrelated tests). For non-callback values, the getter returns the field as-is so existing verify-style "if (data.Signature == null)" still fail-closed instead of auto-signing.
- `Data.@this.RawSignature` — internal accessor that never triggers populate. Used by `Ed25519Provider.VerifyAsync` and other peek sites.
- `Data.@this.EnsureSigned()` — explicit populate trigger. Throws `InvalidOperationException` when no Context. Sync-over-async dispatch through `app.RunAction<sign>(...)`. Reads `app.Callback.Signature.ExpiresInMs` only for ICallback values.

### Channel routing
- `App.Channels.Serializers.@this.GetByMimeType(string)` — throws `UnregisteredMimeType` on miss. Existing `GetByContentType` (returns null) preserved for legacy callers.
- Registered `text/html` as alias to JsonStreamSerializer (same wire shape).
- Registered `application/plang+data` → PlangDataSerializer at App boot.

### Tests filled
22 of the test-designer Stage-3 stubs:
- `SignatureRenameTests` × 2 — old name unresolved, new name exists.
- `DataLazySignatureTests` × 4 — first-access populates, cached on subsequent reads, expires seeded from app.Callback config for ICallback only.
- `DataContextWiringTests` × 3 — settable Context property pinned (per Ingi's Q1 — no constructor change), lazy expiry, throws-without-Context on EnsureSigned.
- `JsonSerializerRoundTripTests` × 3 — emits value only, never reads Signature, both text/html + application/json route to same serializer.
- `PlangDataSerializerRoundTripTests` × 5 — full envelope wire shape, lazy signing on first Signature read, round-trip preserves signature unverified, no auto-verify, mimetype routing.
- `MimeRegistrationTests` × 3 — lookup by mimetype, hard-error on unregistered, plang+data registered at boot.

### Test fixture updates
- `TestFixtures/TestProvider/TestSigningProvider.cs` and `TestFixtures/NoCtorProvider/NoCtorProvider.cs` — added IsBuiltIn + Source to satisfy the IProvider interface (Stage 1 added these). Rebuilt the DLLs and re-staged into `PLang.Tests/App/Fixtures/dlls/`.

C# baseline: 59 stubs failing (post-Stage-2) → 37. The 37 remaining are all Stage-4 stubs. PLang tests: 192/181/0fail/11stale (unchanged).

### Test-pathing change
Tests that go through the signing pipeline write to disk (identity store at `/test/.db`). Switched their App constructor to a temp-dir path so the sandbox doesn't reject filesystem writes. Older Stage-1/2 tests that don't reach signing keep using `/test`.

## Code example

The lazy signature getter:

```csharp
public Signature? Signature
{
    get
    {
        // Read _value directly (not Value) so DynamicData's factory isn't force-computed.
        if (_signature == null && _value is ICallback) EnsureSigned();
        return _signature;
    }
    set => _signature = value;
}
```

`PlangDataSerializer.Write` triggers signing for non-callback Data via the explicit `EnsureSigned()` hook, so the wire envelope always has a Signature, while JSON writes never touch it (the property is `[JsonIgnore]`).

## Next

Stage 4 — `ICallback` records (AskCallback / ErrorCallback), `Error.Callback` lazy property, `callback.run` action, `crypto.encrypt`/`decrypt` v1 pass-through. Will turn the remaining `[S4]` test-designer stubs green and unlock both integration cuts.
