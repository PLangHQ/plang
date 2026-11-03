# action-event-path

Traces to: the builder's trace on set-add-order (`.bot/set-add-order/builder/v1/mark-event-trace.md`, 483dbb7f4; set-add-order decision 12). Ingi, 2026-11-03: "yes" (its own branch). Cut from runtime2 4fb4d5043 (set-add-order merged). Decisions: `.bot/action-event-path/architect/summary.md`.

## Why

A step that binds to an action's moment builds the wrong event:

```plang
- before action output.write call Mark
```

It builds `on.event(Event=%!app.type.step.on.start%, When=before, Action=goal.call(Name="Mark"))` 5/5: before each **step**, not before each `output.write`. `doc/events.md`'s first example prints `marks: 3` only because three steps follow the bind. Across other wordings it is right 0 times in 37. With a comma (`before action output.write, call Mark`) it builds `%!app.on.start%` 4/4.

## The cause (the builder's trace, read in its captures)

- The decider is right: `on.event 0.98 (module on 0.98)`.
- The writer's prompt holds the rule ("an action the step names as module.action → `%!app.module.file.read%` … not `%!app.type.action%`") and two examples of it (`after action output.write call CaptureWrite`, `before action file.read call OverrideFileRead`).
- The writer copies an example only when the step's whole text matches it, the called goal's name included:
  - the two example texts build the module path 3/3;
  - the same texts with only the goal renamed build `step.on.start` 3/3.
- Otherwise it takes the nearest example's path:
  - `after step call TrackAfterStep` gives `step.on.start`;
  - `before the app starts, call Say` gives `app.on.start` when there's a comma.
- The rule line is read as prose, not applied.
- Not the decider, and not on-event-examples' words (e9078aee5): the description still names "an action starting".

## The fix: teaching only (the builder's)

In `os/system/modules/on/event.{notes,examples}.md`:

1. **The rule first.** The Event parameter's teaching opens with it: a step that names an action (`module.action`) binds that action's own moment, `%!app.module.<module>.<action>.on.start%`. The list of other moments comes after.
2. **Examples that vary.** The `action module.name` examples get different called goals and different actions, with and without a comma, so that what the writer copies is the rule, not one sentence. Add a third (e.g. `before action http.get, call Meter`).
3. Nothing else: no C# check. Matching the step's words is the builder's no, since plang is multi-language.

The description stays in words with no paths (on-event-examples; catalog.md: the writer reaches for the first path a description shows).

## Proof

- **Before and after, fresh builds, cache skipped** (the builder):
  - the EvA–EvI wordings (0/37 right today), each ×3;
  - `before action output.write call Mark` ×5;
  - the two current example texts ×3, as the control;
  - the other moments, which must still build right: `after step call X`, `before each goal call X`, `before the app starts, call Say`, each ×3.
- **The teaching-edit rule:** the decider fixtures (`PickListTests`, `ConfirmTemplateTests`, `LineTwinTests`, `PickOptionTests`) and the page goldens (`ModulePageTests`, `TypePageTests`) re-run. A moved pin is re-pinned through `AcceptTheFixture`, and its diff is read and listed.
- **The plang tests:** `test/module/on/event` and the tests that bind events, no worse than runtime2.
- **doc/events.md:** the Mark example built and run, binding `output.write`'s start and printing `marks: 3`. Docs shows its output and closes `doc/verify.md` row 68.

## The line

builder (teaching and measurement) → tester (fixtures, plang tests) → auditor → docs and merge. The codeanalyzer and security have no C# and no permission surface to review here.

## Demolition

Nothing is removed. **Stays:** the on.event description in words, the step, goal and app examples, and the decider.

## Collision check

This branch's decisions (summary.md 1–3) were read against on-event-examples' (descriptions in words, no paths in the description) and catalog.md's rule. The paths stay in the notes and examples, not the description. Nothing is replaced.
