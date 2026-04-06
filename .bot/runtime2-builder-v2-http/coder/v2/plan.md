# v2 Plan — OBP Refactor of HTTP Module

## Context
Fresh-eyes review found OBP violations in the HTTP module. The action records and provider interface are clean, but `HttpHelper` (static utility class) pulls pipeline behavior out of its owner and takes decomposed parameters everywhere. This plan addresses findings #1, #2, #4, #5, #6 from the review. Finding #3 (signing record construction) is deferred for discussion.

## Changes

### 1. Eliminate HttpHelper — move behavior to DefaultHttpProvider
**Files:** `HttpHelper.cs` (delete), `DefaultHttpProvider.cs` (modify)

Move all pipeline methods from `HttpHelper` into `DefaultHttpProvider` as private instance methods. The provider already receives the action record (`this`) and can navigate to engine, context, config. No need for a static intermediary.

Keep as static utilities (tiny, truly stateless):
- `ToSystemMethod()` — enum conversion, no owner dependency
- `IsContentHeader()` — string classification

These move to a minimal static section at the bottom of `DefaultHttpProvider`, or stay in a stripped-down `HttpHelper` with just these two.

### 2. Pass action records instead of decomposed fields
**File:** `DefaultHttpProvider.cs`

Instead of extracting `unsigned`, `timeout`, `url`, `method`, `bodyString` and passing them individually to helper methods, pass the action record. Each internal method navigates what it needs.

Before:
```csharp
var signResult = await HttpHelper.SignRequestAsync(
    engine, action.Context, resolvedUnsigned, action.SignOptions,
    bodyString, resolvedUrl, httpMethod.Method);
```

After:
```csharp
var signResult = await SignRequestAsync(action, resolvedUrl, bodyString);
// SignRequestAsync navigates action.Context, action.Unsigned, action.SignOptions internally
```

Note: `resolvedUrl` and `bodyString` are computed values, not direct action fields — they're passed as parameters because they're derived state. The action record, engine, and config are navigated.

### 3. Deduplicate config resolution
**File:** `DefaultHttpProvider.cs`

The pattern `action.Unsigned || config.Resolve("Unsigned", false)` repeats in SendAsync, DownloadAsync, UploadAsync. Extract a private method that resolves common settings from action + config:

```csharp
private (bool unsigned, int timeout) ResolveCommon<T>(T action, ModuleView<Config> config)
    where T : IContext { ... }
```

Or better: each internal method navigates the action and config directly — no intermediate struct needed. The point is one code path, not three copies.

### 4. Deduplicate error handling
**File:** `DefaultHttpProvider.cs`

Extract a private method for the common try/catch pattern:
```csharp
private async Task<Data> ExecuteWithErrorHandling(Func<Task<Data>> operation)
```
Catches `TaskCanceledException`, `HttpRequestException`, `IOException`, `UnauthorizedAccessException` uniformly.

### 5. Fix hardcoded "!data" in progress callback
**File:** `DefaultHttpProvider.cs` (moved from HttpHelper)

`StreamWithProgressAsync` hardcodes `"!data"` as the variable name. It should resolve from `OnProgress.Parameters` the same way streaming resolves `dataVarName` from `OnStream.Parameters`.

### 6. Eliminate duplicate FollowRedirects/MaxRedirects state
**File:** `DefaultHttpProvider.cs`

Currently `_followRedirects` and `_maxRedirects` duplicate what's in the config scope chain. The provider should read from config when creating the client, not maintain its own shadow copies.

### 7. Add `using` on HttpResponseMessage
**File:** `DefaultHttpProvider.cs`

`SendAsync` doesn't dispose `HttpResponseMessage`. Add `using` to prevent resource leaks.

## Files touched
- `PLang/App/modules/http/DefaultHttpProvider.cs` — major refactor (absorb HttpHelper)
- `PLang/App/modules/http/HttpHelper.cs` — delete or reduce to 2 static utilities
- `PLang.Tests/App/Modules/http/` — update tests if method signatures change

## Not in scope
- Finding #3 (signing record construction) — deferred for discussion
- No new functionality — pure OBP compliance refactor
- No changes to action records, IHttpProvider interface, types, or Config

## Risk
- Tests may need updating if internal mocking relied on HttpHelper being static/public
- Streaming behavior is complex — need to verify all paths still work after move
