# Docs v1 — PLang test module user documentation

## What this is

Branch `runtime2-test-module` ships the `test` module — a PLang-native test runner with discovery, tag filtering, per-test App isolation, timeouts, module + branch coverage, and two report formats. By auditor v2 it's code-complete and ready to merge.

The gap this version fills is user-facing documentation. Before this session, a PLang developer could not learn how to run their tests without reading the C# handlers. There was no `docs/modules/testing.md`, no entry in the module index, no cross-reference from the authoring doc, and no good_to_know entry for the test module's cross-cutting invariants.

Scope: **documentation only**. No code changes. XML docs on the new C# handlers and Test.* types were already thorough — confirmed by auditing `discover.cs`, `run.cs`, `tag.cs`, `report.cs`, `Test/this.cs`, `TestFile`, `TestRun`, `TestStatus`, `Coverage`, and `RequiresCapabilityAttribute`. No XML-doc gaps to fill.

## What was done

### Created

- `docs/modules/testing.md` — **the main deliverable**. Full user-facing reference: lifecycle overview, `.test.goal` authoring example, `plang --test` CLI, config dictionary (`timeout`, `parallel`, `include`, `exclude`, `verbose`, `format`) with defaults, tags (user tags + auto tags via `[RequiresCapability]`, exclude-wins semantics), per-test App isolation (App boundary = file boundary), staleness (`goal.Hash` vs `.pr.Hash`), timeouts (linked CTS propagation), failure output with `AssertionError.Variables` snapshot, `[Sensitive]` masking behavior and limits, `.test/results.json` and `.test/junit.xml` schemas, module and branch coverage tables, full `discover`/`run`/`tag`/`report` actions reference with parameters and observable Data.Properties, `TestStatus` values, and the three low-severity security limitations from security-report.json.

### Modified

- `docs/modules/index.md` — added `test` row under "Events & Testing". Also added `notContains` to the `assert` row's action list (the action exists on this branch but wasn't listed).
- `docs/modules/assert.md` — documented the `notContains` action (new on this branch per `PLang/App/modules/assert/notContains.cs`; noted symmetric argument semantics per tester v6 note on L2 resolution).
- `Documentation/v0.2/building_plang_tests.md` — added one lead paragraph cross-referencing the new user doc, keeping this file focused on authoring concerns.
- `Documentation/v0.2/good_to_know.md` — appended "Test Module — Cross-Cutting Invariants" covering nine non-obvious facts (App boundary, Coverage merge semantics, site key format, `test.discover` chain seeding, `[RequiresCapability]` attribute constraints, hash-based staleness, `ChildAppCreated` test-only hook, `test.tag` production no-op, `Variables.Snapshot` vs `[Sensitive]` filter).

Every claim in `testing.md` and `good_to_know.md` is traceable to the source: defaults from `Test/this.cs`, discover/run/report flow from the handler source, site key format from `run.cs:99`, staleness algorithm from `discover.cs:135-140`, security limitations from the resolved findings in `security-report.json`.

## Code example

One illustrative fragment. `testing.md` exposes the observable Data.Properties on `test.report` that meta-tests use:

```markdown
**Returns:** a `Data` result with the following observable properties:

| Property | Type | Description |
|---|---|---|
| `format` | string | The format used (`json` or `junit`). |
| `reportPath` | string | Absolute path of the written file. |
| `content` | string | Full content of the written file. |
| `summaryTotal` | int | Count of `TestRun` entries. |
| `summaryPass` | int | Count at `TestStatus.Pass`. |
| `summaryFail` | int | Count at `TestStatus.Fail`. |
| `variableSnapshotCount` | int | Number of failed runs that carry an `AssertionError.Variables` snapshot. |

These are how meta-tests (tests of the runner itself) validate the runner without a filesystem round-trip.
```

The parameters and descriptions mirror `report.cs:76-83` exactly. The "why meta-tests use this" framing is in the doc because the reason these scalars exist at all (vs. parsing content strings) is the builder-LLM Value/Container argument order issue that L2 in test-report.json captures — useful history that isn't in the code.

## Verification

- Re-read `testing.md` cold. A developer who has never seen the branch can (a) run `plang --test`, (b) write a `.test.goal`, (c) read the console summary, (d) know where `.test/results.json` lives, (e) understand why a test is `Stale`, (f) configure tag filters, (g) understand failure blocks and variable snapshots, (h) find the coverage tables. ✅
- Every action parameter table cross-checked against the C# source: `discover` (Path, Pattern, Recursive), `run` (Tests, Parallel, Timeout), `tag` (Tags), `report` (Results, Format). ✅
- Every behavior claim traceable: exclude-wins (`discover.cs:167-176`), App boundary (`run.cs:75`), variable snapshot exclusions (`Variables/this.cs:593`), chain seeding (`discover.cs:SeedBranchChains`), `ChildAppCreated` event (`run.cs:29`). ✅
- `docs/modules/index.md` link to `testing.md` lands on the new file. ✅

## Outcome

**Verdict: pass.** All documentation gaps are filled. The code is ready to merge.

No findings flagged for coder or tester — all writing was within docs-bot scope.
