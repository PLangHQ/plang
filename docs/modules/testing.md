# Test Module

The `test` module is PLang's built-in test runner. It discovers `*.test.goal` files, runs each one in an isolated App, collects coverage, and writes a report — all from inside PLang itself. You activate it with `plang --test` and don't wire up anything else.

This page is the reference for running tests and reading the output. For *writing* `.test.goal` files — assertion idioms, error-handler variables, builder behavior — see [building_plang_tests.md](../../Documentation/v0.2/building_plang_tests.md).

## Overview

Running `plang --test` invokes `system/test.goal`, which is three lines:

```plang
Test
- set default %path% = '.'
- test.discover tests in %path% recursive, write to %tests%
- test.run tests %tests%, write to %results%
- test.report results %results%
```

That's the full lifecycle:

1. **Discover** walks the directory tree for `*.test.goal` files, loads their built `.pr` files, and decides which are `Ready`, `Stale`, or filtered-out by tag rules.
2. **Run** spins up a fresh App per test, runs the entry goal under a timeout, collects a `TestRun`, merges coverage back into the parent.
3. **Report** prints the console summary, writes a file artefact (`json` or `junit`), and emits the module and branch coverage tables.

The loop is C#, not PLang `foreach`. A single test's failure cannot silent-skip the rest of the suite — that was the whole reason the module exists.

## Writing a test file

A test file is a `.goal` file whose name ends in `.test.goal`. The file's first goal is the entry point. Assertions come from the [assert module](assert.md) and any error along the way is captured as a failure.

```plang
/ Tests/Examples/MathIsSane.test.goal
MathIsSane
- set %a% = 2
- set %b% = 3
- math.add %a% %b%, write to %sum%
- assert %sum% equals 5
```

Then build and run:

```
plang --build
plang --test
```

You should see:

```
Test summary: 1 total, 1 pass, 0 fail, 0 timeout, 0 stale, 0 skipped
  [Pass] Examples/MathIsSane.test.goal (3ms)
```

Supporting goals (fixtures, helpers) can live in the same file or sibling files; they don't need the `.test.goal` suffix unless you want them discovered as tests.

## Running tests

```
plang --test                              # Run all tests under the current directory
plang --test={"parallel":1}               # Serial execution (default is ProcessorCount)
plang --test={"timeout":5}                # 5-second per-test timeout (default 30)
plang --test={"include":["http","fast"]}  # Only tests tagged http OR fast
plang --test={"exclude":["network"]}      # Skip anything tagged network
plang --test={"format":"junit"}           # Write junit.xml instead of results.json
plang --test={"verbose":true}             # Stream per-test stdout live
```

The config dictionary can carry any combination:

| Key | Type | Default | Description |
|---|---|---|---|
| `timeout` / `timeoutSeconds` | int | `30` | Per-test wall-clock timeout (seconds). Must be positive. |
| `parallel` | int | `Environment.ProcessorCount` | Max concurrent tests. Must be positive. |
| `include` | string or list | `[]` (all) | Tag filter — only tests that match at least one tag run. Case-insensitive. |
| `exclude` | string or list | `[]` (none) | Tag filter — any test with a matching tag is skipped. Applied after `include`; **exclude wins on conflict**. |
| `verbose` | bool | `false` | When true, per-test `output.write` streams live. When false, captured output is rendered only in failure blocks. |
| `format` | `"json"` \| `"junit"` | `"json"` | Chosen report format. Console output is always written regardless. |

Invalid values (negative timeout, non-positive parallel, unrecognized format) fail the run immediately with an `InvalidTestConfig` error. Unknown keys are silently ignored, so the dictionary is forward-compatible.

## Tags and filtering

A test can carry two kinds of tags.

**User tags** come from `test.tag` actions inside the goal body:

```plang
HttpSmokeTest
- tag this test 'http', 'slow'
- http.request 'https://example.com', write to %resp%
- assert %resp.status% equals 200
```

