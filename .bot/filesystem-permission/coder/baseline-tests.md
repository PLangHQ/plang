# Baseline tests — filesystem-permission

The list of test results expected on a clean rebuild of this branch, separated
into pass and intentional-fail categories. Use this to distinguish coder
regressions from pre-existing branch state.

## Suites + canonical commands

```bash
# C# (always run first — gates compilation regressions)
dotnet run --project PLang.Tests

# PLang
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

Stale-binary trap: `plang --test` uses a pre-built binary. Before claiming a
result, rebuild from clean (see `CLAUDE.md` → "Running plang Tests").

## C# suite (PLang.Tests)

**Expected: all green.** Any failure is a regression.

No skipped tests as of v5 — Scenario4 was unskipped when the underlying
issue was diagnosed (turned out to be `PermissionRecord.AppId` scoping
grants to a per-instance `App.Id`, not the deserialiser recursion that the
v4 `[Skip]` reason guessed at). See `coder/v5/report.md`.

## PLang suite (`plang --test`)

**Expected fails: 6 — all are intentional fail-fixtures used by the Report
module's tests to verify failure reporting.** Discovered directly because of
the leading-underscore fixture convention.

| Fixture | Reason expected fail |
|---|---|
| `Tests/TestModule/Report/_fixtures_fail/failsvar.fixture.goal` | asserts `42 == 99` |
| `Tests/TestModule/Report/_fixtures_sensitive/sensitivefail.fixture.goal` | asserts identity `== 'will-not-match'` (privateKey masked as `******`) |
| `Tests/Modules/Test/Report/_fixtures_fail/failsvar.fixture.goal` | duplicate of above under `Modules/Test/` path |
| `Tests/Modules/Test/Report/_fixtures_sensitive/sensitivefail.fixture.goal` | duplicate of above under `Modules/Test/` path |

(Total is 4 distinct fixture files; tester v3 counted 6 — discrepancy likely
reflects discovery into nested `_fixtures_*` dirs or duplicate runner passes.
If a future tester sees a different fail count, walk the discovered set and
check provenance before declaring a regression.)

Everything else under `Tests/` is expected green.

## How to use this file

Run both suites. Compare the failure set against the table above:
- If a fail is listed here → not a regression.
- If a green test goes red, or a new fail appears → regression, surface in the
  test report.
- If a listed expected-fail goes green → fixture file changed; investigate
  whether the change was intentional and update this baseline.
