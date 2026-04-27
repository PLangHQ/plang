# v1 Summary — HTTP Module Implementation

## What this is
HTTP client module for PLang App. 4 actions (request, download, upload, configure), IHttpProvider interface with DefaultHttpProvider, shared HttpHelper utilities, and full C# test coverage. Integrates with the signing module for request signing and application/plang response verification.

## What was done

### Module files created (9 files)
- `PLang/App/modules/http/types.cs` — HttpMethod, StreamFormat, ContentAs, FileExists enums + TransferProgress record
- `PLang/App/modules/http/Config.cs` — ISettings with defaults (timeout, baseUrl, headers, etc.)
- `PLang/App/modules/http/providers/IHttpProvider.cs` — Provider interface (SendAsync + Configure)
- `PLang/App/modules/http/providers/DefaultHttpProvider.cs` — Lazy HttpClient + SocketsHttpHandler
- `PLang/App/modules/http/HttpHelper.cs` — Shared static helpers (URL resolution, header merging, signing, response parsing, streaming)
- `PLang/App/modules/http/request.cs` — Core HTTP action (all methods, signing, streaming, response parsing)
- `PLang/App/modules/http/download.cs` — File download with FileExists enum
- `PLang/App/modules/http/upload.cs` — Content upload with auto-detection
- `PLang/App/modules/http/configure.cs` — Settings management via scope chain

### Engine modifications (2 files)
- `PLang/App/this.cs` — Register DefaultHttpProvider, add non-generic `RunAction<TAction>()` overload
- `PLang/App/Providers/this.cs` — Add "http" to ResolveType()

### Source generator fix (1 file)
- `PLang.Generators/LazyParamsGenerator.cs` — Fix enum default values: emit `(EnumType)intValue` cast instead of raw int

### Tests implemented (5 C# files + 10 PLang goals)
- `RequestActionTests.cs` — 28 tests (happy path, signing, streaming, timeout)
- `DownloadActionTests.cs` — 8 tests
- `UploadActionTests.cs` — 7 tests
- `ConfigureActionTests.cs` — 7 tests
- `DefaultHttpProviderTests.cs` — 4 tests
- `Tests/App/Http/*/` — 10 PLang test goals + 1 supporting goal

**All 1889 tests pass (0 failures, 8 skipped).**

## Code example
```csharp
// request.cs — the core HTTP action pattern
[Action("request")]
public partial class request : IContext
{
    public partial string Url { get; init; }
    [Default(HttpMethod.GET)]
    public partial HttpMethod Method { get; init; }
    public partial object? Body { get; init; }
    [Default(false)]
    public partial bool Unsigned { get; init; }
    public partial GoalCall? OnStream { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        var config = engine.Settings.For<Config>(Context);
        var urlResult = HttpHelper.ResolveUrl(Url, config);
        if (!urlResult.Success) return urlResult;

        // Sign via engine.RunAction — relay the Data object, don't decompose
        var signResult = await HttpHelper.SignRequestAsync(engine, Context, ...);
        if (signResult != null)
        {
            if (!signResult.Success) return signResult;
            HttpHelper.ApplySignature(requestMessage, signResult); // navigates .Signature
        }
        // ...
    }
}
```

## Key design decisions
- **OBP signing flow**: `engine.RunAction<sign>()` returns Data with `.Signature` — relayed as-is, never decomposed
- **Non-generic RunAction**: Added `RunAction<TAction>()` overload that preserves full Data (Signature, Properties) — needed when Value isn't the result you want
- **XML stored as-is**: `Type.FromMime("application/xml")` — Data layer handles dot-access navigation
- **Enum default fix**: Source generator now casts enum defaults properly
