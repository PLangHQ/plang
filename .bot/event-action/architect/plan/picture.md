# event-action — the design as it stands (2026-10-29)

One page: each piece, what holds now, the decisions behind it, what it replaced. Built = in stage 1 (2c7b8f78d). The log is `../summary.md`.

## What holds

| # | piece | holds now | decisions | built |
|---|---|---|---|---|
| 1 | what's bound | an **event**: kinds `call` (on.event, a goal call only), `clause`, `code` (C#), `inherited` | 4 | yes |
| 2 | `%!event%` | the bound event; `.name`, `.when` readable; `!item` what it ran for, `!data` the value (a failure is `%!error%`); kept in a snapshot by `{action, n}` | 21, 23, 27, 22 | yes |
| 3 | a moment | `on`: `before`, `after` (its start), named moments (`load`, `error`, `create` …) each an `on`; plang type `on` | 13, 26 | yes |
| 4 | a step's binding form | today's `%!<item>.on.<moment>%` + `When` | 12 | yes |
| 5 | where events live | on the actor that bound them; `%!app.event%` reads the running actor's | 4 | no |
| 6 | whose list | a goal its own; a step its own; an action its **catalog action's** (`llm.query`, shared by every llm.query step); a variable its **name's** when an event is bound on that name, else the variable type's | 10, 28, 24 | no |
| 7 | how a list fills | **open, see below** | 3, 10 vs 7, 33 | no |
| 8 | plang's own goals | one list; `event.setting.goal: app \| all` (default `app`): a run inside a `system/` goal runs no events; `goal.IsSystem` stays as the one member asked; checked in `side.Start` when a side holds an event | 31, 32 | no |
| 9 | order | `before` and `after` both in bind order. An action: app's before → timeout's deadline → cache lookup → **action** → cache store → step's `on error` → app's `on error` → app's after | 4, 11, 14 | no |
| 10 | a handled error | `after error on each action, call Log` sees it | 14 | no |
| 11 | running | one line per side: the actor's list for this item, `before.Start(item, context)` | 5 | no |
| 12 | system events | `/system/event/` loaded by both actors at the root's start; `event.setting.system: load \| skip` (default `load`); a sub app inherits, and its own `system/event/<name>.goal` replaces the same-named | 8, 16, 17 | no |
| 13 | sub apps | `Sub: inherit \| none`, no `lock` | snapshot 251, 18 | no |
| 14 | a goal call | finds its goal in the app its caller belongs to | 18 | no |
| 15 | the goal | `Enter` into `Start`, `Leave` onto the frame, the parent's goal its own kind; return early | 6 | no |
| 16 | whole-module binding | kept (`before each http action`) | 33 | no |

## The collision: how a list fills

| | what it says | from |
|---|---|---|
| **pull** | a list isn't loaded until its first run; then it loads every event that applies (its module's, its own, by pattern, its type's); it never asks again; a later event joins the loaded lists it applies to; an unloaded list is never added to, it loads everything first | Ingi, 3 and 10, and his words before 34 |
| **push** | an event bound on one item is put into that item's lists when bound (made then); bound on a module, into each of its actions'; the only question is `Is(type)` | mine, 7; 33's item-base member for the module |

The plan the coder got says pull (3, 10) **and** push (7). Stage 2's work in progress is the push version (parked on `event-action-wip-stage2`). 34 (pull, push dropped) went to the coder without Ingi's go and is held.

With pull, "does this event apply to this item?" is asked when a list loads and when a later event is bound. The coder's shape for it, waiting:

```csharp
// event/this.cs
internal bool Reaches(global::app.type.item.@this item) => Each is { } type ? item.Is(type) : item.Is(Item);

// type/item/this.cs — NEW, item's locked base
internal virtual bool Is(@this other) => Equals(this, other);

// goal/step/action/this.cs — a catalog action is part of its module
internal override bool Is(global::app.type.item.@this other) => base.Is(other) || Module.Is(other);
```

## Replaced along the way

| was | by |
|---|---|
| 9: "each goal" skips `system/` by a path pattern | 28: two tables, then **31**: one list + `event.setting.goal` |
| 14: `!data` is the Data whole | **23**: the value; a failure is `%!error%` (Properties hold no Data) |
| 9: `goal.IsSystem` goes | **31**: it stays, the one member asked |
| 10: a list per program action | **28**: an action's lists are its catalog action's |
| 7, 33: push at bind | **open**: pull (3, 10, Ingi) |
| 244/245: `%!app.event.goal.before` as the binding path | **12**: today's form |

## Open for Ingi

1. Pull, as he spelled it: 7's push and 33's member go.
2. The "applies" question's shape (above), with its new `Is(item)` on item's base.
3. The green light for stage 2.
