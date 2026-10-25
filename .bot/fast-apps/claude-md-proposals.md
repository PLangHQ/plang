# fast-apps — CLAUDE.md proposals

## architect — v1 — 2026-10-25
**Target:** /CLAUDE.md
**Why:** Ingi set two standing rules on fast-apps that apply to every branch and every bot. The boy scout rule (decision 66) came after he caught `EnsureTable`, an older verb+noun setup step in a file the branch reworked, which codeanalyzer's review skipped because its scope was the diff's lines. Code only tests call (decision 35) came when the hold left public overloads only tests used: "we don't have code in runtime for tests". Neither is in CLAUDE.md, so a coder or a reviewer on the next branch won't know them.
**Proposed change:** add under "OBP Shape Smells", after the design alarms:
```markdown
**Branch rules:**
- **The boy scout rule.** A file a branch touches is left better: its older smells are fixed in the branch, not deferred as "pre-existing". A reviewer's scope is every file the branch touched, read whole, not only the diff's lines; it marks an older finding with the commit it came from. A fix is scaled to its smell (a rename where a rename does it). Only a file the branch never touches leaves its smells to another branch. (Ingi, fast-apps 66: "improve when we chance to improve … with incremental improvements come great gains".)
- **No runtime code for tests.** A production member that only tests call is removed with the tests that exist for it; a test of real behaviour reaches it through the door production uses. (Ingi, fast-apps 35.)
```
