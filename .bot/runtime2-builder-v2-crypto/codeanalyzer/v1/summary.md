# Code Analysis v1 — Summary

## What this is
Code quality review of the crypto module (hash/verify handlers, ICryptoProvider, DefaultProvider) and Engine.Providers (type-keyed pluggable registry), plus identity module changes merged from the identity branch.

## What was done
5-pass analysis (OBP compliance, simplification, readability, behavioral reasoning, deletion test) across 12 files.

**Architecture is clean.** Engine.Providers is a minimal, well-designed registry. Crypto handlers are thin — resolve provider, serialize data, delegate. OBP compliance is good throughout. Identity wiring (SensitivePropertyFilter, Actor.Identity, DynamicData) is correct.

**4 findings, 1 medium:**

1. **DefaultProvider.Verify duplicates algorithm validation (medium)** — Lines 18-27 manually check for keccak256/sha256 before calling Hash(), but Hash() already validates and throws for unsupported algorithms. When a new algorithm is added to Hash() but not Verify's if-guard, Verify breaks silently. Fix: `var actual = Hash(data, algorithm); return actual.AsSpan().SequenceEqual(expectedHash);`

2. **new DefaultProvider() per call (low)** — hash.cs:62 allocates a stateless DefaultProvider on every hash/verify. Use `private static readonly`.

3. **Property `Data` shadows type `Data` (low)** — Forces `Engine.Memory.Data.Ok(...)` instead of `Data.Ok(...)` everywhere in hash.cs and verify.cs. Design trade-off — "Data" is the right domain name for what you're hashing.

4. **Double ToLowerInvariant() (low)** — DefaultProvider.cs:20 calls it twice on the same string.

## Code example
The main finding — DefaultProvider.Verify before and after:

```csharp
// Current (redundant guard):
public bool Verify(byte[] data, byte[] expectedHash, string algorithm)
{
    if (algorithm.ToLowerInvariant() == "keccak256" || algorithm.ToLowerInvariant() == "sha256")
    {
        var actual = Hash(data, algorithm);
        return actual.AsSpan().SequenceEqual(expectedHash);
    }
    throw new NotSupportedException($"Algorithm '{algorithm}' is not supported");
}

// Simplified (Hash() already validates):
public bool Verify(byte[] data, byte[] expectedHash, string algorithm)
{
    var actual = Hash(data, algorithm);
    return actual.AsSpan().SequenceEqual(expectedHash);
}
```

## Verdict: NEEDS WORK
Send back to coder for fixes. All findings are straightforward.
