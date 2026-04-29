# tester v2 — verify coder's response to v1

## Approach

Three-pass: closures → false-green hunt on new tests → fresh PLang reds. Stop and report rather than try to be exhaustive — v1 was thorough, v2 is targeted.

## Pass 1: verify each closure (10 findings touched)

For each closure, check that the new test would catch the regression it claims to.

| F# | Verification |
|---|---|
| F1 | Tests deleted. Confirm BuildingGuardTests.cs is gone, justification in commit holds (grep for surviving guard layers: Variables / file provider / Actor / App shutdown). |
| F5 | Format-side fix at three sites. Run `dotnet build` first, then read each touched line. Ideally a unit test sets Thread.CurrentCulture=it-IT and asserts the format output is `"3.14"` not `"3,14"`. If no such test exists, this is still a missing-coverage minor (but the fix itself is correct). |
| F6 | XML doc claims build-time only. Verify by grep: nothing in `--test` paths actually runs these. If any `--test` path does invoke them, the claim is wrong. |
| F7 | 3 new tests in FileHandlerTests. Apply deletion test: if I delete the `if (ResolveVariables...)` branch in `read.cs`, do the tests fail? Also check the skipInfrastructure test — does it actually exercise the protection or pass for the wrong reason? |
| F8 | 4 new tests in TypeMappingTests. Apply deletion test on `TypeConverter.cs:156-168` auto-wrap branch. Check `ListOfStringToListOfString_PassesThrough` is not a tautology (the upstream list-conversion path could be passing it through without ever hitting the wrap branch). |
| F9 | ErrorHandleTests retry — verify stateful lambda actually captures retry count, not initial count + 0. Read the production retry path to confirm Wrap returns 1 + retryCount calls. |
| F10 | If 404 + NotFound key — confirm goal.call's NotFound StatusCode is 404 (not 410, not 0). |
| F11 | ValidateResponse Null — confirm both message strings come from null-input branch and not from another ValidationError variant (step-count, gap, Keep-without-prior). |
| F12 | List/Foreach — confirm Error.StatusCode==404 / Key=="ValidationError" pins are real (the actual production path produces these, not just the test setup). |

## Pass 2: re-run the suite, surface fresh reds

- Run C# tests; should now be 2289/2289 (was 2281/2289). If anything else turns red after the rename, that's a new finding.
- Run `/Tests/` PLang suite; was 132/161 + 4 stale. F2/F3/F4 are likely still red. Categorize: which are real production bugs vs build-time-only-by-design vs stale fixtures.
- Run `/tests/` PLang suite; was 8/9.

## Pass 3: coverage spot-check

Re-run coverage on the four files the coder touched:
- `PLang/App/modules/file/read.cs` (was 62.5%)
- `PLang/App/Utils/TypeConverter.cs` (was 50.4%)
- `PLang/App/modules/builder/promoteGroups.cs` (was 0% — should still be 0%, intentional)
- `PLang/App/modules/builder/enrichResponse.cs` (was 0% — same)
- `PLang/App/Catalog/ExampleRenderer.cs`, `FluidProvider.cs`, `DefaultBuilderProvider.cs` FormatValue — locale fix coverage

## Out of scope

- F2 / F3 / F4 PLang test clusters: I'll re-run, categorize, and surface — but if coder explicitly leaves them as "build-time only" or pre-existing breakage, I won't insist they fix them in this round. I'll record in the report.
- New code on the rename branch (App.Build) — pure rename, no behavior change. I'll spot-check.

## Outputs

- `.bot/runtime2-builder-bootstrap/tester/v2/coverage.json` (spot-check, not full re-run)
- `.bot/runtime2-builder-bootstrap/tester/v2/result.md`
- `.bot/runtime2-builder-bootstrap/tester/v2/summary.md`
- `.bot/runtime2-builder-bootstrap/tester/v2/verdict.json`
- Update `.bot/runtime2-builder-bootstrap/test-report.json`
- Update `.bot/runtime2-builder-bootstrap/tester/summary.md` with v2 line
