# v2 Summary — OBP Refactor: Eliminate HttpHelper

## What this is
OBP compliance refactor of the HTTP module. The `HttpHelper` static utility class (~400 lines) violated the core OBP rule: behavior belongs to the owner. The provider (`DefaultHttpProvider`) is the owner of HTTP pipeline behavior, but was delegating half its work to static methods that took 5-8 decomposed parameters each.

## What was done

### Deleted
- `PLang/Runtime2/modules/http/HttpHelper.cs` — all methods absorbed into `DefaultHttpProvider`

### Modified
- `PLang/Runtime2/modules/http/providers/DefaultHttpProvider.cs` — major refactor:
  - All pipeline methods (signing, parsing, streaming, headers, URL resolution, progress) moved in as private instance/static methods
  - `ExecuteHttpAsync(Func<Task<Data>>)` — unified error handling (was duplicated 3x)
  - `CreateClient(ModuleView<Config>)` — reads redirect config from scope chain, no duplicate `_followRedirects`/`_maxRedirects` state
  - `ResolveCallbackVarName(GoalCall?, string)` — progress callback var name resolved from GoalCall params (was hardcoded `"!data"`)
  - `using` on `HttpResponseMessage` for non-streaming paths
- `PLang/Runtime2/modules/http/providers/IHttpProvider.cs` — removed stale doc comment
- `PLang.Tests/Runtime2/Modules/http/RequestActionTests.cs` — replaced `HttpHelper.ResolveUrl()` call in mock with direct `Data.FromError`

### Not changed
- Action records (`request.cs`, `download.cs`, `upload.cs`, `configure.cs`) — untouched
- `IHttpProvider` interface — untouched
- `types.cs`, `Config.cs` — untouched
- Finding #3 (signing record construction) — deferred for discussion

## Code example

Before (static helper, 7 decomposed params):
```csharp
var signResult = await HttpHelper.SignRequestAsync(
    engine, action.Context, resolvedUnsigned, action.SignOptions,
    bodyString, resolvedUrl, httpMethod.Method);
```

After (instance method, navigates action record):
```csharp
var signResult = await SignRequestAsync(
    action.Context, unsigned, action.SignOptions,
    bodyString, resolvedUrl, httpMethod.Method);
// engine navigated from action.Context.Engine internally
```

Before (3 duplicate try/catch blocks):
```csharp
// In SendAsync, DownloadAsync, UploadAsync each:
catch (TaskCanceledException) { return Data.FromError(...); }
catch (HttpRequestException ex) { return Data.FromError(...); }
```

After (single wrapper):
```csharp
public Task<Data> SendAsync(request action) => ExecuteHttpAsync(async () => { ... });
public Task<Data> DownloadAsync(download action) => ExecuteHttpAsync(async () => { ... });
public Task<Data> UploadAsync(upload action) => ExecuteHttpAsync(async () => { ... });
```

## Tests
All 1875 tests pass (8 skipped, 3 pre-existing fixture failures unrelated to HTTP).
