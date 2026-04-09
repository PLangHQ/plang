# Tester v2 Summary — HTTP Module Test Quality Analysis

## What this is
Test quality analysis for the HTTP module (coder v2). The module has 4 actions, a DefaultHttpProvider (984 lines), config, types, and integration with the signing module. 54 C# tests exist, all passing.

## What was done
- Ran full C# test suite: 1904 passed, 2 failed (pre-existing DLL fixture issue), 8 skipped
- Ran coverage analysis via Cobertura
- Deep analysis of all 5 HTTP test files against source code
- False-green hunting across all 54 tests

## Key Finding: Mocked at Wrong Level

The entire HTTP test suite replaces `DefaultHttpProvider` with a `MockHttpProvider` that returns canned `Data` results. The action `Run()` methods are 3-line delegators:

```csharp
public async Task<Data> Run()
{
    var provider = Context.Engine.Providers.Get<IHttpProvider>();
    if (!provider.Success) return provider;
    return await provider.Value!.SendAsync(this);
}
```

Tests verify that these 3 lines work (100% coverage on actions), but the provider — which contains **all** the actual logic — has **5.7% line coverage**. Only `Configure()` and `Dispose()` are tested on the real provider.

## Untested Production Code (0% coverage)

| Method | Lines | Risk |
|--------|-------|------|
| SendAsync | ~70 | URL resolution, body building, signing, response dispatch |
| DownloadAsync | ~65 | File existence check, stream-to-disk, progress |
| UploadAsync | ~40 | Content resolution, signing, response parsing |
| ExecuteHttpAsync | ~20 | Exception-to-Data mapping (5 exception types) |
| ParseResponseAsync | ~80 | Content-type dispatch (plang/json/xml/text/binary) |
| ParsePlangResponseAsync | ~50 | Signature verification, !ServiceIdentity |
| HandleStreamingAsync | ~65 | Format detection, error check, plang rejection |
| StreamLinesAsync/SSE/Bytes/Plang | ~115 | All stream processing |
| SignRequestAsync | ~25 | Signing action construction and execution |
| ResolveUrl | ~20 | Protocol prefix, relative URL + BaseUrl |
| MergeHeaders + ApplyHeaders | ~30 | Header merging and content/request split |
| BuildProperties | ~35 | Response metadata population |
| ResolveUploadContentAsync | ~40 | Auto-detection logic |

## False Greens (5 identified)

1. **Get_NoProtocol_AutoPrefixesHttps** — mock returns success regardless, URL prefix never verified
2. **Get_RelativeUrlNoBaseUrl_ReturnsError** — mock hardcodes error, real provider not tested
3. **Get_ApplicationPlangJsonVariant_DetectedByProvider** — mock's if-statement tested, not provider's
4. **Configure_PerStepOverridesConfig** — claims to test per-step override but never verifies it
5. **Get_ApplicationPlangResponse_ProviderHandlesVerification** — mock writes to memory, not provider

## Verdict: FAIL

Send back to **coder** with instruction: create `TestableHttpProvider` subclass that injects a mock `HttpMessageHandler` but runs all real provider logic. Re-implement tests against real provider behavior.
