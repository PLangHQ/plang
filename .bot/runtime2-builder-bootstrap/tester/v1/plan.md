# Tester v1 — Plan

## Branch context

`runtime2-builder-bootstrap` — what the coder report described as a "small 3-gap fix" (variable.set AsDefault, file.read ResolveVariables, TypeMapping list auto-wrap) actually contains the full v2 self-hosting builder pipeline plus diagnostics, type system, and validation gates. ~2300 files changed, ~30k insertions.

Codeanalyzer has gone four rounds. Latest verdict (v4, commit 65555d3e): CLEAN, but with two carryovers escalated to me:

1. **Locale fix asymmetry (v4 escalation).** `TypeConverter.cs:325` parses with InvariantCulture. The three FORMAT sites — `DefaultBuilderProvider.FormatValue`, `FluidProvider.FormatFormalValue`, `ExampleRenderer` — still use Thread.CurrentCulture. On European locales (`,`-decimal), the @known round-trip can produce `"3,14"` formals the parser can't read back. **No test exercises this with non-Invariant culture.**
2. **`promoteGroups` is unreachable from any goal (v4 sub-finding).** Module is registered (LLM-routable in theory) but no current goal calls it. The new `ActionError("PromoteGroupsImmutableStep")` code path therefore has zero coverage.

The coder report (`.bot/runtime2-builder-bootstrap/coder/v1/report.md`) is just a pre-implementation handover; it doesn't describe what actually shipped. The real surface is the codeanalyzer chain.

## What I'm going to do

### 1. Run the test suite (C# + PLang)
- `dotnet build` for `PlangConsole` and `PLang.Tests` first.
- `dotnet run --project PLang.Tests` for C#.
- `plang p build && plang p --test` from project root for PLang tests, if a PLang test root is set up. Need to confirm.
- Record raw counts (total / passed / failed / skipped).

### 2. Run coverage on the C# tests
- Coverlet via `dotnet run --project PLang.Tests -- --coverage` (TUnit), output cobertura.xml + coverage.json into `v1/`.
- Spot-check coverage on the actual changed files, not the global %. The interesting question is: what fraction of the ~2300 changed lines is exercised?

### 3. Hunt false greens on the highest-risk surfaces
The codeanalyzer chain already confirmed the OBP / catch / clone problems are fixed at the code level. My job is different: are the **tests honest**?

Highest-risk areas given the v4 carryovers and the change scale:

- **`PLang.Tests/App/Catalog/CatalogTests.cs`** — Catalog is brand new. Are the tests verifying the renderer output (intent) or just that `Catalog.Get(...)` returns non-null (implementation)?
- **`PLang.Tests/App/Modules/builder/ValidateResponseTests.cs`** — does it assert error keys (`Error.Key`, `StatusCode`) or just `result.Success == false`?
- **`PLang.Tests/App/Modules/builder/SaveGoalsTests.cs`** — ties to TypeMapping single→list auto-wrap (Gap 3 from the coder report). Does it actually pass a single Goal to a `List<Goal>` parameter?
- **`PLang.Tests/App/Modules/variable/settests.cs`** — Gap 1 (AsDefault). Does it assert AsDefault=true preserves an existing initialised variable AND does it test AsDefault=true when variable is unset/uninitialised AND default-overwrite path?
- **`PLang.Tests/App/Modules/builder/ComplexTypeDiscoveryTests.cs`, `GetTypeInfoTests.cs`** — TypeConverter / PlangType new infrastructure.
- **`PLang.Tests/App/Memory/DataResolutionTests.cs`** — JSON numeric boxing patterns (the pattern CLAUDE.md flags as recurring).
- **`PLang.Tests/App/Modules/modifier/ErrorHandleTests.cs`** — error.handle.Wrap RetryFirst recovery flow (codeanalyzer v2 noted this as a behavioral change needing coverage).

### 4. Carryover-specific checks
- **Locale carryover.** Grep for any test setting `Thread.CurrentCulture` to a non-invariant culture, or asserting numeric round-trip via `FormatValue`/`FormatFormalValue`/`ExampleRenderer`. If none → finding (missing-coverage).
- **promoteGroups carryover.** Grep for any test invoking `promoteGroups` action or its `PromoteGroupsImmutableStep` error. If none → finding (missing-coverage).
- **`Step.Clone()` deletion deferred.** Codeanalyzer v3 said the method has zero production callers and 7 of 18 properties missing. If still present, look for a test that uses it — that test is testing scaffolding, not behaviour.

### 5. Apply the deletion test
For the highest-impact production code paths (DefaultBuilderProvider.NormalizeParameterTypes, validateResponse, error.handle.Wrap), pick a non-trivial line and ask: "If I deleted this, would any test fail?" If no, that's a finding.

### 6. Write outputs
- `v1/coverage.json` — coverage data, raw.
- `v1/result.md` — per-finding analysis, structured.
- `v1/verdict.json` — pass/fail.
- `v1/summary.md` — narrative.
- `.bot/runtime2-builder-bootstrap/test-report.json` — branch-shared test report (the schema in the character file).
- Update `.bot/runtime2-builder-bootstrap/tester/summary.md`.

## What I'm explicitly NOT going to do

- Re-litigate codeanalyzer findings. v4 is CLEAN and the carryovers are mine. I'm not re-reviewing OBP, bare-catches, clone-family — that's been done four times.
- Edit production code or .pr files. My job is test quality, not implementation.
- Write new tests for missing coverage. I can flag them; the coder writes them.
- Read every one of 28 changed test files line-by-line. I'll triage by risk and apply patterns from prior tester sessions in memory (`/memory/feedback_*.md`).

## Risks

- **Branch is huge.** Easy to miss a false-green in a file I don't open. Counter: pattern-grep for weak-assertion shapes (`Assert.IsFalse(result.Success)` without `.Error.Key`, `Assert.IsNotNull` without value check) across all 28 changed test files in one sweep.
- **Build failures.** If the project doesn't build cleanly after the squash commit, that's not a tester finding — that's a coder finding.
- **PLang tests may not exist for the new modules.** Per CLAUDE.md, every new module needs a PLang `.goal` test alongside the C# test. The v2 builder modules (validate, validateResponse, enrichResponse, promoteGroups, types, app, goals, etc.) are all new.
