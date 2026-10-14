# tester: app-systems

Traces to: decisions 671 (the test tree: what stays, what goes), 716 (`is file` saved as a truth check; ReadCsv passing by luck), 717 (the final baseline), 718 (the baseline checked on a clean checkout; tests leaving files) and 719 (test hygiene; a `.pr` edited by hand), in `.bot/app-systems/architect/summary.md`.

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

## In plang

The same brief as steps. The comments under it show what a finding looks like.

```plang
Review the tests on app-systems before it merges into its parent (decision 718; not runtime2, 734)
Start
- read .bot/app-systems/architect/briefs/common.md
- read .bot/app-systems/coder/summary.md; your version folder is v23 (the coder's latest)
- run `./dev.sh build`, then `./dev.sh test`, then `cd test && ../PlangConsole/bin/Debug/net10.0/plang --test`
- compare with .bot/app-systems/builder/baseline.md: 6 C# fails, 245/16/12/3, the same 16 names; any difference is your first finding
- check `git status` after the run: a test that leaves a file behind is a finding
- for each passing plang test, compare each step's words with the action its .pr saved (`test/**/.build/*.pr`, read only), starting with test/app/ and test/module/{condition,assert,list,variable}/
- check each plan's start.goal (test/plan/{app-systems,task,list-query,snapshot}/): every listed test exists, is built and runs; snapshot's are carried by design
- list modules and actions with no plang test, except those next-branch.md already lists
- check that each C# test added since runtime2 (`git log 0ea5a4b94..app-systems --diff-filter=A -- 'PLang.Tests/**/*.cs'`) goes red without its change, and pins behaviour, not an implementation detail
LIMITS: read only; no fixes, no .pr edits, no C#, no .goal edits
DONE WHEN: every finding names its test, what the step says against what the .pr or code does, and blocks or carry
REPLY: test-report.json at .bot/app-systems/, your notes in .bot/app-systems/tester/v23/, and a message in the Done/HASH/CHECK/OPEN shape
WHY: an LLM builder rebuilt the suite in two days (347 tests with 281 unbuilt → 276, every red owned); a test can pass for the wrong reason, and that is the risk now

/ what a wrong save looks like (found 2026-10-12, ReadCsv_LandsAsTable)
/ step:   - assert %report% is file
/ saved:  assert.isTrue(Value=%report%)
/         "file" dropped: it passes for any value that exists
/ right:  condition.if(Left=%report%, Operator="isnot", Right="file")
/
/ others to look for
/ - a dropped operand (Cover's own example):  - if %n% is 0   saved with no Right
/ - the channel dropped (goalchannelrecursion):  - write %message% to echo   saved as  output.write(Data=%message%)
/ - a type word compared as text (fixed 284e61c7a):  - if %cfg% is not a file   saved as  Operator="!=", Right="file"
/ - an on error that does nothing (refused since 7bdfbd105): an on.error with no Recovery, retry or Ignore
```
