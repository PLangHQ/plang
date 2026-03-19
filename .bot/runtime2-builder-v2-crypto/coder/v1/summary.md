# Crypto Module Implementation — v1

## What this is

A crypto module for PLang Runtime2 providing hashing (Keccak256, SHA256) and hash verification, plus `Engine.Providers` — a generic type-keyed registry for pluggable module implementations. This is piece 2 of the builder-v2 migration. The signing module (piece 3) depends on this for data hashing. Bcrypt support is deferred.

## What was done

### Files created
- `PLang/Runtime2/Engine/Providers/this.cs` — `Engine.Providers`, type-keyed `ConcurrentDictionary<Type, object>` with `Register<T>`, `Get<T>`, `GetOrDefault<T>`. Solves Foundation Checklist item #4 (pluggable action implementations).
- `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs` — Provider interface (`Hash`, `Verify`)
- `PLang/Runtime2/modules/crypto/providers/DefaultProvider.cs` — Built-in provider using Nethereum (Keccak256) and System.Security.Cryptography (SHA256)
- `PLang/Runtime2/modules/crypto/types.cs` — `HashedData` type (Algorithm, Format, Hash)
- `PLang/Runtime2/modules/crypto/hash.cs` — Thin hash action handler: resolve provider, serialize, delegate
- `PLang/Runtime2/modules/crypto/verify.cs` — Thin verify action handler: resolve provider, decode hex, delegate

### Files modified
- `PLang/Runtime2/Engine/this.cs` — Added `Providers` property
- `PLang/Runtime2/GlobalUsings.cs` — Added `EngineProviders` alias
- `PLang.Tests/Runtime2/Modules/crypto/HashActionTests.cs` — 12 stubs → real tests
- `PLang.Tests/Runtime2/Modules/crypto/ProviderResolutionTests.cs` — 3 stubs → real tests
- `PLang.Tests/Runtime2/Modules/crypto/DefaultProviderTests.cs` — 4 bcrypt tests skipped
- `Documentation/Runtime2/todos.md` — Updated item #4, added Libraries retirement todo

### Key decisions

1. **Engine.Providers for pluggable implementations** — Design evolved during session. Plan called for DataSource, but DataSource serializes to JSON (can't store object references). Discussed with Ingi → designed `Engine.Providers` as the generic solution. A PLang developer loads a DLL implementing `ICryptoProvider`, the handler picks it up via `Engine.Providers.GetOrDefault<ICryptoProvider>(new DefaultProvider())`.

2. **Handlers are thin** — Hash and verify handlers just resolve the provider and delegate. All crypto logic lives in the provider. This pattern applies to any module (condition, DB, templating, etc.).

3. **Static helper methods on Hash** — `SerializeData()`, `FormatHash()`, and `ResolveProvider()` are `internal static` on the `Hash` class so the `Verify` handler reuses them.

## Code example

```csharp
// Engine.Providers — generic pattern for any module
engine.Providers.Register<ICryptoProvider>(new DefaultProvider());  // module default
engine.Providers.Register<ICryptoProvider>(new MyKmsProvider());    // PLang developer override

// hash.cs — thin handler delegates to provider
[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    public partial object? Data { get; init; }
    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    public async Task<Data> Run()
    {
        if (Data == null)
            return Data.FromError(new ActionError("Data cannot be null", "ValidationError", 400));
        try
        {
            var provider = context.Engine.Providers.GetOrDefault<ICryptoProvider>(new DefaultProvider());
            var (bytes, format) = SerializeData(Data);
            var hashBytes = provider.Hash(bytes, Algorithm);
            return Data.Ok(new HashedData { Algorithm = Algorithm.ToLowerInvariant(), Format = format, Hash = FormatHash(hashBytes) });
        }
        catch (NotSupportedException ex) { ... }
        catch (Exception ex) { ... }
    }
}
```

## Status

- All 26 crypto tests pass, 4 bcrypt skipped
- Full suite: 1675 pass, 0 fail
- PLang tests deferred until module is registered with the builder (piece 8)