**Auto tags** come from the `[RequiresCapability]` attribute on the C# handlers the test touches. `http.request`, for example, is declared with `[RequiresCapability("network")]`, so any test that calls it picks up the `network` tag automatically. Discovery walks static `goal.call` chains (depth 50, cycle-safe) so tags flow transitively — a test that calls a helper that calls `http.request` still ends up tagged `network`.

The union of user tags and auto tags is what `include` and `exclude` match against. A test with no tags passes any include filter only when no include is set.

## Isolation

Each test runs in a **fresh App**, rooted at the directory of the `.test.goal` file:

- Its own `MemoryStack` — variables set by one test are invisible to another.
- Its own `FileSystem` wrapper with its own root — relative paths are relative to the test's directory.
- Its own `Goals` collection loaded from the test's `.pr`.
- Its own `CurrentTest` slot so `test.tag` at runtime accumulates onto the right record.

The parent App's `SystemDirectory` is inherited so signing/settings/identity stay shared, but runtime state does not cross the boundary. A test's crash, assertion failure, or cancellation takes down that App only — the parent runner keeps going. Child Apps are disposed via `await using` before the next test starts.

The App boundary is the **file boundary** — not per-goal, not per-step. If a single `.test.goal` contains several goals, they share state within the run.

## Staleness

Each `.pr` file carries a SHA-256 hash of its source goal text. At discovery time, the runner re-parses the current `.goal` file and compares. If the hashes don't match — or if the `.pr` is missing or corrupt — the test is marked `Stale` and **not executed**. It's still reported, so CI sees the problem:

```
  [Stale] Examples/MathIsSane.test.goal (0ms)
```

The fix is always the same: `plang --build`. There is no silent fallback. An edited `.goal` without a matching rebuild is not a passing test, and it is not a failing test — it's a loudly-stale one.

## Timeouts

The per-test timeout is a linked `CancellationTokenSource`. When it fires:

- The child App's context cancellation stack gets cancelled, which propagates to downstream handlers that honor `Context.CancellationToken` — `timer.sleep`, `http.request`, etc. will throw `OperationCanceledException` mid-call.
- The run is recorded as `TestStatus.Timeout`.
- The parent runner is **not** cancelled; only the offending test dies.

The default budget is 30 seconds. Tune with `--test={"timeout":N}` run-wide, or pass `timeout` as a direct parameter on `test.run` inside a meta-test.

## Failure output

When an assertion fails, the runner captures the assertion details plus a **snapshot of all user-visible variables** at the moment of failure. The snapshot lives on `AssertionError.Variables` and flows into both the console block and the file artefact:

```
  [Fail] Examples/MathIsSane.test.goal (4ms)
    FAIL: Examples/MathIsSane.test.goal
      Expected: 6
      Actual:   5
      Variables:
        %a% = 2
        %b% = 3
        %sum% = 5
```

The snapshot excludes infrastructure variables (`!`-prefixed), always-fresh dynamic variables (`Now`, `GUID`), and settings-backed variables. Everything else is included by value.

**Sensitive values are masked.** Any property marked `[Sensitive]` on a serialized type renders as `******` in both `results.json` and `junit.xml` (via the diagnostic JSON path used by `AssertionError.FormatValue`). Non-string sensitive properties still keep their key visible — only the value is replaced. If a Sensitive-typed variable flows through an assertion, the masking applies to that too.

Ordinary variables are *not* automatically masked. A variable holding an API token, a bearer string, or a response body with PII will appear verbatim in a failed test's diagnostics. Treat the report artefact as sensitive when publishing it downstream, or mark the carrier types with `[Sensitive]`.

## Report artefacts

The report is written under `.test/` at the app root — this path is stable regardless of which subdirectory the suite was launched from.

### `.test/results.json` (default)

