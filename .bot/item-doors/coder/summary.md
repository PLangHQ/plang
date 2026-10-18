# coder — item-doors — summary

## Version
v2 (stage 1: a value is born whole — lists, singular nodes, and the per-type required ctor; item-doors 32–47)

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
  (906 generator-emitted). That scope and the error status are with Ingi.

## Interims (each flagged in code, each has its turn)
- `culture.Machine` (the app setting's default) makes its own type — setting defaults move to birth in group 6 (17).
- A call frame's `StartedAt`/`CompletedAt` make their own datetime type — a frame holds no type list (call memory locked).
- input's numbers are `number.@this.Create(x)!` until number is born whole; then `Create(x, types[...])`, the `!` goes.
- choice values (mouse Action/Button, navigate To) are made on read; at choice's turn they're held from birth.

## Blocked (asked of the architect)
- **Kinded types** (base64, binary, image, duration, text, path): the app's kinded type needs a context
  (`types[new type(name, kind), context]`), but `Parse(raw)` and item's locked `Kinded(kind)` have none.
- **status**: group A (error status codes), with Ingi.
- **null**: `@null.Instance` static + 75 makers — stay typeless like the root, or per app?
- **bool**: `True`/`False` statics, tied to the C# → plang operator (42 scope).

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
The architect's answers on the blocked list; then on in the order (binary, image, duration, choice, bool, path,
dict, text, number; then setting, content, source, separator); last, number's static kinds and `KindOfClass` go.
