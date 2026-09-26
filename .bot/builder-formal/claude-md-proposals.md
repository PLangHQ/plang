## architect — builder-formal — 2026-09-25
**Target:** /CLAUDE.md
**Why:** The "PLang Syntax (v0.1 builder limitations)" line teaches the v0.1 foreach form `call DoProduct item=%product%` (item=%name% names the element). The runtime2 builder reads `name=value` after a called goal as an argument of goal.call (prompt C: "`name=%item%` after the called goal is an argument of that goal.call"), and the builder's own goals use `call Menu step=%item%` (os/system/builder/BuildGoal/Start.goal:31). `Decide.goal:22-23` was written in the v0.1 form (`call AddStepToState item=%step%`), which passes an unset %step% under runtime2, and it produced a wrong golden plus a "silent miss" in eval round 8 that was really the source's ambiguity. Bots writing goals follow this line.
**Proposed change:** replace the foreach bullet with:
```
- foreach always calls a goal, does not support sub steps. Syntax: `foreach %products%, call DoProduct product=%item%` — each element is %item%; `name=value` after the goal name is an argument the called goal reads as %name%. Name the element another way with `as %product%` (`foreach %products% as %product%, call DoProduct product=%product%`). Never `item=%product%` (the v0.1 form).
```

## architect — builder-formal — 2026-09-26
**Target:** /CLAUDE.md (Runtime2 Conventions)
**Why:** Reading a value turned any string holding `%x%` into a template when its row had no marker (type/this.cs:315), so an LLM answer, a cached response or a stored value holding `%x%` got rendered against live variables (a crash on the builder's cached answers, and a template-injection path). Ingi: "that is really bad and should be completely removed; the only way it should try to render a var is when template is set."
**Proposed change:**
```
- **Only a marked template renders.** A value renders `%var%` only when its type carries `template` (`"type": {"name": "text", "template": "plang"}` in a .pr row). Nothing infers it from content at read: a text from outside (LLM answer, cache, http, file content, a store read-back) that holds `%x%` stays plain text. The marker is born at build, when the formal reader types the programmer's literal, or by an explicit request (`file.read … ResolveVariables=true`). A build-time "does this hold a variable" check asks the marker (`data.HasVariableReference`), never `raw.Contains('%')`.
```
