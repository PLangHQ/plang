# coder — app-systems

**Version:** v22 (all pushed; reviews by the architect, plang-21, gate by gate)

## v22 — the pick pass (814ca5209)
- `step.Mask` (`PLang/app/goal/step/mask/this.cs`): the step's variables as placeholders `%v1%…`, with a stem the
  step doesn't use. The `=> formal:` prefill is written through `Hide`, and `formal.Reader.Read` restores at its one door.
- `type.Offers(step) => kind.Offers(step)` (a choice set offers its values); `question.Values` = offers + none.
- `listed.Option` via one reader `Chosen()`, shared with `Call()`.
- `conversation.Create` takes any value. `llm.query` refuses one that isn't an llm answer, naming the variable.
- Pins: `PLang.Tests/Wire/App/Decider/MaskTests.cs`, plus 2 in `QueryConversationTests`; `pick_golden.json` re-pinned.
- Next: the builder (plang-75) switches its templates to `s.Mask.Text`. Queued: the event slot wire contract,
  task stage 3 (`parallel`), the `/system/` new-file rule, the hash kind, default-true options → choices.

## What this is
app-systems makes every `app.X` the type X, so the plang path, the C# path and the file path agree. The
architect's plan is `.bot/app-systems/architect/plan.md`; its running log of rulings is
`.bot/app-systems/architect/summary.md`. Working mode: the architect relays Ingi's rulings and gates every
commit (a full six-suite run); the coder shapes first, builds, runs the suites its change touches and
`plang --test`, then pushes and reports.

## Done since v14 (2026-10-02)
- **Goals are found by their `.goal`** — `goal.Pr(source)` holds `X.goal → .build/x.pr` once; `goal.Load` takes
  the `.goal`; `Find` is one method answering a Data (goal / miss / `GoalNotBuilt`), a goal's name compared
  ignoring case; the goal list walks `.goal` files, never dot-folders. Executor's own (wrong) copy of the rule
  went.
- **The /system/ overlay** is one door: `path.Place(context)` — the app's own, then the os's.
- **Shortcuts** — the system shortcut goals (`os/system/shortcut/*.goal`) built; `%!goal% %!step% %!error%
  %!test% %!channel%` read through them; the context keeps only `!app`/`!context`.
- **Numbers** — every number written as text reads through `number.Parse` (an integer is long; past long a
  biginteger, exact); json documents and llm tool arguments no longer turn integers into doubles.
- **Choice over a closed set** — three kinds of set (`member`, `named`, `family`), one birth door
  `set.For(clr)`; a `[Default]` is born through `choice.Create`; `crypto.hash`'s Algorithm is
  `choice<hash.kind>`.
- **Templates** — `file.read`'s `Template` is a `choice<template.kind>` (plang); the template mark everywhere is
  the kind (the `.pr` bytes unchanged); a dotted key answers only as an extension.
- **Builder** — the Option question (`ask:` on a notes line → a stage-2 choice of the option's values + none);
  a prompt written as text is a user message; a list read off one value holds it; an event written as a value is
  refused saying its path; the formal reader takes a list of dicts for a typed list.
- **Settings** — the call stack and debug read through the store's cache (holders and `App.Refresh` gone);
  the store is `/.data/data.sqlite`.
- **Fixes found on the way**: the error show loaded `show.goal` (it is `Show.goal`); a goal channel's
  `%message%` for a failure; `data<T>.From` lost a miss; json `long : double` unification (3 places).
- **Goal flags derived from Path** — `IsSystem`/`IsSetup`/`IsTest` answer from the plang path; the `.pr` drops
  the four keys (the reader still skips them by name until the tracked `.pr` are rebuilt).
- **A stored value settles where it is written** — a template given to `list.add/set/remove/contains/indexof/any`
  (the one door `data.Given(then)`) and to a variable's member write (`variable/code.Set`: `%dict.k%`, `%x[0]%`)
  renders at the write, never on a later read. Every storing action was checked.
- **on.event** — the `on` node refuses a name that is no event itself (`NoEvent`); bind says whether the item or
  the event is missing; When/Scope/Action open through `Use` (a `%unset%` is the answer, not a null).
- **permission** — `grant.Allows(request)` (was `Covers`); `TryCover` gone (its actor check repeated Allows').
- **llm tool arguments** — opened by the json kind (`Open` + the item's `EnumerateItems`): a nested object
  navigates; `[1,2]` answers `ArgumentsNotAnObject` (it used to throw out of the whole query).
- **Teaching** — `loop.foreach`'s no-`as` example writes `%item%` (it taught `%row%`, which isn't set).
- **obp-cleanup.md** lists only what is open: resolved entries are removed (the commit is the record).

## Waiting
- **v18 item 1** (a hash holds its kind) — the shape is accepted; `signing.setting.Hash` is with Ingi.
- `goal.call` `Wait` vs `Parallel` — with Ingi.
- obp-cleanup entries sized and left (not small): CountRaw (53 sites), the action `Resolve` rename (~64), the
  code provider's DLL string (rides the snapshot record), the variable store's Clone, ContainerFamily, the
  goal reader's key skips.

## Code example — one door, the rule stated once
```csharp
// goal/this.cs — the .pr a .goal is built to, read by PrPath, Load and setup's discovery, nothing else
public static path.@this Pr(path.@this source)
    => source.Parent.Combine(".build").Combine(source.FileNameWithoutExtension.ToLowerInvariant() + ".pr");
```
Before it, Executor computed `"/.build/" + name.ToLower() + ".pr"` itself (wrong for a subfolder), setup wrote
`"/.build/setup.pr"`, and Find did string math on folder names.
