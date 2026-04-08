# Security Audit Summary — system-goals-architecture v1

## What this is

Full blue+red team security audit of the new `PLang/App/` architecture — a near-complete rewrite (809 production C# files changed) from Runtime2 to App namespace. Assessed against PLang's user-sovereign threat model: .pr files are trusted, defend against external data, don't flag user actions as attacks.

## What was done

Audited 10 attack surface areas across 30+ files. Produced 12 findings: 2 high, 4 medium, 6 low.

### HIGH findings

1. **Binding.Run missing try-finally** (`Events/Lifecycle/Bindings/Binding/this.cs:34`): If event handler throws, `ExitEvent()` never runs. Binding becomes permanently dead — silently skipped on all future invocations. If the binding is a security guard (BeforeAction permission check), all subsequent actions bypass it. Fix: wrap in try-finally.

2. **Variable expansion from untrusted sources** (`Variables/this.cs:265-275`, `modules/file/read.cs:25`): `Resolve()` expands `%!app.AbsolutePath%`, `%!app.Goals%` etc. from any string. When used on external data (file content with ResolveVariables=true, or HTTP response bodies), attackers can probe internal state. Fix: add untrusted flag that skips `%!%` infrastructure variables.

### MEDIUM findings

3. HTTP header injection via `TryAddWithoutValidation()` — CRLF in header values
4. Event handlers execute outside step timeout — can hang indefinitely
5. LLM cache key missing actor isolation — cross-actor info leakage
6. `/system/` path fallback may allow traversal after prefix extraction

### What's solid

- **Signing**: Ed25519 with `internal set`, 9-step verification, nonce replay, thread-safe ToSigningBytes
- **CallStack**: MaxDepth=1000 with proper CallStackOverflowException
- **Decompression**: 100MB limit with per-chunk enforcement
- **HTTP**: Size-limited reads (ReadLimitedStringAsync/ReadLimitedBytesAsync)
- **LLM tools**: Strict whitelist — LLM can only call pre-declared goals
- **Step execution**: try-catch wrapping actions, timeout support, proper cancellation propagation

### Key code examples

**Finding 1 fix** — `Binding/this.cs:29-35`:
```csharp
// BEFORE (vulnerable):
var result = await Handler(context);
context.ExitEvent(Id);

// AFTER (fixed):
Data.@this result;
try { result = await Handler(context); }
finally { context.ExitEvent(Id); }
```

**Finding 2 pattern** — `file/read.cs:23-26`:
```csharp
// This is unsafe when reading untrusted files:
if (ResolveVariables && result.Success && result.Value is string content)
{
    var resolved = Context.Variables.Resolve(content);  // expands %!app% from file content
}
```

## Verdict

**FAIL** — 2 high findings open. Recommend sending to coder for fixes on findings #1 and #2, then re-audit.

## Files modified

None (audit only — no code changes).
