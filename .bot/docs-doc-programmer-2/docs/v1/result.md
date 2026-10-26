# docs v1 — read of the programmer-2 pages (doc2, 7956325ed)

## Checked
- Scope: only `doc/` (testing-your-goals, settings, modules, start, calling-goals, verify) and `.bot/`; links resolve.
- Ran: the assert actions on the page match `os/system/modules/assert/` (equals, notEquals, contains, notContains, isTrue, isFalse, isNull, isNotNull, greaterThan, lessThan); the module table matches `PLang/app/module/` and the extra os folders (ai, db, install, plang); `plang --test <folder>` prints the summary and `[Pass]` lines as the page shows, and a failing test prints Expected / Actual / Variables; `--build.setting.cache=none` is `ChoiceInvalid` (valid: use, skip); a mode-word setting flag starts that mode (a `--build.setting.cache=skip` run started a build); test/module/setting 1 and test/app/setting 2 pass.

## Fixed (old docs, not doc/)
- `docs/modules/testing.md`: `.test/results.json` is an array of `{name, path, hash, status, duration, stdout, timings}`, plus `error` when a test failed; the page showed `{summary, builderVersion, runs, branchCoverage}`. Replaced with a real entry from a run.
- `Documentation/v0.2/cli-changes.md`: the duplicate-case example used `--build.setting.cache=none`, which is not a value; now `use`. Checked: `--Build.setting.cache=skip --build.setting.cache=use` is refused as one setting written twice in different case.

## Not re-run
- Rows needing a build with the decider key (the settings goals, the cold reader).
