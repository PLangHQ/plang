# codeanalyzer summary

**Version:** v4

**What this is:** A short re-check of the coder's fixes for my v3 section A (architect triage, decision 279): commits 46f7965b5..HEAD, 223 production files. Read only, not built or run. Report: `v4/report.md`.

**What was done:** Each A item rated against my v3 root fix. Fixed at the root: A1, A2, A8, A10, A12, A13, A14, A15. Open for one thing each: A3 (two `Set` tails), A4 (throwaway node), A6 (one 12-argument frame constructor with nulls, written three times), A7 (`actor.Equals(user)` is still reference equality). Partly: A9 (role words and five misleading names remain), A11 ("774 A" and 13 "until the … branch" comments). New smells: the Executor's mode test parses a Data's Name; A13's reflection by name with a tri-state `OwnInternal`.

**Next:** the coder closes the short list in the report, then a last check. B/C/D untouched.

**Code example:**
```csharp
// A7 today: renamed, not removed
if (await Actor() is not { } actor || actor.Equals(user)) return session;   // test/list/this.cs:43, no Equals override: still reference equality
// root: the actor answers IsUser
```
