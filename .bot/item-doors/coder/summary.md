# coder — item-doors — summary

## Version
v2 (stage 1: a value is born whole — lists, singular nodes, the per-type required ctor, kinds born with their
owner; item-doors 32–96)

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
- **dict, text, number, setting born whole**: each takes the app's type (dict's parameterless ctor gone; a call
  frame's Tags from the stack's dict type, as 51; a setting fills an empty dict member like a list). text: the
  readers ask the list for `{text, kind, template}`; a derived text holds `Type.Family` (the app's plain text);
  source's template Text is born from the list. number: `Kind` is `Type.kind` (no copy), Write through
  `Type.kind.Write`; the courier and the reader re-birth what they make holding the app's `{number, <kind>}`; an
  arithmetic result of an operand's kind holds that operand's type, a climbed one keeps the class-built fallback
  until the kinds item (21). The operator births (`string → text`, the 15 number lifts, Parse) keep the class-built
  fallback (50). setting: path/module/action nodes take the type; the `.Path`-only makers ask the list.
- **content, separator, step 3 (the stand-ins)**: content composes the app's file/url entry with the app's format
  kind (75). The `Type => new(...)` overrides went for snapshot, table, store, query (+ clauses), archive, hash,
  signature, clr, ask, code registration and override, size; each is born holding the app's type. A setting's
  birth fills every non-nullable member the values leave out with its class's empty value (culture's is the
  machine's). item's own `Template` went (a template reads only `Type.Template`). Kept, marked: error (its makers
  have no app — the implicit-conversion branch), event (shared across apps — the event door, Ingi's), the
  operator births (50), the kinds-item markers (number's climb, hash's and size's kind-made births, separator's
  offers).
- **path born whole**: the base ctor takes the app's `{path, <scheme>}`; `Resolve` asks the app's list (file, and
  http by the url's scheme); a path made from another (parent, child, extension, a listing's entries, a transfer's
  target) holds its source's type. A scheme handler's contract is its static `Resolve`, never a bare ctor.
- **A failure report keeps the test's reading (68)**: an assertion keeps Expected/Actual as it showed them in the
  test's context; the runner's report writes those. `choice.Member` went (70): a hash kind by name is the hash
  type's own kind; the template kind is the template set's member (no template type; stage 6).
- **A dict's `!key` on an unread dict (39)**: a fact read with `!` is a use — a resting source (a `.pr` literal, a
  json read) opens and its value answers (`source.Fact`; `Fact` is async).
- **An error's exception (60's red)**: `error.Exception` is C#-only (internal); plang and the 500 templates read
  `%!error.trace%` (its C# type, message and stack as text).
- **A kind is born holding its owner (79, 81, 82; 925787b46)**: every kind class is a `kind<T>` whose only ctor takes
  the app's entry for T; that owner is its `Family`; the registry enlists each with `types[T]` (`Registry.Owner`).
  `Owner` is a type (the empty kind keeps its own); a kind answers only its own aliases (`Names`/`Keys`). Choice
  sets read their options off the type they're handed (`choice<T>._set` gone); the family set reads the held kinds
  lazily; template's kinds stay ownerless (stage 6). Settings' defaults are `[Default("…")]`, made by the birth.
  No-context doors (number's/duration's operator statics, size's `Standards`, `reflection.Fallback`, test.format's
  report kinds) are class-built, marked with 50.
- **Fixes pushed on their own**: the `--debug` core dump (dcd7eb293: the app in its asker's memory walked forever;
  the debug view answers "(cannot be shown: …)"); an image holds its declared type (b0cf849dc, b8813dd03: strict from
  the declaration, the kind from it or the extension); a type with a kind and strict builds (aa8bb032f: the build's
  strict probe reads through the Data, 87); a path-backed image reads nothing at the set (3956a8e09, 90).
- **Registry.Enlist scans once (fix B, Ingi; 290aad619)** and **dev.sh's tested snapshot is taken at the sweep's
  start (Ingi; 6f4d0572d)**. Test slowness measured: an app's birth is ~150 ms warm, ~90% of the suites' CPU; Ingi
  chose to live with it (no static scan cache, no registry clone).
- **In progress, uncommitted — 89, the app writes itself**: the 7 `[JsonIgnore]` on app's nodes go; `app.Output` writes
  its members (Store → its [Store] record), `variable` in Debug only, an AsyncLocal guard writes its name when met
  again. Held: on the wire `actor` and `parent` are refused (NoWireContract); the architect has it with Ingi.

## Interims (each flagged in code, each has its turn)
- `culture.Machine` (the app setting's default) makes its own type — setting defaults move to birth in group 6 (17).
- input's numbers are `number.@this.Create(x)!` until number is born whole; then `Create(x, types[...])`, the `!` goes.
- choice values (mouse Action/Button, navigate To) are made on read; at choice's turn they're held from birth.
- The C# implicit operators (50): `data<T>(T)` keeps a private context-less birth (and refuses a null); binary's and
  duration's keep a class-built type — all until the implicit-conversion branch.
- number's 15 static kinds and `Kinds` stay only for the C# operator births, owned by one class-built number type
  (50); a number with an app asks its family's kinds.

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
- C# sweep after each push: only the known reds (6 Wire snapshot/resume); surface re-pinned with the decision each
  time (last: 79); the decider pin is back to its old value (41). Small fixes are proved on a clean worktree with
  their affected suites, not a forced sweep (Ingi).
- plang tests (after 79): 303 total, 277 pass, 11 fail (the long-standing reds), 12 stale, 3 skipped; the four
  strict-kind tests green on the builder's rebuilt .pr.
- plang tests unbuilt here (no decider key): `test/plan/item-doors/app/type/dict/key/KeyIsTheFirstKey.test.goal`,
  `test/plan/item-doors/app/type/input/modifier/ModifierIsTheFirstKeyHeld.test.goal` — the test-designer builds them.

## Next
- 89 once Ingi rules on actor/parent on the wire, the 13 KB of type facts and nulls, and `variable`'s nameless list:
  then its tests (each view's members, Debug-only variable with `%app%` as its name, app.pr's round trip), the
  `data/this.Output.cs` comment, the surface re-pin citing 89. The tree holds a `Tmp_Capture` test in AppNodesTests
  — remove it.
- Then the stage-1 report: every stage-1 test, what stays marked (error, event, the 50 fallbacks), anything wrong.
- Backlog from the architect: "this type, made strict" has two callers (image courier, builder's Tighten) — a third
  makes it a type member (Ingi's); the strict imprint has two doors (image.Create and set.cs) — one door: the courier.
