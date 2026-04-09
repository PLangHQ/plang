# Code Analysis v1 — Summary

## What this is
Full 5-pass code analysis of all new/modified source files on `runtime2-builder-v2-http` — the HTTP module branch that also introduced identity, signing, crypto, and provider modules plus engine subsystem changes (Config rename, Providers registry, transport filters, Data envelope).

## What was done
Analyzed ~35 source files across 5 passes (OBP, Simplification, Readability, Behavioral Reasoning, Deletion Test).

**Architecture is sound.** OBP is followed consistently across all modules — actions delegate `this` to providers, providers own behavior, SignedData owns signing/verification, collections are smart. The handler pattern (thin action → provider delegation) is clean and uniform.

**3 must-fix findings:**

1. **Engine.DisposeAsync never disposes providers** (`Engine/this.cs:384-414`) — DefaultHttpProvider implements IDisposable and owns an HttpClient. Engine only iterates `_libraries.All` for disposal, not `Providers`. HttpClient leaks its SocketsHttpHandler connection pool.

2. **DefaultHttpProvider.ExecuteHttpAsync catch-all** (`DefaultHttpProvider.cs:249-261`) — The `_ => ("HttpError", 500)` fallback catches NullReferenceException, InvalidOperationException, etc. and converts them to user-visible Data errors instead of letting them crash. Makes debugging programming errors painful.

3. **DefaultIdentityProvider.LoadAllAsync swallows errors** (`DefaultIdentityProvider.cs:214-231`) — Returns empty list on DataSource failure. Downstream code sees "no identities" and auto-creates a new default, silently rotating keys on database corruption.

**2 should-fix findings:**
4. StreamPlangAsync silently skips malformed NDJSON lines (attack vector in signed streams)
5. Ed25519Provider.Verify generic catch turns OOM into "SignatureInvalid"

**Deletion test findings:** TryExtractSignedErrorIdentity (34 lines) and all 4 streaming methods have zero C# test coverage.

## Code example

The provider disposal fix (finding #1):
```csharp
// Engine/this.cs DisposeAsync — add before existing library disposal:
foreach (var provider in Providers.List())
{
    if (provider is IAsyncDisposable ad) await ad.DisposeAsync();
    else if (provider is IDisposable d) d.Dispose();
}
```

## Verdict: NEEDS WORK
Send back to coder for the 3 must-fix issues. Streaming test coverage is a tester concern.
