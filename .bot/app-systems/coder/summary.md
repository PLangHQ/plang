# coder — app-systems

**Version:** v18 (all pushed; reviews by the architect, plang-21, gate by gate)

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

## Waiting
- **v18 item 1** (a hash holds its kind) — the shape is accepted; `signing.setting.Hash` is with Ingi.
- **Goal flags derived from Path** (`IsSystem`/`IsSetup`/`IsTest`, a flat copy) — shape sent; the `.pr` bytes
  change, so it is the architect's first.
- `goal.call` `Wait` vs `Parallel` — with Ingi.
- The builder bot: the `ask:` lines and decider template (issue 25/28), the 7 stale os `.pr` (issue 30).

## Code example — one door, the rule stated once
```csharp
// goal/this.cs — the .pr a .goal is built to, read by PrPath, Load and setup's discovery, nothing else
public static path.@this Pr(path.@this source)
    => source.Parent.Combine(".build").Combine(source.FileNameWithoutExtension.ToLowerInvariant() + ".pr");
```
Before it, Executor computed `"/.build/" + name.ToLower() + ".pr"` itself (wrong for a subfolder), setup wrote
`"/.build/setup.pr"`, and Find did string math on folder names.
