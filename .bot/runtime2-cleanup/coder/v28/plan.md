# v28 plan — auditor fix pass

## Fix #3 (report.cs Console.Out.Write)

`PLang/App/modules/test/report.cs:38` — replace
`Console.Out.Write(console.ToString());`
with
`await Context.App.CurrentActor.Channels.WriteTextAsync(global::App.Channels.@this.Output, console.ToString());`

`Run()` is already `async Task<Data.@this>`; `Context.App` is in scope. Pattern
matches `modules/builder/code/Default.cs:192` and `modules/llm/code/OpenAi.cs:363`.

## Fix #2 (Diagnostics rename)

- Rename `PLang/App/Diagnostics/this.cs` → `PLang/App/Diagnostics/Format.cs`.
- Class `App.Diagnostics.@this` → `App.Diagnostics.Format`.
- Update doc comment ("Static class because…" stays — but drop the `@this`-convention
  language; that was the smell).
- Update callers (4 sites):
  - `PLang/App/modules/test/report.cs:280` `Diagnostics.@this.Options` → `Diagnostics.Format.Options`
  - `PLang/App/modules/test/report.cs:328` `Diagnostics.@this.Format(...)` → `Diagnostics.Format.Format(...)` *(note: nested name; the public method is also `Format` — that's the cost of the rename, but unambiguous)*
  - `PLang/App/modules/assert/code/Default.cs:176` same swap
  - `PLang/App/Errors/AssertionError.cs:42` same swap
  - `PLang.Tests/Support/TypeMappingTestFacade.cs:74` same swap

## Fix #1 (CaseInsensitiveRead routing)

- In `PLang/App/Types/Conversion.cs`: change `_caseInsensitiveRead` from
  `private static readonly` to `internal static readonly` (keep the same name and
  initialiser). Add an `internal static JsonSerializerOptions CaseInsensitiveRead => _caseInsensitiveRead;`
  exposed on `App.Types.@this`.
- Update doc comment: explicitly note the test facade now routes here (so a
  future contributor adding a converter knows the test surface is pinned).
- In `PLang.Tests/Support/TypeMappingTestFacade.cs:65-70`: replace the locally
  constructed `CaseInsensitiveRead` with a forwarder to
  `global::App.Types.@this.CaseInsensitiveRead`. Use `InternalsVisibleTo` if not
  already wired — check assembly references first.
- Leave `http/code/Default.cs:55`'s separate copy alone (independent consumer;
  per-consumer ownership is the documented choice for the production sites).

## Verify

1. `rm -rf */bin */obj && dotnet build PlangConsole`
2. `dotnet run --project PLang.Tests` — expect 2752/2752
3. `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` — expect 199/199

## Commit

`coder v28: auditor fixes — report.cs channel, Diagnostics rename, CaseInsensitiveRead routed`
