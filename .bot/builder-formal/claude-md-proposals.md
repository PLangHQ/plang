## architect — builder-formal — 2026-09-25
**Target:** /CLAUDE.md
**Why:** The "PLang Syntax (v0.1 builder limitations)" line teaches the v0.1 foreach form `call DoProduct item=%product%` (item=%name% names the element). The runtime2 builder reads `name=value` after a called goal as an argument of goal.call (prompt C: "`name=%item%` after the called goal is an argument of that goal.call"), and the builder's own goals use `call Menu step=%item%` (os/system/builder/BuildGoal/Start.goal:31). `Decide.goal:22-23` was written in the v0.1 form (`call AddStepToState item=%step%`), which passes an unset %step% under runtime2, and it produced a wrong golden plus a "silent miss" in eval round 8 that was really the source's ambiguity. Bots writing goals follow this line.
**Proposed change:** replace the foreach bullet with:
```
- foreach always calls a goal, does not support sub steps. Syntax: `foreach %products%, call DoProduct product=%item%` — each element is %item%; `name=value` after the goal name is an argument the called goal reads as %name%. Name the element another way with `as %product%` (`foreach %products% as %product%, call DoProduct product=%product%`). Never `item=%product%` (the v0.1 form).
```
