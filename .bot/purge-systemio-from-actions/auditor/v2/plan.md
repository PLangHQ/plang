# auditor v2 — plan

**Branch:** `purge-systemio-from-actions`
**Diff since v1:** 4 coder commits + 1 codeanalyzer pass.

- `bfb34bca4` coder: address auditor v1 — App.Parent chain in IsInRoot
- `f2f870feb` codeanalyzer v2: PASS, 5 LOW
- `ecdd0de4f` coder: address codeanalyzer v2 N1+N2 (cycle cap + filtered catch)
- `ef47e2d5a` coder: scrub review-bot references from source comments

## What I'm checking

1. **F1 (the regression I flagged) — actually closed?** Clean rebuild, full-suite
   `plang --test`. The bar is 206/206 same as runtime2.
2. **The App.Parent fix — does it reopen F1?** New parent-chain walk in
   `IsInRoot` widens the set of paths that auto-grant. Audit: can a child
   app inherit a *strictly broader* root from a parent it shouldn't be
   reading? Is the cycle cap honest? Could an action handler reach
   `ctx.App.Parent = victimApp`?
3. **N2 filtered catch in `Canonicalize` — list complete?** Does it cover
   what `Path.GetFullPath` actually throws, and let other exceptions escape?
4. **PLNG002 carve-out count unchanged?** Still exactly two file-scope
   exemptions, both inside `Plng002.cs`. Zero suppressions elsewhere.

## Verdict gate

PASS if (a) tests reproduce 206/206 on clean rebuild, (b) App.Parent doesn't
reopen F1 in any plausible reach, (c) PLNG002 still has exactly 2 carve-outs.
