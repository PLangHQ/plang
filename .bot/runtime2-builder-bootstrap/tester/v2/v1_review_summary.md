# v1 Review Summary — what coder responded with

## What v1 flagged (12 findings, verdict needs-fixes)

1. **CRITICAL** — BuildingGuardTests: 8 reds. Guard helper deleted by v2 builder squash but tests survived.
2. **MAJOR** — BuilderValidateValid PLang test: ~80 `Cannot convert int = 1 (String) to Int32` errors. @known annotation strings reach Convert.ChangeType.
3. **MAJOR** — Loop test cluster: returns string concat `0 + 1 + 1 + 1` instead of arithmetic `3`.
4. **MAJOR** — Signing cluster: 9 reds incl. `timeout.after.after` routing bug.
5. **MAJOR** — Locale-format asymmetry: parse side uses Invariant, format side uses CurrentCulture. Worse than no fix on it-IT.
6. **MAJOR** — promoteGroups.cs + enrichResponse.cs at 0% line coverage.
7. **MAJOR** — Gap 2 (file.read ResolveVariables): zero tests anywhere.
8. **MAJOR** — Gap 3 (single→list auto-wrap): zero tests, TypeMappingTests has six TryConvertTo tests, none target List<T>.
9. **MINOR** — ErrorHandleTests: retry tests only check Success.IsFalse. Deletion-test passes with retries=0.
10. **MINOR** — IfErrorOrchestration: no Error.StatusCode/Key pin on 404 path.
11. **MINOR** — ValidateResponseTests.NullInputs: shared `ValidationError` Key, no Message pin.
12. **MINOR** — Six call sites use Success.IsFalse without Error.Key follow-up.

## What coder did (5 commits, d8eb2958..bbf982d4)

| Commit | Addresses | Action |
|---|---|---|
| `4633674c` | F1 critical | Removed BuildingGuardTests — design call, guard intentionally not restored. Justification in commit body: other layers (Variables, file provider .pr-write guard, Actor setup, App shutdown) still enforce Building.IsEnabled; per-action guard added no value beyond those. |
| `63e88e6c` | naming | Renamed `App.Building` → `App.Build` (35 sites, 19 files). Pure rename. |
| `cc8e638d` | F5 (locale) | Format-side InvariantCulture passed at three sites: ExampleRenderer:103, FluidProvider:140, DefaultBuilderProvider FormatValue. |
| `2dad3023` | F9–F12 (weak C# assertions) | ErrorHandleTests retry-exhaustion: stateful lambda + callCount assert. IfErrorOrchestration: pinned 404 + Key=="NotFound". ValidateResponse: pinned message substring. ListTests/ForeachErrorPropagation: added StatusCode/Key+message. |
| `bbf982d4` | F6 (intentional 0%), F7 (Gap 2), F8 (Gap 3) | promoteGroups/enrichResponse marked build-time-only via XML doc. 3 ResolveVariables tests. 4 single→list auto-wrap tests. |

## What coder did NOT address

- **F2** (BuilderValidateValid `int = 1` cluster) — no commit touches the @known unwrap path
- **F3** (Loop string-concat) — no commit
- **F4** (Signing cluster, timeout.after.after routing) — no commit

These are PLang test-failure clusters from `/Tests/`. They are the bulk of the 25 reds + 4 stale. v2's job is to verify whether they're still red and decide whether the coder's reasoning (build-time only signal) covers them.
