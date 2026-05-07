# v9 Baseline (clean rebuild)

State: `f2790967` (security v1 + auditor v1 reports landed).

## C# (`dotnet run --project PLang.Tests`)

- Total: 2762
- Passed: 2762
- Failed: 0

## PLang (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`)

- Pass: 205
- Fail: 6 — all under `_fixtures_fail/` and `_fixtures_sensitive/` (deliberate fixture inputs; matches tester v7 / coder v8 / security v1 baseline)

## Build

- `dotnet build PlangConsole`: 0 errors, 454 warnings.

Any test that goes red on top of this baseline that isn't in the deliberate-fixture list is my regression.
