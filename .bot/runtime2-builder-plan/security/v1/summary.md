# Security Audit v1 Summary — runtime2-builder-plan

## What this is

Full blue team + red team security audit of the runtime2-builder-plan branch (147 changed runtime source files). Covers Data/Navigators, variable resolution, builder pipeline, HTTP/file/crypto/LLM modules, engine/goal/step execution, and actor/config/cache/utils.

## What was done

Six parallel research agents analyzed all changed files. Raw findings were filtered through PLang's threat model (user-sovereign, crypto trust boundary, external data is the real attack surface). Many findings from naive analysis were filtered out — module loading, mock in prod, settings ACL, builder prompt injection are all user-sovereign actions, not attacks.

**12 findings survive filtering:**
- 3 HIGH open (all HTTP module — its core job is safe external data handling)
- 1 HIGH accepted-risk (SSTI via template rendering — known from prior audits)
- 6 MEDIUM open (crypto timing, nonce replay, JSON amplification, info disclosure, breadth explosion)
- 2 LOW open (URL scheme, type name disclosure)

### Key files with issues:
- `PLang/App/modules/http/providers/DefaultHttpProvider.cs` — findings 1, 2, 3, 11
- `PLang/App/modules/signing/providers/Ed25519Provider.cs` — findings 7, 8
- `PLang/App/Data/Navigators/JsonStringNavigator.cs` — findings 5, 6
- `PLang/App/modules/file/providers/DefaultFileProvider.cs` — finding 9
- `PLang/App/Variables/this.cs` — finding 10
- `PLang/App/modules/ui/providers/FluidProvider.cs` — finding 4 (accepted risk)

### Code example (the pattern — all 3 HTTP HIGHs share this shape):

```csharp
// StreamWithProgressAsync — no size limit on file downloads
while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
{
    await destination.WriteAsync(buffer, 0, bytesRead, ct);  // Writes forever
    bytesTransferred += bytesRead;
    // Missing: if (bytesTransferred > maxBytes) throw ...
}
```

The HTTP module protects in-memory reads (100MB limit via `ReadLimitedStringAsync`) but not file downloads, SSE streams, or slow connections. These are gaps in its primary responsibility.

### Standing finding update:
- **Fluid MaxSteps** was previously open — now configured at 100,000 (line 65). **CLOSED.**

## Verdict

**FAIL** — 3 HIGH open findings in HTTP module. Send to coder for fixes, then re-audit.
