# builder v3 — plan (2026-10-02)

Session driven by `.bot/app-systems/architect/builder-next-session.md`. Continuation of the
issue queue in `builder-issues-2026-10-01.md`. v2 handed the os/ build tail + reopened issues
to a fresh session; this is that session.

## Ground rules (bit last session)
- A count is real only with real builds: fresh folder or `.build` deleted, `--build={"cache":false}`.
- Clean rebuild of the binary before any claimed result (stale-binary trap).
- A notes/examples change runs Wire too — re-pin, word-diff.
- One test suite at a time, with `nice`.
- The 9 Wire snapshot reds are baseline, not mine.

## Queue (architect's order)

1. **Issue 25 reopened (C4 4/6).** Diagnose with the decider trace
   (`--debug={"llm":{"user":true,"response":true}}`) on `/shared/educator/work/lv-samples2/c4-1,2`.
   Architect's hypothesis: Prefill fills a chosen option only for an action that ends up *certain*
   (`goal/step/pick/list/this.cs` `Call`); there `file.read` scored under Near so `Template=plang`
   never reached the writer. If confirmed → shape the fix (chosen value reaches the writer for a
   listed-but-uncertain action too) and send to architect (the pick is core). Educator holds C4.

2. **Issue 32, two shapes.**
   - (a) `call goal Page module=%!app.module.condition%` adds junk `condition.compare` — a word
     inside a variable path pulls in its module.
   - (b) `call goal Page module=%!app.module.file%` builds `Name=%!app.module.file%` (2 of 3).
     Check (b) against 2829786ff first (goal.call's note "the path is the whole name … `/path/name`").
   Direction to measure: the decider sees a step's variables as opaque.

3. **Issue 2 with a key (1 of 6).** `foreach %person% as %value% with key %field%, call …` loses
   both Item and Key. Same lever as Conversation 26(1): the decider offers the step's own variables
   (Option question v2). Shape with architect before building. `pick/list` `Code` reads `as %x%` but
   not `with key %k%`.

4. **Issue 30, pass 2.** Rebuild each remaining reopened `os/` goal individually, cache off, don't
   stop at first failure. One table: goal, built/not, refusal, class (writer mis-map / core / write
   in formal).

5. **`os/system/Build_dpricated.goal2`:** stray leftover — DELETE. ✅ done (git rm).

6. **After coder's stages 1–2 of `test/plan/task/`:** its stage 3 (goal.call's `Parallel` teaching
   for "in parallel"/"don't wait"/"in the background"; the task module's teaching, kept apart from
   `timer.sleep`). Gated on the coder.

## Approach
Items 1–3 are mostly diagnosis + shape-to-architect (pick/decider are core). Item 4 is a mechanical
rebuild table. Item 5 done. Item 6 gated. Start with diagnosis (1, 32a/b, 2), hand shapes to
architect, do item 4 table, then any writer-mis-map rows that fall to me.
