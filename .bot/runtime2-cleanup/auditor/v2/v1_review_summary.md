# v1 review summary — coder addressed all 3 code findings

Auditor v1 returned FAIL with 3 minor pre-merge fixes. Coder v28 closed each
cleanly. Finding 4 was process-only; not actionable.

| Finding | Coder fix |
|---|---|
| #3 — `test/report.cs:38` Console.Out.Write | `await Context.App.CurrentActor.Channels.WriteTextAsync(global::App.Channels.@this.Output, console.ToString())`. Pattern matches `output/write.cs`. |
| #2 — `Diagnostics/@this` static-class abuse | Renamed file `Diagnostics/this.cs` → `Diagnostics/Format.cs`; class `@this` → `Format`; doc comment now explicitly notes "not named @this because there is no app.Diagnostics mount". The public method also renamed `Format(...)` → `Value(...)` to avoid `Format.Format(...)` collision (coder judgment call — clean). 4 callers updated. |
| #1 — `TypeMappingTestFacade.Json.CaseInsensitiveRead` 4th fork | `Conversion._caseInsensitiveRead` promoted from `private` to `internal` and re-exposed via `internal static JsonSerializerOptions CaseInsensitiveRead => _caseInsensitiveRead;` on `App.Types.@this`. Facade now forwards. `InternalsVisibleTo("PLang.Tests")` already wired in `PLang/PLang.csproj:44`. The `http/code/Default` copy stays independent — documented as different consumer. |
