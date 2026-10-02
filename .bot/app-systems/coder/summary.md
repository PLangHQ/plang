# coder — app-systems

**Version:** v22 (all pushed; reviews by the architect, plang-21, gate by gate)

## After v22, later on 2026-10-02 — each gated (the 18 known) and accepted by the architect
- 7a26894cd `size` (561): `type/item/size`, kinds iec/si; a text keeps its suffix's standard, a count is written in
  `%!app.type.size.setting.standard%` (a type's setting class reads at `app.type.X`). `MaxDownloadSize` is a size;
  StatInfo's `Length` → `Size`, born in Stat; `%file.Size%` reads it.
- cb60c22d0 + b5634bd69 `progress` (563): `http/type/progress` {received|sent, total, percent}; `http/code/body`
  reports every 500 ms and once more via `Done(error)` — the last report's Data carries how it ended (HashMismatch,
  ResponseTooLarge, a cut body's network failure via `http/code/failure`). Upload sends through `http/code/content`.
  A type answers its own `.setting` (`Setting.Of(type)`, no `is`).
- 0c1272d0f + 43add1730 + 6c4725af1 the archive module (559, 574): `archive.pack`/`archive.unpack`; formats are
  the archive type's kinds (`module/archive/type/archive/kind/{gzip,deflate,brotli,tar,tar/gz,zip,oci/layer}`);
  `code/Default` finds the format (named, suffix, first bytes). Unpack guards on the bundle base (path `Follow`,
  `Link`, `Mode`); a name that is the folder itself never lands. `item.Pack` decides what a value packs as;
  the archive holds `{value, held:{type,name}}`. Compression left `Data`; `variable.compress` deleted. Pack packs the
  binding a step names (`item.Get(ctx)`).
- 1adf16188 (572): path `relative` (a path) / `extension` (text) are dot members; `!` reads the Data's facts and a
  reference's (`item.Fact`, answered by `content`); `Data.Path` internal.
- e69c0ae86 (567 part 1): the type catalog lists every `[LlmBuilder]` member; methods carry `Arguments`.
- e04350246: a whole-`%ref%` template source writes the bound value (plang's format round-trips a signed one).
- Next: 567 part 2 (markdown loader for `os/system/type/<type>/…`, statics removed per type as markdown lands),
  then 564 (App.Statics removal + dynamic members) — shape first. Friction: `.bot/app-systems/coder/friction.md`.

## After v22 (2026-10-02), each gated (the 18) and accepted by the architect
- 14de729af: an unknown `.pr` property fails the run at `action.Instance()` ("goal.call has no property Wait;
  rebuild the goal"); a list made from one value holds it (`list<T>.Create`, `list.Create`).
- 54d452dea: a new `/system/` file lands where its folder is (`ValidatePath`: present → housed → as written).
- 6cbf36046: permission `Verb`/`Match` lowercase, Regex dropped, `VerbLabel` gone. The member-type change is
  held: a `.pr` written from CLR values holds dict members as unread Data rows that `Create`'s type-tests miss
  (pinned, skipped) — (i) dict reader opens leaf rows vs (ii) async birth, with Ingi.
- 8a4ef5fc7: masking removed (Ingi): the step is read as written; templates read `s.Text`.
- d6ae465ca: permission declares Example/Shape/Description.
- d2aa1358d + 4af605b25: offers. `type.Offers(step)` async, items; `type<T,L>` offers its collection's (goal
  names reachable via `goal.list.Chain`/`Beside`, the walk `Find` shares; type names), then the step's
  variables; the write-to is left out; a chosen offer writes itself through `formal.Writer`.
  `variable.set` Type is `data<type>`; a type is born from its name or `{name,…}`.
- 701ae4049: a list of records shows one of its element (`list<permission>` → `[{path, verbs}]`).
- The event-slot "no wire contract" no longer reproduces on the current core.

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
