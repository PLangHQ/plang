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

## architect — v1 — 2026-10-25 (3)
**Target:** /CLAUDE.md
**Why:** fast-apps made apps share their maker's types, actions and goals (decisions 36–41). Two leaks followed from one cause: a binding left on a shared entry fired in every app (26), and a read wrote its source into a shared type's history, growing it forever (29). The rule that prevents both, "a run never writes an object its app shares", is written only in the plan and the log (auditor N19).
**Proposed change:** add under "OBP Shape Smells", in the branch rules:
```markdown
- **A run never writes an object its app shares.** A sub app's types, actions and goals are layers over what it's made from, so a shared entry is every app's: what a run adds goes to its own layer or its own context, never onto the shared object. A binding made while a program runs is held on the context of the actor that made it (`actor.on`); a type takes no history. The only writes onto a shared object are caches that give every app the same answer (a kinded type made once, a kind coined once). (fast-apps 26, 29.)
```

## architect — v1 — 2026-10-25 (2)
**Target:** /CLAUDE.md
**Why:** Ingi (fast-apps 67): "the code analyzer should also record the speed of running plang apps, memory not growing, and time running tests, compile c#, keeps within what we are doing so we can quickly catch if it suddenly changes". fast-apps built the speed (a sub app born in 0.5 ms, `plang --test` ~5 s, memory flat), and nothing yet keeps a later branch from losing it unseen.
**Proposed change:** add a section after "Running plang Tests":
```markdown
## Speed record
- `Tools/Bench/run.sh` measures, gated on the machine's load: a sub app's and a root app's birth (time, bytes), a request app (time, bytes) with its held memory after 3,000 requests and a rest, `plang --test`'s time, the C# sweep's time, and the C# build (clean and incremental). Each run appends a row to `Tools/Bench/record.md` (date, branch, commit, load, each number); each number's noise band is at the record's top.
- Codeanalyzer runs it at every review and compares with the last row recorded on the branch's parent. A number outside its band is a finding: blocking unless the branch's decisions explain it. Held memory that grows with the request count always blocks.
```
