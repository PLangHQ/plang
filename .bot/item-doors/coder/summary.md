# coder — item-doors — summary

## Version
v2 (stage 1: a value is born whole — lists, singular nodes, and the per-type required ctor; item-doors 32–64)

## What this is
Every plang value holds the one type object its app made for it (item-doors 33): the maker hands it in, and a value
never builds its own (`new type.@this(...)`) or falls back to one. v1 moved the scalar types' Type overrides; v2
does lists, the program nodes, the singular nodes for plurals plang reaches, and then each value type's constructor
in turn, smallest first. Decisions are numbered per branch (item-doors 1–47, `.bot/item-doors/architect/summary.md`).

## What was done
- **Lists (32–37)**: list and list<T> take their type, required; 12 list subclasses pass the app's; goal, step,
  action, test make their own node lists from the app's type list; settings fill empty list members at birth.
- **Singular nodes (39, 41)**: `dict.key` (`%dict!key%` the first key, `.list` every one, internal);
  `input/modifier` + `input/modifier/list` replace the `Modifier` enum: mouse and key hold `Modifier` from birth,
  `%click.modifier%` is the first key held, `.list` all, the line writes its `Code` (Ingi's name). key's VK naming
  table stays on the key (43).
- **Required ctor, per type (33)**: guid, date, time, tag, culture, prose, note, datetime, input (mouse, key, text,
  navigate, modifier, modifier.list). Each ctor and `Create(raw, type)` takes the type; couriers hand the declared
  one; readers ask `ctx.Context.App.type.list`; the Windows window gets the type list at birth from
  `screen.window.Start`. `data.Value<T>()` hands `T.Create` the app's type.
- **Parts take the context (42, 47)**: datetime's parts and duration's units are one-context `[LlmBuilder]` members
  answering their carrier: `data<number>.From(context.Ok(Value.Year))`. `%now.year%` reads the same.
- **Measured (42 widened)**: 1,726 production lines use a C# → plang implicit operator; 1,330 are error status codes
  (906 generator-emitted). Ingi (50): those operators and the error status are their own branch after item-doors.
- **Kinded goes (49)**: a kindless leaf declared a kind is made through the type's own `Make` (its courier re-births
  a value of its class holding the declared type); base64 and binary are born with the app's type.
- **Every Data is born with its context (52)**: a Data with no context (handed or its parent's) is refused at birth;
  its empty slot is its app's null — one value per app, held by the null type (`type.Empty` keeps an empty value
  whose `Copy()` is itself). `FromError`/`NotFound`/`Uninitialized`/`Null` take the context (the emitter hands it);
  `null.Instance`, `item.Absent` and the context-less `Ok` statics are gone; `type.Get(key, ctx)` and
  `module.Get(name, ctx)` answer with the asker's context.
- **Call stack and duration (51, 56, 57)**: the context hands its stack the app's datetime and {duration, short}
  types, the stack hands them to each frame. duration is born `(TimeSpan, type)`, written by its type's kind
  (`kind.Write(item, IWriter)`, Ingi's locked addition); `Resolve` asks each of the app's duration kinds to parse.
  `duration.Text` and its `ToString` are gone: a duration shown as text is its Output (Fluid renders a span that way).
- **Fluid and a type's face (60, 61)**: one Fluid value over any plang item (`ui/code/Fluid.cs`): text is its Output,
  truthiness its own, `> <` and equality its Compare, a leaf's raw backing for Liquid's sort. A type written alone in
  the Out view is its face (`text`, `list<path>`); its facts are navigated.
- **The app's nodes (62–64, group 8)**: `app/type/node/{this,current,root}` are items of the internal type `node`,
  not types. Each holds `type` (the type list's plain entry), `list`, `current` where in play. Written alone:
  the asker's list, the one in play (`%!app.goal%` the running goal), every type's facts (`%!app.type%`).
  `Registry.Replace` is gone; events bind on the plain type (`%!app.type.goal.on.start%`, `app.goal.type.on`).
  The goal offer is gone (64): a goal slot is offered the step's variables; the type `type` is made by its list
  holding it (its values, as a choice holds its set) and offers the type names.
- **image, choice, bool born whole**: each ctor takes the app's type; makers hand it (image: the kind its magic bytes,
  extension or wire name, `{image, png}` from the app's list, so its mime is the registered kind's; choice: the
  app's `{choice, <set>}`; bool: the app's bool). The C# operator births (`T → choice`, `bool → bool`, `True`/`False`)
  and a set's offers keep a class-built type until the implicit-operator branch (50). `choice.Member(symbol)` for
  the sites that want only the member. Mouse and navigate hold their choices from birth. An enum that declares no
  plang name is a bare choice.
- **A dict's `!key` on an unread dict (39)**: a fact read with `!` is a use — a resting source (a `.pr` literal, a
  json read) opens and its value answers (`source.Fact`; `Fact` is async).
- **An error's exception (60's red)**: `error.Exception` is C#-only (internal); plang and the 500 templates read
  `%!error.trace%` (its C# type, message and stack as text).

## Interims (each flagged in code, each has its turn)
- `culture.Machine` (the app setting's default) makes its own type — setting defaults move to birth in group 6 (17).
- input's numbers are `number.@this.Create(x)!` until number is born whole; then `Create(x, types[...])`, the `!` goes.
- choice values (mouse Action/Button, navigate To) are made on read; at choice's turn they're held from birth.
- The C# implicit operators (50): `data<T>(T)` keeps a private context-less birth (and refuses a null); binary's and
  duration's keep a class-built type — all until the implicit-conversion branch.
- number keeps its own copy of its kind (`Kind`) until its turn, when it writes through `kind.Write` (56).

## Open
- test/plan/app-systems/type/duration/KeepsItsKind.test.goal asserts `%short.text%` (57: Text is gone); new steps
  sent to the architect, the test-designer rebuilds it.
- text made from a value (`%d as text%`) is `Convert.ToString` of its CLR value — text's turn (57).
- A sweep of the ToString-for-display uses is its own later item (57).

## Code example
```csharp
// a value is born holding the app's type, handed in by its maker
public @this(System.DateTimeOffset value, global::app.type.@this type) : base(type) { Value = value; }
// the reader hands the context's
new global::app.type.item.datetime.@this(reader.DateTimeOffset(), ctx.Context.App.type.list[typeof(global::app.type.item.datetime.@this)]);
// a part answers its carrier, born from the asker's context
[LlmBuilder] public data.@this<number.@this> Year(context.@this context) => data.@this<number.@this>.From(context.Ok(Value.Year));
```

## Checks
- C# sweep after each push: only the known reds (6 Wire snapshot/resume, ImageFromBytes); surface re-pinned with
  the decision each time; the decider pin is back to its old value (41).
- plang tests unbuilt here (no decider key): `test/plan/item-doors/app/type/dict/key/KeyIsTheFirstKey.test.goal`,
  `test/plan/item-doors/app/type/input/modifier/ModifierIsTheFirstKeyHeld.test.goal` — the test-designer builds them.

## Next
On in the order: path, dict, text, number; then setting, content, source, separator; last, number's static kinds and `KindOfClass` go.
