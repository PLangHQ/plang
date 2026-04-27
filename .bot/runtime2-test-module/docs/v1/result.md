# Docs v1 — User-visible changes

No code changes in this session. User-visible documentation changes:

## New documentation

**`docs/modules/testing.md`** — full reference for the PLang test runner. Covers:
- Lifecycle (`discover` → `run` → `report`)
- `.test.goal` authoring (minimal example)
- `plang --test` CLI and config dict (`timeout`, `parallel`, `include`, `exclude`, `verbose`, `format`)
- Tags (user tags, `[RequiresCapability]` auto-tags, exclude-wins)
- Per-test App isolation (App boundary = file boundary)
- Staleness (hash check)
- Timeouts (linked CTS)
- Failure output and Variables snapshot
- Sensitive masking behavior and its limits
- `.test/results.json` and `.test/junit.xml` schemas
- Module.action and branch coverage tables
- Actions reference with parameters + observable Data.Properties
- `TestStatus` enum
- Known low-severity limitations

## Updated documentation

- `docs/modules/index.md` — added `test` row under Events & Testing; added `notContains` to the `assert` action list.
- `docs/modules/assert.md` — documented the `notContains` action (new on this branch).
- `Documentation/v0.2/building_plang_tests.md` — added lead paragraph cross-referencing `docs/modules/testing.md` for runtime concerns.
- `Documentation/v0.2/good_to_know.md` — appended "Test Module — Cross-Cutting Invariants" section covering nine non-obvious facts about the test runner.

## CHANGELOG

Repo has no `CHANGELOG.md`. If one is added later, the user-facing entry for this branch should be:

> **Added: PLang test module.** New `test.discover`/`test.run`/`test.tag`/`test.report` actions + `plang --test` runner with per-test App isolation, tag filtering (`include`/`exclude`, `[RequiresCapability]` auto-tags), timeouts, module.action and `condition.if` branch coverage, and JSON + JUnit report formats. See [docs/modules/testing.md](docs/modules/testing.md).
> **Added: `assert.notContains` action.** Symmetric in argument order, same container types as `contains`.
> **Improved: assertion failure diagnostics.** `AssertionError.Variables` snapshots user variables at the moment of failure; `[Sensitive]` properties mask as `******` in both console and report output.
