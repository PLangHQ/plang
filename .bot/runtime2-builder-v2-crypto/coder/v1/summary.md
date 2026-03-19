# Crypto Module Implementation — v1

## What this is

A crypto module for PLang Runtime2 providing hashing (Keccak256, SHA256) and hash verification. This is piece 2 of the builder-v2 migration. The signing module (piece 3) depends on this for data hashing. Bcrypt support is deferred.

## What was done

### Files created
- `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs` — Provider interface (`Hash`, `Verify`)
- `PLang/Runtime2/modules/crypto/providers/DefaultProvider.cs` — Built-in provider using Nethereum (Keccak256) and System.Security.Cryptography (SHA256)
- `PLang/Runtime2/modules/crypto/types.cs` — `HashedData` type (Algorithm, Format, Hash)
- `PLang/Runtime2/modules/crypto/hash.cs` — Hash action handler with JSON serialization of non-byte inputs
- `PLang/Runtime2/modules/crypto/verify.cs` — Verify action handler with hex decode and provider dispatch

### Files modified
- `PLang.Tests/Runtime2/Modules/crypto/HashActionTests.cs` — 12 stubs replaced with real tests
- `PLang.Tests/Runtime2/Modules/crypto/ProviderResolutionTests.cs` — 3 stubs replaced
- `PLang.Tests/Runtime2/Modules/crypto/DefaultProviderTests.cs` — 4 bcrypt tests skipped

### Key decisions

1. **Provider resolution via MemoryStack, not DataSource** — The plan called for DataSource, but DataSource serializes to JSON (SQLite). An `ICryptoProvider` can't survive JSON round-trip. Changed to `Context.MemoryStack.Get("CryptoProvider")` which stores object references in-memory.

2. **Static helper methods on Hash** — `SerializeData()`, `FormatHash()`, and `ResolveProvider()` are `internal static` on the `Hash` class so the `Verify` handler reuses them. The `Verify.Hash` property shadowed the `Hash` class name, requiring `crypto.Hash.` qualification.

## Code example

```csharp
// hash.cs — action handler pattern
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
            var provider = ResolveProvider(Context);
            var (bytes, format) = SerializeData(Data);
            var hashBytes = provider.Hash(bytes, Algorithm);
            return Data.Ok(new HashedData { Algorithm = Algorithm.ToLowerInvariant(), Format = format, Hash = FormatHash(hashBytes) });
        }
        catch (NotSupportedException ex) { return Data.FromError(new ActionError(ex.Message, "UnsupportedAlgorithm", 400)); }
        catch (Exception ex) { return Data.FromError(ActionError.FromException(ex, "CryptoError", 500)); }
    }
}
```

## Status

- All 26 crypto tests pass, 4 bcrypt skipped
- Full suite: 1675 pass, 0 fail
- PLang tests deferred until module is registered with the builder (piece 8)
