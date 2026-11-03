# tester v1 — action-event-path at 6f952c362 (teaching only, from runtime2 4fb4d5043) — PASS

Branch diff outside .bot: `os/system/modules/on/event.{description,notes,examples}.md` and `pick_golden.json`. No C#, no tests added.

## Counts (no red names; no tests added or removed, so the counts are runtime2's)
- C# `./dev.sh test --force` (forced: dev.sh's cache keys on C# roots, so a teaching change would otherwise skip suites): Modules 1500, Types 1049, Wire 656, Data 882, Generator 191, Runtime 1513 — all green.
- Teaching-edit fixtures by class: PickListTests 8/8, ConfirmTemplateTests 2/2, LineTwinTests 1/1, PickOptionTests 18/18, ModulePageTests 8/8, TypePageTests 10/10.
- plang `--test` after `./dev.sh build`: 323 total, 322 pass, 0 fail, 0 stale, 1 skipped (runtime2's). Tree clean after.

## The re-pin, read
`pick_golden.json` moves 9 `state1` strings (the stage-1 request, one per golden). Compared old vs new JSON value by value: in every one only the `on` module's e.g. lines differ — the same 8 lines each (the old three `after action llm.query…`, `before action file.read…`, `after action output.write call CaptureWrite` out; the five new `before action file.read call OverrideFileRead`, `after action output.write, call Record`, `before action http.get, call Throttle`, `before action variable.set call Trace`, `after action llm.query, call ShowCost` in). No answer, score or other module's line moved.

## plang tests that bind events — a `.pr` built from the old teaching
Tests (`.test.goal`) that bind an action: `module/on/event/afteraction` (`%!app.module.output.write.on.start%`), `app/event/AfterEachLlmQuery` (`…llm.query.on.start%`), `app/module/EachFileAction` (`…file.on.start%`) — built right already, would build the same. Two committed `.pr`s carry the very bug this branch fixes, but neither is a test: `module/on/event/override/Start.goal` (`before action file.read` → `%!app.type.step.on.start%`; its test file is `.test.goal2`, disabled) and `module/on/event/basic/Start.goal` (`before action variable.set` → `%!app.type.action.on.start%`; `Events.test.goal` does not use that step). A rebuild with the new teaching would build both right; neither can turn a green test red. Worth rebuilding by the builder, never by hand.

## Not mine
Builder's counts (action wordings 17/27 from 6/27, Mark 3/5 from 0/5) are LLM builds, not rerun. Note 3/5 on Mark is still a miss in 2 of 5.
