# coder — runtime2-cleanup

## Version
v28 — Auditor v1 fix pass. Three minor findings addressed before merge.

## What this is

Auditor v1 cleared the 27-stage cleanup but flagged three pre-merge fixes
(verdict: FAIL until fixed). v28 takes them all on the same branch:

1. **Finding #3** — `test/report.cs` was still doing `Console.Out.Write` to emit
   the run summary. The branch's own thesis is channel discipline; that line
   was the one anti-thematic spot left.
2. **Finding #2** — `App/Diagnostics/this.cs` was a `public static class @this`,
   abusing the `@this` convention (which signals folder-as-instance reachable
   via `parent.Folder`). There is no `app.Diagnostics`.
3. **Finding #1** — `TypeMappingTestFacade.Json.CaseInsensitiveRead` was a 4th
   independent fork of the case-insensitive-read JSON options bag, not routed
   to either production home. Future converter additions in
   `Types/Conversion.cs` would silently drift in tests.

## What was done

### Fix #3 — report.cs channel write (1 line in source)
- `PLang/App/modules/test/report.cs:38`: replaced
  `Console.Out.Write(console.ToString());` with
  `await Context.App.CurrentActor.Channels.WriteTextAsync(global::App.Channels.@this.Output, console.ToString());`.
- Tests in `PLang.Tests/App/Testing/{ReportActionTests,EdgeCaseTests,TestMetadataTests}.cs`
  switched from `Console.SetOut(StringWriter)` capture to a
  `StreamChannel(EngineChannels.Output, MemoryStream, ...)` registered on
  `_app.User.Channels`. `[NotInParallel]` removed (no longer needed — the
  capture is per-app, not process-wide). Helper `CapturedOutput()` reads the
  memory stream as UTF-8.

### Fix #2 — Diagnostics rename
- Renamed `PLang/App/Diagnostics/this.cs` → `PLang/App/Diagnostics/Format.cs`
  (via `git mv`). Class `App.Diagnostics.@this` → `App.Diagnostics.Format`.
  Public method `Format(value)` → `Value(value)` (avoids `Format.Format(...)`).
  Doc comment now explicitly notes why the name dodges `@this`.
- 5 caller updates: `test/report.cs:280, 328`, `assert/code/Default.cs:176`,
  `Errors/AssertionError.cs:42`, `TypeMappingTestFacade.cs:74`.
  Pattern: `Diagnostics.@this.Format(x)` → `Diagnostics.Format.Value(x)`,
  `Diagnostics.@this.Options` → `Diagnostics.Format.Options`.

### Fix #1 — Route TypeMappingTestFacade to production
- `PLang/App/Types/Conversion.cs`: `_caseInsensitiveRead` flipped from
  `private` to `internal` and exposed via
  `internal static JsonSerializerOptions CaseInsensitiveRead => _caseInsensitiveRead;`.
  Doc comment updated to call out the test-surface route.
  `InternalsVisibleTo("PLang.Tests")` was already wired (`PLang.csproj:44`).
- `PLang.Tests/Support/TypeMappingTestFacade.cs:65-70`: 6-line locally-built
  bag replaced with a one-line forwarder:
  `public static JsonSerializerOptions CaseInsensitiveRead => global::App.Types.@this.CaseInsensitiveRead;`.
  Comment names the asymmetry: `http/code/Default.cs` keeps its independent
  copy by design (separate consumer).

## Code example

The pre-existing channel test pattern, now adopted by the test/report tests:

```csharp
_captureStream = new System.IO.MemoryStream();
_app.User.Channels.Register(new StreamChannel(
    EngineChannels.Output, _captureStream,
    ChannelDirection.Output, ownsStream: true)
{ Mime = "text/plain" });
// ...
private string CapturedOutput()
    => System.Text.Encoding.UTF8.GetString(_captureStream.ToArray());
```

## Verification

- Clean rebuild: `rm -rf */bin */obj && dotnet build PlangConsole` → 0 errors.
- C#: `dotnet run --project PLang.Tests` → **2752 / 2752 pass**.
- PLang: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`
  → **199 / 199 pass**.

Both suites match the auditor's verified baseline (fb8eda3b → unchanged after
v28).

## What's next

Branch is ready for merge to runtime2. All three actionable auditor findings
addressed; finding #4 (process — no tester pass) was advisory for the next
branch and stays as-is.
