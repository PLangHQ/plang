# Auditor v1 Summary — HTTP Module

## What this is
Cross-cutting audit of the HTTP module (piece 4) on `runtime2-builder-v2-http`. The HTTP module adds request/download/upload/configure actions backed by a DefaultHttpProvider that owns all HTTP behavior. Three prior reviewers (codeanalyzer, tester, security) all passed.

## What was done

### Cross-File Contract Tracing

**Signing integration (HTTP ↔ signing)**: Traced the complete round-trip across 6+ files:
- `DefaultHttpProvider.SignRequestAsync` → creates `signing.sign` record → `engine.RunAction` → `SignedData.CreateAsync` → result on `Data.Signature`
- `ApplySignature` serializes to X-Signature header
- `ParsePlangResponseAsync` deserializes with `_transportInOptions` (TransportPropertyFilter.ForInbound re-includes `[In]` properties) → creates `signing.verify` → `SignedData.VerifyAsync`
- `StreamPlangAsync` does the same verify per NDJSON line

**Result**: All contract points verified. Types match, nulls handled, errors propagate correctly.

**Config scope chain**: Verified all 15 `config.Resolve(...)` calls against Config.cs properties. All 10 property names match, all defaults align, case handling is consistent (OrdinalIgnoreCase throughout).

**Disposal lifecycle**: Engine.DisposeAsync → Providers.All() → IDisposable.Dispose on DefaultHttpProvider → `_client?.Dispose()`. Chain is complete. CallFrame disposal is correctly isolated (HTTP doesn't use it).

### Prior Review Assessment

| Bot | Verdict | My Assessment |
|-----|---------|---------------|
| Codeanalyzer v2 | PASS | **Agree** — thorough file-level analysis |
| Tester v4 | PASS | **Partial** — missed 7 weak assertions |
| Security v2 | PASS | **Agree** — correct severity ratings |

### Findings

1. **Error message integer division** (minor) — `maxBytes / (1024*1024)` gives "0MB" for 4KB MaxErrorBodySize
2. **`_client ??=` not thread-safe** (minor) — Not exploitable in current architecture, but lacks defensive protection
3. **7 weak test assertions** (minor) — Signing identity, streaming bytes, form structure tests check presence, not content
4. **ResolveUrl https assumption** (nit) — Undocumented, could frustrate local development

## Verdict: PASS

No critical or major findings. The HTTP module is well-architected, the cross-file contracts are tight, and the prior reviewers did solid work. The minor findings are quality improvements, not blockers.

## Code Example

The signing cross-file contract — the key pattern this audit verified:

```csharp
// DefaultHttpProvider.cs — creates signing action record
var httpSign = new signing.sign {
    Context = context,
    Data = bodyContent ?? "",
    Headers = new Dictionary<string, object> { ["url"] = url, ["method"] = method },
};
return await context.Engine.RunAction<signing.sign>(httpSign, context);
// ↑ Result has .Signature = SignedData (set by SignedData.CreateAsync)

// Later, verifying inbound:
Data? data = JsonSerializer.Deserialize<Data>(body, _transportInOptions);
// ↑ _transportInOptions uses TransportPropertyFilter.ForInbound → re-includes [In] props → Signature populated
var verifyAction = new signing.verify { Context = context, Data = data };
var verifyResult = await engine.RunAction<signing.verify>(verifyAction, context);
// ↑ verify.Run() → Data.Signature.VerifyAsync(this) → navigates action for contracts/headers/timeout
```

## Next Step
Suggest running the **docs** bot next.
