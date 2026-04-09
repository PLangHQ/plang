# v1 Summary — HTTP Module Test Design

## What this is
Test suite for the HTTP module (piece 4). 62 test stubs (52 C# + 10 PLang) defining the behavioral contract for request/download/upload/configure actions and DefaultHttpProvider.

## What was done
- Analyzed architect plan v1, identified all public behaviors and edge cases
- Created 41 C# test stubs from architect spec across 5 test files
- Added 11 independent gap-coverage tests: timeout, OnProgress, null body, signed errors, application/plang+json, relative URL edge case, configure scope
- Created 10 PLang test goals (+ 1 supporting goal) across 10 subdirectories
- Follows existing patterns from signing/crypto/identity modules

### Files created
- `PLang.Tests/App/Modules/http/RequestActionTests.cs` — 27 tests (happy path, signing, streaming, timeout)
- `PLang.Tests/App/Modules/http/DownloadActionTests.cs` — 8 tests
- `PLang.Tests/App/Modules/http/UploadActionTests.cs` — 7 tests
- `PLang.Tests/App/Modules/http/ConfigureActionTests.cs` — 7 tests
- `PLang.Tests/App/Modules/http/DefaultHttpProviderTests.cs` — 4 tests
- `Tests/App/Http/*/` — 10 PLang test goals + 1 supporting goal

## Code example
```csharp
[Test]
public async Task Get_JsonResponse_DeserializesAndSetsProperties()
{
    // GET returns JSON body, Data.Value is deserialized object, Properties has StatusCode=200, IsSuccess=true
    Assert.Fail("Not implemented");
}
```

## Next step
Run the **coder** bot to implement the HTTP module and make these tests pass.
