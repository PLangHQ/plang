# Code Analysis v1 — Summary

## What this is
Full 5-pass code analysis of the signing, crypto, identity, and provider modules on `runtime2-builder2-signing`. This is a post-coder-fix review — the coder already addressed 7 OBP violations. I analyzed ~30 files across 4 modules for remaining issues.

## What was done
Analyzed all new/modified source files through OBP compliance, simplification, readability, behavioral reasoning, and deletion test passes.

**8 findings, 2 high-severity:**

1. **IKeyProvider.GenerateKeyPair() returns KeyPair, not Data** (HIGH) — The only provider interface that doesn't return Data. This is the exact pattern Ingi flagged on the crypto branch. Ed25519Provider.GenerateKeyPair() can throw CryptographicException unhandled. The caller (DefaultIdentityProvider.GenerateIdentity) wraps in try/catch, which is the wrong-level fix.

2. **EngineProviders generic/non-generic duplication** (MEDIUM) — Register, Remove, SetDefault, List each have near-identical generic and non-generic versions. Generic should delegate to non-generic (~50 lines eliminated).

3. **DefaultIdentityProvider "get by name or default" duplicated** (MEDIUM) — GetAsync and ExportAsync share identical resolution logic. Extract to ResolveIdentityAsync.

4. **Bare catch in Deserialize** (MEDIUM) — `catch { return null; }` swallows all exceptions. Narrow to `catch (JsonException)`.

5-8: Low-severity readability/simplification items (VerifyAsync length, ResolveType default, provider module pattern repetition).

## Code example — Finding #1

```csharp
// Current (IKeyProvider.cs)
public interface IKeyProvider : IProvider
{
    KeyPair GenerateKeyPair();  // throws on failure
}

// Should be (consistent with ICryptoProvider, ISigningProvider):
public interface IKeyProvider : IProvider
{
    Data<KeyPair> GenerateKeyPair();  // returns Data on all paths
}
```

## Status
**NEEDS WORK** — Send back to coder for fixes #1-4.
