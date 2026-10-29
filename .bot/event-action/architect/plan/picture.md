# event-action — the design on one page (rewritten 2026-10-29, decision 36)

What holds now. The decisions it comes from are in `../summary.md`; the replaced ones are listed at its top. `start.md` is the same at length.

| # | piece | holds now | decisions | built |
|---|---|---|---|---|
| 1 | what's bound | an **event**: `call` (on.event, a goal call only), `clause`, `code` (C#), `inherited` | 4 | yes |
| 2 | `%!event%` | the bound event: `.name`, `.when`, `!item`, `!data` (the value; a failure is `%!error%`); a snapshot keeps it by `{action, n}` | 21, 22, 23, 27 | yes |
| 3 | a moment | `on`: `before`/`after` (its start) and named moments, each an `on`; plang type `on` | 13, 26 | yes |
| 4 | binding form | today's `%!<item>.on.<moment>%` + `When` | 12 | yes |
| 5 | where events live | on the actor that bound them, in bind order; `%!app.event%` reads the running actor's | 4 | no |
| 6 | whose list | a goal its own; a step its own; an action its catalog action's; a variable its name's when bound on the name, else the type's | 10, 28, 24 | no |
| 7 | how a list fills | **pull** when it isn't loaded (its first run loads every event that applies); **push** when an on.event step runs (into loaded lists only) | 3, 10, 34 | no |
| 8 | the one question | `item.Is(@event.Item)`: the item itself, of that type, or (a catalog action) of that module | 34, 33 | no |
| 9 | by pattern | `before each goal in api/*`: applies by the goal's path, the one glob matcher | 35 | no |
| 10 | plang's own goals | one list; `event.setting.goal: app \| all` (default `app`); `goal.IsSystem` asked when a side runs | 31, 32 | no |
| 11 | order | bind order. An action: app's before → timeout → cache lookup → **action** → cache store → step's `on error` → app's `on error` → app's after | 4, 11, 14 | no |
| 12 | a handled error | `after error on each action` sees it; the failure is `%!error%` | 14, 23 | no |
| 13 | system events | `event.setting.system: load \| skip` (default `load`); both actors load `system/event/` at the root's start; a sub app's own file replaces the inherited one of that name | 8, 16, 17 | no |
| 14 | sub apps, calls | `Sub: inherit \| none`; a call finds its goal in its caller's app | 251, 18 | no |
| 15 | the C# kind, the check | `event/code/`; `context.Enter`; `Start` for every kind | 7 | no |
| 16 | the goal | `Enter`, `Leave` onto the frame, the parent's goal kind; return early | 6 | no |

## Open for Ingi before stage 2

1. How a step names a pattern (a first idea: `%!app.goal["/api/*"].on.start%`, the goal list answering a pattern for a wildcard key).
2. Which side answers the one question so a pattern fits: the thing bound on ("does an event on me run for this goal?") or `Is(item)` handing it over. Both on item's locked base.

## Where the code is

- `event-action` at 2c7b8f78d: stage 1, green.
- `event-action-wip-stage2` at 2bc303d42: the push version, parked, not to be merged as it is.