```json
{
  "summary": { "Pass": 2, "Fail": 1, "Timeout": 0, "Stale": 0, "Skipped": 0, "Ready": 0 },
  "builderVersion": "0.2.0",
  "runs": [
    {
      "path": "Examples/MathIsSane.test.goal",
      "entryGoal": "MathIsSane",
      "status": "Fail",
      "durationMs": 4.1,
      "goalHash": "a1b2…",
      "builderVersion": "0.2.0",
      "tags": ["math"],
      "error": "assert.equals failed",
      "expected": 6,
      "actual": 5,
      "variables": { "a": 2, "b": 3, "sum": 5 }
    }
  ],
  "branchCoverage": {
    "Examples/MathIsSane.test.goal:4": { "declared": ["if","else"], "observed": ["if"] }
  }
}
```

### `.test/junit.xml` (when `format: "junit"`)

Standard JUnit XML grouped by test directory. One `<testsuite>` per directory, one `<testcase>` per test, `<failure>` with the error message on fail, `<failure type="timeout">` on timeout, `<skipped>` for stale/skipped. Sensitive masking still applies because the message flows through the same diagnostic path.

## Coverage

### Module.action coverage

Every action handler fire is observed via an `AfterAction` event. The report renders the full universe of registered `(module, action)` pairs and marks which ones were hit:

```
Module.action coverage:
  [x] assert.equals
  [ ] assert.greaterThan
  [x] file.read
  [ ] http.download
  ...
  total: 24/87
```

Untested modules are visible at a glance. The coverage tracker is thread-safe (ConcurrentDictionary); child-App coverage is **merged additively and idempotently** into the parent after each test completes, so parallel execution is safe.

### Branch coverage (`condition.if`)

Every `condition.if` site in the suite's goal tree is reported. A "site" is identified by `goalPath:stepIndex` — the source path is part of the key, so a `Start` step in two different files never collides. For each site, the report shows the declared chain (e.g. `if / elseif[1] / else`) with ✅ or ❌ for each branch:

```
Branch coverage (condition.if):
  Tests/Examples/Login.test.goal:3: {✅ if, ❌ else}
  system/test.goal:5: {✅ true, ✅ false}

  Sites: 2 total (1 complete, 1 partial, 0 unreached)
  Branches: 3/4 covered (75%)

  Untested branches:
    Tests/Examples/Login.test.goal:3  else
```

`test.discover` seeds the declared chain for every `condition.if` in every discovered test's goal tree — including statically-reachable sub-goals — before anything runs. That means a site that exists in source but is never reached by any test still appears in the report as fully unreached, instead of silently not showing up. Dynamic `%var%` `goal.call` targets are skipped because they can't be resolved statically.

## Actions

### discover

Walks a directory for test files and decides which ones are `Ready`.

```plang
- test.discover tests in '.' recursive, write to %tests%
- test.discover tests in 'Tests/Foo' recursive=false, pattern='*.fixture.goal', write to %tests%
```

| Name | Type | Default | Description |
|---|---|---|---|
| `Path` | string | `.` | Directory to walk. Resolved under the app root; traversal outside is rejected and returns an empty list. |
| `Pattern` | string | `*.test.goal` | Filename glob. Override for fixture discovery. |
| `Recursive` | bool | `true` | Walk subdirectories. |

**Returns:** a `List<TestFile>`. Each entry carries `Path`, `Directory`, `PrPath`, `Goal`, `EntryGoalName`, `GoalHash`, `BuilderVersion`, `Tags`, `Status`, `StatusReason`. `Status` is `Ready`, `Stale`, or `Skipped` after include/exclude filtering.

### run

Runs the discovered tests in isolated child Apps, returns the run-wide `Results`.

```plang
- test.run tests %tests%, write to %results%
- test.run tests %tests%, parallel=1, timeout=5, write to %results%
```

| Name | Type | Default | Description |
|---|---|---|---|
| `Tests` | list of `TestFile` | required | Output of `test.discover`. |
| `Parallel` | int | `Testing.Parallel` (= `ProcessorCount`) | Concurrent test slot count. Bumped to 1 if less. |
| `Timeout` | int | `Testing.TimeoutSeconds` (= 30) | Per-test timeout in seconds. |

