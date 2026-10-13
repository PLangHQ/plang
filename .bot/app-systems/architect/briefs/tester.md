Review the tests on app-systems before the merge to runtime2 (decision 718)
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
