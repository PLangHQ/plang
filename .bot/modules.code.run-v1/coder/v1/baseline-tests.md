# Baseline tests — code-run branch (off runtime2-cleanup @ 3c7c827d)

Date: 2026-05-09

## C# (`dotnet run --project PLang.Tests`)

- Total: 2752
- Passed: 2752
- Failed: 0

Pre-existing noise: stdout shows `File not found: .build/handleplang.pr`,
validation-failed messages from negative-path tests, and sensitive/fail fixture
goals that intentionally throw. None of them affect the green count.

## PLang (`cd Tests && plang --test`)

- Total: 199
- Passed: 199
- Failed: 0

Pre-existing noise: per-file `Test summary: 1 total, 0 pass, 1 fail` lines from
`_fixtures_fail/failsvar.fixture.goal` and `_fixtures_sensitive/sensitivefail.fixture.goal` —
these are negative-path fixtures driven by other tests; the aggregate summary is
clean. "Untested branches" trailer is informational, not a fail signal.

## Reference

If anything goes red after my changes that wasn't on this list, that's my
regression.