**Returns:** `Results` — an enumerable of `TestRun` objects (thread-safe; backed by `ConcurrentQueue`). Does not throw for child-test failures; failure is data.

### tag

Declares user tags for the running test. Read at discovery time to build the tag set, and accumulated at runtime on the in-flight `TestRun`.

```plang
- tag this test 'http'
- tag this test 'fast', 'slow'
```

| Name | Type | Default | Description |
|---|---|---|---|
| `Tags` | string or list | required | One or more tag strings. |

**Returns:** the current tag set as a list (empty outside test mode). Outside `--test` the action no-ops rather than erroring, so shared goals that tag themselves still work when reused in production.

### report

Writes the console summary + coverage tables + a file artefact.

```plang
- test.report results %results%
- test.report results %results%, format='junit', write to %report%
```

| Name | Type | Default | Description |
|---|---|---|---|
| `Results` | `Results` | `Testing.Results` on the current App | Run collection to render. Defaults so `test.report` with no args works inside `system/test.goal`. |
| `Format` | string | `Testing.Format` (default `json`) | `json` → `.test/results.json`; `junit` → `.test/junit.xml`. |

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

## TestStatus values

| Status | Meaning |
|---|---|
| `Ready` | Discovered, `.pr` fresh, passes tag filters — will run. |
| `Pass` | Ran to completion, no error. |
| `Fail` | An assertion or error produced a failing result. |
| `Timeout` | Per-test timeout cancelled the run. |
| `Stale` | `.pr` missing or hash mismatch against the current `.goal`. Not run. |
| `Skipped` | Filtered out by `include`/`exclude`. Not run, still reported. |

Non-`Ready` states are still reported — hiding them would hurt CI visibility.

## Known limitations

These are tracked as low-severity security findings on this branch and do not block merge, but are worth knowing when you publish test artefacts:

- **Terminal escape sequences in captured stdout.** The runner strips ANSI CSI sequences (`\x1B[…`) from captured output before rendering failure blocks, but does not strip OSC (`\x1B]…`), DCS, or charset designators. A test whose stdout echoes attacker-controlled content (e.g. an HTTP response body) could forge hyperlinks or manipulate the terminal title when the CI log is viewed.
- **C0 control bytes in JUnit output.** XML-escaping covers `<>&"'` but not raw control bytes (`\x00`–`\x08`, `\x0B`, `\x0C`, `\x0E`–`\x1F`). A test that asserts on binary data can produce a `junit.xml` that strict JUnit parsers reject — an availability issue for reporting, not a security breach.
- **Variable snapshots may carry user secrets.** As noted under [Failure output](#failure-output), only `[Sensitive]`-marked properties are masked. Ordinary user variables holding credentials, tokens, or PII will appear verbatim in a failing test's `results.json`. Treat the artefact accordingly.

## Examples

### Filter a large suite to fast tests

```
plang --test={"include":["fast"],"parallel":8,"timeout":10}
```

### Run without network-dependent tests

```
plang --test={"exclude":["network","llm"]}
```

### Emit JUnit for CI

```
plang --test={"format":"junit"}
```

### Meta-test the runner itself

```plang
/ Tests/TestModule/Discover/TestDiscoverFindsTestGoals.test.goal
TestDiscoverFindsTestGoals
- test.discover tests in '_fixtures' recursive, pattern='*.fixture.goal', write to %tests%
- list.count %tests%, write to %count%
- assert %count% equals 3
```

Meta-tests use `*.fixture.goal` as the discovery pattern to keep fixtures from being picked up by the outer `*.test.goal` pass.

## See also

- [assert](assert.md) — assertion actions used inside tests
- [mock](mock.md) — mocking actions in tests
- [error](error.md) — `on error` handlers in test goals
- [Building PLang tests](../../Documentation/v0.2/building_plang_tests.md) — authoring reference (syntax, error variables, builder behavior)
