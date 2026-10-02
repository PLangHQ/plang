# Friction: the builder bot's review of plang and its builder

A user review as much as a bug list (Ingi: we eat our own dog food). Where the builder
misunderstood what I wrote, where an error didn't say what to do next, what I worked
around, what I wished a step could say, and what felt good and should stay. Each entry:
**what** · **Cost** (evidence) · **If I had** (the wish). The architect gathers these in
`.bot/app-systems/architect/friction.md`.

## Building and the builder's LLM

- **builder · a correct teaching note loses to a stage-1 common-action certainty.** For
  `call ShowFiles files=%files% in %browser.desktop%`, the decider's own state prompt
  carried the exact counter-example (`… → window.callGoal, not goal.call: in %window% puts
  the goal in the window's page`), yet goal.call mapped **0/5** (saved wrong, or parse-error
  `goal.call has no property Window`). The writer-facing note was right and ignored, because
  goal.call's stage-1 common-action yes/no fired certain and its module skipped stage 2.
  **Cost:** issue 41 — a window call silently built as a plain goal call; a day of the os
  bot's build. The fix was in a different place than the note (goal.call's yes/no *false*
  side, decider.json), not the examples. **If I had** a warning when the writer's chosen
  action is dropped in favour of a certain common action it directly contradicts, I'd have
  looked at the yes/no first instead of the examples. **new**

- **builder · goal.call is privileged; a module's own call-like action can't displace it.**
  The module question answered `window` (0.70) correctly, but goal.call's common-action
  yes/no (0.94) overrode it, and window.callGoal never got picked. **Cost:** issue 41;
  teaching patched the symptom (0/5 → 5/5) but the asymmetry stands — a common action's
  yes/no is enforced even when the module answer names another module. **If I had** the pick
  weigh the module answer against a common-action yes/no when they disagree (or let a module
  action claim a step a common action also claims), the teaching wouldn't be load-bearing.
  Raised to Ingi as a design question. **new**

- **builder · a per-action note that points at another action's note is dangling.**
  `condition/compare.notes.md:2` said "the same operator mapping as condition.if — see
  if.notes' table". Notes render only for the action the planner picked, so a `compare` step
  never receives `if.notes` — the model was pointed at a table it never sees. **Cost:** every
  compare step's operator teaching resolved to nothing; silent, no warning. **Done:** made
  compare.notes self-contained (this branch). **If I had** an orphan/dangling-reference scan
  over the notes (a note naming another note's file is a warning, like MarkdownTeaching's
  orphan scan), it would have surfaced on its own. **new**

## Debugging and tracing

- **builder · the decider's own scores aren't traceable.** `--debug={"llm":{...}}` shows the
  OpenAI writer call, but the stage-1 module/common scores and stage-2 answers that *decide
  the pick* come from the TypeSafe decider and never appear. **Cost:** on issue 41 I inferred
  the certain-common-action stage from reading `pick/list/this.cs` plus the os bot's separate
  trace, instead of reading it off a build. **If I had** `--debug={"pick":true}` — one table
  per step: each candidate action, its stage-1 score, stage-2 answer, final mark, kept/dropped
  and why — a misbuild would explain itself. (Same wish the architect filed.) **new**

- **builder · the `--debug` schema drifts between branches with no hint.** On plang-os-stable,
  `--debug={"...","maxLength":50000}` failed with `UnknownSetting 'maxLength'`; here it is
  `length.max`, and llm is `{system,user,response,schema}`. **Cost:** a wasted build and a hunt
  through `app/module/debug/setting/*.cs` to find the real field names. **If I had** the
  `UnknownSetting` error name the valid settings (or the shape be stable across branches), it
  would have been a one-line fix. **new**

## Tooling

- **builder · the binary's `os/` is hardlinked to source.** `PlangConsole/bin/.../os/system/...`
  is the *same inode* as `os/system/...`. I edited the bin copy for a measurement-only change
  (so as not to touch tracked source on a branch I was told not to commit to) and it showed up
  as a source modification. **Cost:** nearly left a prompt edit on the wrong branch; had to
  `git checkout` to undo both at once. **If I had** the build copy be a real copy (or
  read-only), a scratch edit next to the binary couldn't silently change source. **new**

## What felt good (keep it)

- **builder · the per-action notes system is the right shape, and the teaching lever is fast.**
  Issue 41 went from 0/5 to 5/5 window.callGoal with a **one-line** addition to goal.call's
  yes/no false side (`a call that says WHERE the goal runs … is window.callGoal, not a goal of
  this app`), and the guards held (plain `call Finalize` 5/5 goal.call; `call goal Render
  module=%files%` 5/5 goal.call). Teaching that lives in markdown, renders per picked action,
  and can be measured in minutes is genuinely good — keep it. The remaining gap is only *where*
  the teaching has to live (the decider's yes/no, not the writer's examples), which the pick
  trace above would make obvious.
