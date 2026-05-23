# v8 baseline

**Commit:** `b9be51121` (post-tester-v7)

## C#
- `dotnet run --project PLang.Tests`
- **Total 2889 / Passed 2889 / Failed 0**
- First run flaked 1 test (`.build/` discovery race during validation
  setup); re-run was clean. No persistent reds.

## PLang
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`
- **Total 203 / Pass 203 / Fail 0 / Stale 0**

## Build
- `dotnet build PlangConsole` → 0 errors, 454 nullable warnings (pre-existing
  generator output, unchanged from v7).

Any test that goes red after my v8 changes is mine.
