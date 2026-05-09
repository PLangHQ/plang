# auditor v2 result — runtime2-cleanup coder fix-pass close-out

## Verdict: PASS

Coder v28 (`033c8290`) closed all three v1 code findings cleanly. Both test
suites green on a clean rebuild. Branch is ready for merge to runtime2.

## Fixes verified

### Finding #3 — `test/report.cs:38`

Was `Console.Out.Write(console.ToString());`.
Now `await Context.App.CurrentActor.Channels.WriteTextAsync(global::App.Channels.@this.Output, console.ToString())`.

Verified: `grep "Console.Out.Write\|Console.Error.Write\|Console.Write" PLang/App/modules/` returns zero hits. The branch's channel-discipline thesis now actually closes — the report module routes through Channels like every other module.

### Finding #2 — `Diagnostics/this.cs` rename

- File renamed `Diagnostics/this.cs` → `Diagnostics/Format.cs`.
- Class `App.Diagnostics.@this` → `App.Diagnostics.Format`.
- Public method `Format(object?)` → `Value(object?)` to dodge the `Format.Format(...)` collision (coder judgment call — clean).
- Doc comment now explicitly states "not named @this because there is no app.Diagnostics mount" — exactly the conversational signal the convention should send.
- 4 callers updated: `AssertionError.cs:42`, `assert/code/Default.cs:176`, `test/report.cs:280` and `:328`, `TypeMappingTestFacade.cs:73`.

Verified: `grep "Diagnostics\.@this"` returns zero hits in production and tests. `grep "Diagnostics\.Format"` returns the 5 expected sites.

### Finding #1 — `TypeMappingTestFacade.Json.CaseInsensitiveRead`

`Conversion._caseInsensitiveRead` promoted to `internal static readonly` (was `private`); new `internal static JsonSerializerOptions CaseInsensitiveRead => _caseInsensitiveRead;` accessor added. Doc comment updated to spell out the test-facade contract.

`TypeMappingTestFacade.Json.CaseInsensitiveRead` now forwards: `=> global::App.Types.@this.CaseInsensitiveRead`. The fourth fork is gone; the facade pins the production conversion-path bag, so a converter added there is exercised by tests automatically. The `http/code/Default` copy stays independent — different consumer, documented.

`InternalsVisibleTo("PLang.Tests")` already wired in `PLang/PLang.csproj:44`, so the routing compiles cleanly.

## Verification

```
$ git rev-parse HEAD
033c8290...
$ rm -rf */bin */obj && dotnet build PlangConsole          → green (445 warnings, 0 errors)
$ dotnet run --project PLang.Tests                          → 2752/2752 pass
$ cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test → 199/199 pass
```

## Carry-over

- v1 finding #4 (no tester report on a 465-file branch) — process advisory; not actionable on this branch.

## Recommendation

Branch is in shape. Next step is docs.
