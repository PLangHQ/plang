# Builder — next session (2026-10-02)

You are the builder bot on `app-systems`. The previous session ended on context length after closing issues 1, 9, 21, 24, 25 (first pass), 26 (2), 28, 29 and 31.

## Read first

1. `.bot/app-systems/builder/summary.md`: your own state.
2. `.bot/app-systems/architect/builder-issues-2026-10-01.md`: every open issue with its evidence; the newest entries are at the end.
3. `.bot/app-systems/architect/os-goal-sweep-2026-10-02.md`: what is left in `os/` after the 24 deletions.
4. `Documentation/v0.2/conventions.md` "A Flag Is False By Default" (Ingi, 2026-10-02).

## Your lane

`os/system/**` (goals, `.llm` prompts, templates, the teaching under `os/system/modules/**`) and the module C# under `PLang/app/module/**`. Core (`goal/**`, `type/**`, `event`, `actor`) is the coder's, through the architect. Show the architect a shape before a C# edit. Every commit gets the architect's review and gate.

## Rules that bit last session

- **A count is real only with real builds:** a fresh folder, or `.build` deleted before each run, with `--build={"cache":false}`.
- **A notes or examples change runs Wire too.** The Compile prompt pins (`ThePromptCUserMessage`) and `pick_golden.json` read them. Re-pin once, and word-diff to show only your lines moved.
- **One test suite at a time, with `nice`** (11 GB machine).
- **The 9 Wire snapshot reds are baseline** (`.bot/app-systems/coder/baseline-failures.txt`), not yours.

## Queue, in order

1. **Issue 25, reopened (C4 4/6).** In `/shared/educator/work/lv-samples2/c4-1` and `c4-2` the read has only `Path`. The architect's guess: Prefill fills a chosen option only for an action that ends up certain (`goal/step/pick/list/this.cs` `Call`), and there `file.read` scored under Near. Check with the decider trace (`--debug={"llm":{"user":true,"response":true}}`) on those folders. If so, shape the fix (the chosen value reaches the writer for a listed but uncertain action too) and send it to the architect: the pick is core. The educator holds lesson C4 until this holds.
2. **Issue 32, two shapes.** (a) `call goal Page module=%!app.module.condition%` adds a junk `condition.compare` (a word inside a variable path pulls in its module). (b) `call goal Page module=%!app.module.file%` builds `Name=%!app.module.file%` (2 of 3). Check (b) against 2829786ff first: goal.call's note says "the path is the whole name … `/path/name`". The direction to measure: the decider sees a step's variables as opaque.
3. **Issue 2 with a key (1 of 6).** `foreach %person% as %value% with key %field%, call …` loses both Item and Key. It's the same lever as Conversation 26 (1): the decider offers the step's own variables (the Option question's v2). Shape it with the architect before building.
4. **Issue 30, pass 2.** Rebuild each remaining reopened `os/` goal on its own, cache off, without stopping at the first failure. Send one table: goal, built or not, the refusal, the class (writer mis-map / core / write in formal).
5. **`os/system/Build_dpricated.goal2`:** a stray non-`.goal` leftover; delete it.
6. **After the coder's stages 1–2 of `test/plan/task/`:** its stage 3 (goal.call's `Parallel` teaching for "in parallel", "don't wait", "in the background"; the task module's teaching, kept apart from `timer.sleep`).
