# tester: app-systems

Read `common.md` beside this file first. Your version folder is `v23` (the coder's latest is `.bot/app-systems/coder/v23/`); the coder's state is in `.bot/app-systems/coder/summary.md`.

## Why

The plang suite was rebuilt in the last two days (347 tests with 281 unbuilt → 276, every red owned). A test that **passes for the wrong reason** is the risk now: the LLM builder can save a `.pr` that drops what the step says, and the test still goes green. One was found yesterday: `assert %report% is file` was saved as `assert.isTrue(Value=%report%)` (the "file" dropped), so it passed by luck. There are probably more.

## What to do

1. **Confirm the baseline.** `./dev.sh build`, `./dev.sh test`, then `cd test && ../PlangConsole/bin/Debug/net10.0/plang --test`. Expect C# 6 carried, plang 245/16/12/3 with the 16 names in `.bot/app-systems/builder/baseline.md`. Any difference is your first finding. Check `git status` after the run: a test that leaves files in the tree is a finding.
2. **Read passing tests against their `.pr`.** For each passing plang test, compare each step's text with the action and values its `.pr` saved (`test/**/.build/*.pr`, read only). Flag a step whose saved action doesn't check what the words say: a dropped operand, a type word turned into a truth check, a literal changed, an `on error` that does nothing, an assert comparing against the wrong thing. Start with `test/app/` and `test/module/condition/`, `assert`, `list`, `variable`.
3. **Every plan's tests run.** `test/plan/{app-systems,task,list-query,snapshot}/start.goal` index each plan's behaviours; check each indexed test exists, is built, and runs (snapshot's are carried by design).
4. **Missing coverage.** Next-branch.md lists the known gaps (crypto encrypt/decrypt, environment.start, module add/remove, on.cancel/unbind/input, identity list/unarchive, code list/remove/setDefault, the os modules); don't re-list those. Report other modules or actions with no plang test.
5. **C# tests that pin the right thing.** Sample the C# tests added on this branch (`git log 0ea5a4b94..app-systems --diff-filter=A -- 'PLang.Tests/**/*.cs'`): does each go red without its change (the branch's rule), or does it pin an implementation detail?

## Output

`test-report.json` at `.bot/app-systems/`, and your notes in `.bot/app-systems/tester/v23/`. Per finding: the test, what its `.pr` (or code) does versus what the step says, **blocks the merge / carry**.
