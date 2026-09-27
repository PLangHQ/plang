# Stage 9 worklist: action handlers holding their owner's logic

Where this comes from: Ingi asked (2026-09-27) for "cs that have methods more than 1 line but should really just be reference pointers to a class that should own the logic". An architect sweep of every handler in `PLang/app/module/action/` found that 60 of 126 hold logic belonging to an owner, and 66 are one-liners or pure plumbing. The architect spot-checked the top claims against the code (setting divergence, file.read, variable.set, list.contains). This file is stage 9's input, beside its checklist (plan.md, stage 9 row). **You own the final shape.** The "owner" column names where the logic should go, not the finished code.

## Already covered by another stage (don't do twice)

| Handler | Goes in | Why |
|---|---|---|
| `setting/get.cs`, `set.cs`, `remove.cs` | 7e-2b-ii | **A real bug, not just shape.** They read and write the store's `settings` table with raw keys (`store.Set("settings", key, …)`, `setting/set.cs:19-20`), but the owner keys rows `<actor>!<path>` (`actor/setting/this.cs:121,135`), in the same table. Neither side sees the other's rows. 7e-2b-ii replaces get/set with `setting.save`/`remove` going through `Context.Setting`, which removes the divergence |
| `cache/wrap.cs`, `timeout/after.cs`, `event/on.cs`, `mock/intercept.cs`, `on/error.cs` | 8 | Deleted there: modifiers become events on the owner. Whatever they compute today (retry and filters in `on/error`'s Wrap, the second glob matcher and 5 stray helpers in `mock.intercept`) moves to the owner in stage 8; error matching belongs to `Error` |
| `test/start.cs` (~210 lines), `test/report.cs` (~260 lines of StringBuilder) | 9, after 7c | 7c created `test/report/this.cs` and the session; stage 9 makes the actions one-line doors to them. The report renders through templates (presentation is os templates, not C#). `test/start` copies `timeout.after`'s timing, which it gets from the owner instead |

Also pulled forward to stage 7's cleanup (decision 56), because this branch's own stages made them: `variable.set`'s `%!…%` fork, `test.start`'s orchestration (`app.test.Start()`), `test.report`'s rendering, and debug's forwarding properties. They are no longer stage 9's.

## Top findings, in order

1. **`variable/set.cs`** (356 lines; the conversion block at about `:110`, about 95 lines): an `as <type>` conversion chain plus `!`-routing. The owner: the target type's `Create` (the value is born through its type) plus the variable's own `Set`. The handler becomes one line.
2. **`file/read.cs`**: the rule's own example (stage 9 starts here, Ingi). Branches on `path is http` (`:38`) and re-derives the kind from `Extension.TrimStart('.')` at three sites (`:40,69,99`, a raw hand-off). The owner: `path.Read(…)`, with per-scheme overrides (file, http), and the extension-to-kind step asked of the path once.
3. **`test/discover.cs`**: discovery belongs to test's list.
4. **`loop/foreach.cs`**: body slicing belongs to the step list. `GetBodyActions` is dead; delete it.
5. **`code/load.cs` + `module/add.cs`**: assembly loading is written twice. One owner (code's).
6. **`channel/set.cs`**: defaults duplicated from the channel. The channel owns its defaults.
7. **`error/throw.cs`**: builds the error by hand. `Error` owns its construction.
8. **`list.where`**: should be a virtual `Where` on item, overridden by list and dict (no type-switch in the handler).
9. **The list module re-implementing list members**: `list.contains` walks and compares itself (`list/contains.cs`) although `list.@this.Contains(item, context)` exists (`type/item/list/this.cs:775`). The same goes for first, last, get, count, any, indexof, join, split, flatten, reverse, remove, unique, range, group, sort, set and add: each becomes one line to the list's own member, and a member the list lacks is added to the list, not to the handler.
10. **Also flagged:** `output.ask`, `goal.call` (Build/Validate heavy), `goal.return`, `debug.tag`, `mock.reset`, `mock.verify`, `module.remove`, `channel.remove`, `code.list`/`remove`/`setDefault`, `timer.start`/`end`, `environment.start` (three optional slots → one: what to start), `condition.if`/`elseif` (re-derive truthiness; the value answers, `IBooleanResolvable`).

## Patterns (fix once, not per handler)

- **A. The list module's shared 6-line preamble** (read the variable, start it, check it's a list): becomes one member on the variable, "the list this variable holds".
- **B. Actor-by-name resolution**: 5 copies with 3 different fallbacks. One door, on the actor list (`app.actor.Get(name)`).
- **C. Modifier `Start()` is `Ok()` and the logic lives in `Wrap`**: disappears with stage 8.
- **D. assert's static `AssertSnapshot.WithVariables`**: a static with no owner; moves onto the snapshot or the variable memory.
- **E. The math provider is a middleman** to the static `number.Round(n, decimals)`: an opened box. The number owns it: `await Value.Round(Decimals)`.
- **F. File path verbs take CLR `bool`/`string`**, and the `if (!Path.Success)` preamble repeats 6 times: typed `Data<T>` slots; the generator's required-parameter guard, or the path's own result, carries the failure.
- **G. Duplicated assembly loading**: see 5.
- **H. mock and test objects hold only data** while the handlers hold their behavior: the behavior moves onto the objects.

## Carried in from stage 7's cleanup (decision 66)

- **`test.report.Write` (`app/test/report/this.cs`)** chooses the artefact with `if (chosen == Format.JUnit) {…} else {…}`, a *fork*. Each format should write its own artefact (content and file name), for example as a serializer chosen by format, so the value writes itself.
- **Console presentation still in C#:** the report's summary and per-test lines, `coverage.Text`'s tables, and `test.Failure`'s block are all built with a StringBuilder. Presentation is os templates.

## Outside `Start()`

- Heavy `Build`/`Validate` in `variable.set`, `goal.call`, `file.read`, `llm.query`, `test.tag` and `loop.foreach`: same rule. Validation belongs to the thing validated.
- `HttpBuildHelpers` is a static class (no statics): its members move onto what they describe.
- `signing.sign` is both an action and an item: one of the two names goes.
