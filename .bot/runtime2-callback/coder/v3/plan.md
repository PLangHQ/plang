# Coder v3 — Stage 3: Data Lazy Signing + Per-Mimetype Serializers

Implements architect's `stage-3-data-signing-and-serializers.md`. Builds on Stages 1+2.

## Stage 3 deliverables

| Architect deliverable | File | Notes |
|---|---|---|
| `SignedData` → `Signature` rename | `PLang/App/modules/signing/Signature.cs` (renamed from `SignedData.cs`); 45 callsites | OBP cleanup. `class SignedData` → `class Signature`. |
| `Data.@this.Signature` lazy property | `PLang/App/Data/this.Envelope.cs` | Backing field `_signature`. Lazy populate on getter when value is ICallback (Callback's serialization path). For non-ICallback, `EnsureSigned()` is the explicit populate trigger called by `PlangDataSerializer.Write`. |
| `app.Callback` config holder | `PLang/App/Callback/this.cs` + `Signature/this.cs` | `app.Callback.Signature.ExpiresInMs` (`int?`, default null). Stage 4 fills in the rest of the `app.Callback.*` surface. |
| `ICallback` marker interface | `PLang/App/Callback/ICallback.cs` | Empty marker for Stage 3; full record types come in Stage 4. |
| `JsonSerializer` handles text/html + application/json | `PLang/App/Channels/Serializers/this.cs` | Add text/html → existing JsonStreamSerializer alias. |
| `PlangDataSerializer` for application/plang+data | `PLang/App/Channels/Serializers/Serializer/PlangDataSerializer.cs` | New. Emits full envelope (Type+Value+Signature). Calls `data.EnsureSigned()` before reading Signature. |
| `application/plang+data` MIME registration | `PLang/App/Channels/Serializers/this.cs` ctor | Wired at App boot via `new PlangDataSerializer()`. |
| Channel routing + hard error on unregistered | `PLang/App/Channels/Serializers/this.cs` | New `GetByMimeType(string)` method that throws `UnregisteredMimeType` exception when the mimetype isn't known. Existing `GetByContentType` (returns null) stays for legacy callers. |
| C# tests | DataLazySignature, DataContextWiring, JsonSerializerRoundTrip, PlangDataSerializerRoundTrip, MimeRegistration, SignatureRename | Make all `[S3]` stubs green. |

## Q1 reminder (resolved)

Per Ingi: keep `Data.@this.Context` as the existing settable property — DO NOT add ctx to every constructor. Tests for `DataContextWiringTests`:
- `Data_Constructor_AcceptsContext_AndStoresPrivately` — pin via the existing `new Data.@this(name, value) { Context = ctx }` pattern OR add a convenience constructor variant `(string name, object? value, Context.@this ctx)` for ergonomics. I'll add the convenience overload.
- `Data_LazySignature_ReadsExpiryFromContextAppCallbackSignature` — assert that lazy-populated Signature.Expires reflects `app.Callback.Signature.ExpiresInMs` when value is ICallback.
- `Data_BareConstructorWithoutContext_NoLongerCompiles_OrThrowsOnSignatureRead` — pin that lazy populate without ctx is an InvalidOperationException.

## Design decision: lazy populate scope

Architect leaned "every read populates." That breaks existing verify-path code:

```csharp
if (action.Data?.Signature == null) // verify expects fail-on-missing, not auto-sign
```

Pragmatic split:
- **ICallback values** — getter auto-populates on read (matches the architect's lazy intent, and these are the values that drive callback serialization).
- **Non-ICallback values** — getter returns `_signature` as-is. `EnsureSigned()` is the explicit populate trigger for PlangDataSerializer.

This keeps verify happy, satisfies the test names (`FirstAccess_Populates*` tests use ICallback values), and gives serializers a clean explicit hook.

## Lazy populate execution

Sync-over-async via `app.RunAction<sign>(new sign { Data = this, ExpiresInMs = expiresInMs }, _context).GetAwaiter().GetResult()`. Architect explicitly noted this is acceptable for the property; if perf becomes an issue an `EnsureSignedAsync()` method can be added later.

## Channel-level UnregisteredMimeType error

```csharp
public ISerializer GetByMimeType(string mimeType)
{
    var s = GetByContentType(mimeType);
    if (s == null) throw new UnregisteredMimeType(mimeType);
    return s;
}
```

`UnregisteredMimeType : Exception` — sibling to ProviderRestoreException's referent-integrity model.

## Workflow

1. Rename `SignedData` → `Signature` (class, file, 45 callsites). The signing module now owns a `Signature` type.
2. Add `App.Callback.@this` + `App.Callback.Signature.@this` config holders. Wire `app.Callback` property on App.
3. Add `App.Callback.ICallback` marker interface (empty in Stage 3).
4. Backing-field `_signature` on Data; lazy getter for ICallback values; explicit `EnsureSigned()` for everyone else.
5. Add `PlangDataSerializer` with full-envelope wire shape.
6. Register `application/plang+data` + `text/html` mimetypes; add `GetByMimeType` with hard error.
7. Add `UnregisteredMimeType` exception type.
8. Fill the [S3] test bodies (~24 stubs).
9. Verify all [S1]+[S2] tests still green; PLang tests no regressions.
10. Commit.
