# Baseline tests — stage 1

Captured before any code changes on `runtime2-cleanup` at 49130197 (post-pull, c18ae01c).

## C# (`dotnet run --project PLang.Tests`)
- total: 2755
- passed: 2755
- failed: 0
- duration: 15s

## PLang (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`)
- total: 199
- passed: 199
- failed: 0
- timeout: 0
- stale: 0
- skipped: 0

The six `[Fail]` lines from `_fixtures_fail/*` and `_fixtures_sensitive/*` are
fixture goals consumed by negative-path tests — they're expected to fail
internally and the consuming test passes when they do.

Build: clean from rm -rf bin/obj of PlangConsole, PLang, PLang.Tests, PLang.Generators.
448 warnings, 0 errors.

Conclusion: green baseline. Any regression on stage 1 is mine.
