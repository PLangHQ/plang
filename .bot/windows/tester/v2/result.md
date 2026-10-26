# windows — the tester's re-check (tip 2d41eae74; v1 FAIL at 001a8e434)

**Verdict: PASS.** The one v1 finding (EachGoalIsTheAppsOwn green without its change) is closed.

- The rewritten test (c435377fc; its .pr rebuilt by the builder, 2d41eae74): `before each goal, call Log`, `call goal Mine`,
  `call goal /system/shortcut/Goal`, `assert %last% equals "Mine"`. The .pr's steps match their text (on.start bind, goal.call ×2, assert).
- Mutation (announced, reverted, tree clean): `goal/this.cs` Level back to `0 => context.App.goal.type, 1 => this`, PlangConsole rebuilt (0 errors):
  `plang --test plan/windows/app/event` → 0 pass, 1 fail, `Expected: "Mine"`. With the rule in place it passes.
- `./dev.sh test --force`: Modules 1,432 / Types 1,008 / Data 880 / Generator 191 / Runtime 1,401 green; Wire 612 with the baseline's six reds, no new name.
- `plang --test` llm excluded: 309 total, 298 pass, 1 fail (`BareLiteralJudgedByForm`), 3 stale, 7 skipped. llm saved 6/6. `git status` clean.
