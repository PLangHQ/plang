# Security review plan — runtime2-test-module (v1)

## Scope
Full branch `runtime2-test-module` vs `runtime2`. ~70 C# files touched, ~5200 LOC added. Test module introduces:
- `test.discover` / `test.run` / `test.report` / `test.tag` handlers
- Per-test child App isolation + timeout + parallel semaphore
- Coverage tracking (module.action + condition.if branches, thread-safe)
- AssertionError variable snapshots
- AfterAction event payload (action + result)
- Executor `--test={...}` config apply
- Minor: `condition.if/elseif/else` orchestration, `assert.notContains`, `Path.GetRuntimeDirectory`, parser `IsTest` flag

## Threat model applied
Test mode is **opt-in local/CI tooling** — activated only by `--test`. The trust boundary remains the signed .pr; unsigned .pr from the user's own tree is trusted by design. Attack surface is narrow: test output rendering (XML/ANSI/JSON), path handling in discover, isolation between parallel tests, variable snapshot in reports.

## Audit checklist
1. `test.discover` — path traversal / symlink egress / recursion depth / cycle handling
2. `test.run` — child-App isolation, concurrent Results.Add, Coverage.Merge race, timeout cancellation propagation, event binding lifecycle
3. `test.report` — JUnit XML escaping (SecurityElement.Escape coverage), JSON serialization, ANSI strip completeness, variable snapshot info disclosure
4. `AssertionError.Variables` snapshot — what's included, leak surface
5. `condition.if` orchestration — re-entry guard, branch index publish
6. `Executor.Configure` — `--test` parameter parsing, error routing
7. `Path.GetRuntimeDirectory` — sandbox respected for LoadedFromPrPath
8. Recurring bug classes — Clone/Copy, depth guards, catch-narrowing, JSON numeric boxing

## Expected outcome
Test module is local dev/CI tooling with narrow attack surface. Expecting low/informational findings — verdict likely **pass**.
