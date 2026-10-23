# coder — item-doors — v6 — the door, by double dispatch (173)

Nothing built. This replaces 71eee3396 (172's direction, a look as a kind, is reversed). 168–171 stand.

## The shape

```csharp
// item.@this
public ValueTask Output(IWriter writer) => writer.Write(this);                       // NEW: public, non-virtual
protected internal virtual ValueTask Write(IWriter writer)                           // NEW: the value's own write, every format (today's 89 Output bodies)
protected internal virtual ValueTask Write(global::app.type.item.text.Writer writer) => Write((IWriter)writer);              // NEW
protected internal virtual ValueTask Write(global::app.goal.step.action.formal.Writer writer) => Write((IWriter)writer);     // NEW

// IWriter
ValueTask Write(global::app.type.item.@this value);                                    // NEW
global::app.View View { get; }                                                        // NEW (168)
global::app.actor.context.@this Context { get; }                                      // NEW (170), never null

// each writer: C# picks the overload for its own class
public ValueTask Write(global::app.type.item.@this value) => value.Write(this);
```
- The `(IWriter)writer` in each default is an upcast (it always succeeds) that picks the general overload, not a
  conversion. So the write path has no cast, no lookup, no if and no switch.

## The writers

- **Production:** text (`type/item/text/writer.cs`), json (`type/item/kind/json/writer.cs`), plang
  (`type/item/wire/kind/plang/writer.cs`) and formal (`goal/step/action/formal/writer.cs`). No module ships one.
- **Tests:** five capture writers (ReaderRegistryShapeTests, TypeProviderDllRoundtripTests, NumberSerializerTests,
  CodeSerializerTests, ImageSerializerTests, plus Plng006's test, which goes); each implements
  `Write(item) => value.Write(this)`.
- **Which get a virtual on item:** only the two where some value looks different, text and formal.
  - json's and plang's look is every value's general write (a type's identity is its general write), so a json or
    plang virtual would have no override. OPEN 1.

## Each look, as an override in the value's own class

| look | where | override |
|---|---|---|
| a type in text: its word (`list<path>`) | type/this.cs | `Write(text.Writer w) { w.String(kind.IsEmpty ? Name : $"{Name}<{kind.Name}>"); }` |
| a type in json and plang: its identity | type/this.cs | the general `Write(IWriter)`; today's `internal Write(writer)` (identity) is it |
| an action in formal: its call | action/this.Item.cs | `Write(formal.Writer w)`: BeginCall, rows, EndCall, body; typed, no cast |
| an action list in formal: `a; b` | action/list/this.cs | `Write(formal.Writer w)`: BeginActions, each `action.Output(w)`, EndActions |
| a kind written as its name | kind/this.cs | `Write(text.Writer)` and `Write(IWriter)`: its name. By C# class, so it needs nothing from stage 1 |

- A list of Data in formal (171) has no look: the list writes `[`, its elements, `]`, and each Data writes its record
  through formal's NEW BeginRecord/EndRecord (`{name: type = …}`).

## Inheritance: what each subclass inherits

- **kind : type** inherits type's text look (its word) and type's identity, but should write its name. Its own two
  overrides fix it (above). One catch: type's identity writes a nested kind through `kind.Write(writer)`
  (type/this.cs:47, `{list, kind: {list, kind: path}}`). Once kind overrides the general write with its name, that
  nesting would lose its structure. Fix: the identity is type's own member, not a look: NEW
  `private protected Identity(writer)` (today's internal `Write`). type's general write calls it, and :47 calls
  `kind.Identity(writer)`.
- **The kind classes under kind** (format kinds, precision kinds, setting kinds, list's element kinds) inherit kind's
  name. Right.
- **Actions** (clause, loop, keep : action) inherit the action's call. Right: a clause is written as the action it is.
- **List subclasses** (list<T>, action.list, step.list, the variable code's list<Hop>) inherit nothing from list in
  formal, because list's formal look goes (171). action.list has its own; step.list writes as a list.
- Nothing else inherits a look it shouldn't.

## Notations: a kind with its own text becomes a subclass of its type

- **duration (3: short, iso, dotnet):**
  - duration (sealed partial today) is unsealed and declares `[Kinds]`. `duration.short`, `duration.iso` and
    `duration.dotnet` (NEW classes, replacing the three kind classes) each hold the span and write and parse
    themselves: `Write(IWriter w) => w.String(<its text>)` and its own Create from a text.
  - The subclass kind (`kind/subclass/this.cs`) makes each one a kind of duration by its class.
  - This goes: `duration/this.cs:33` (`Type.kind.Write(this, w)`), `duration/kind/this.cs` with its `is … else` (:19–22),
    and the `Text`/`Span` pair.
  - Reading a text into the right standard: each subclass's own Create answers or declines (the forms don't overlap,
    as today). The subclass kind's Parse asks its class's Create.
- **size (2: iec, si):** the same opened box (`size/this.cs:53`, `_kind.Text(Value)`). The same shape: size.iec and
  size.si as subclasses.
- **number (16 precisions):** the same shape holds, at a cost.
  - number is `sealed partial` and its arithmetic climbs precisions by kind (`Ladder.cs`, 24 uses;
    `Arithmetic.cs:46` takes `Type.Family.kind[made.Kind.Name]`). As 16 subclasses, every arithmetic result, implicit
    operator and Create must make the right class.
  - A precision kind's Write is a CLR mapping, not a text (short → `w.Int`, ulong → `w.Long`, Int128 →
    `w.String`), so number writing itself would need each precision's class.
  - The cheaper shape: the precision kinds keep their ladder (Create, Read) and stop writing, and number writes its own
    backing with no switch only if each precision is its class. OPEN 2.
- With these, `kind.Write(value, writer)` and its 17 overrides go, and the name clash with it.

## formal's kind

The ownership catch dissolves: no lookup reads `writer.Kind`, so it doesn't matter which type owns formal's kind.
formal keeps `[Format("formal")]` (its encode and decode); it needs no kind class of its own.

## Views (168, 169) and context (170): where they are read

- **`writer.View`** is read in three places:
  1. Data.Output (the courier): `writer.View.Write(data, writer)`. Store writes as written; Out what it resolves to;
     Debug both, or `not set`.
  2. The general write's tag filter (reflection's Tagged): `writer.View.Shows / Masks / Declares`, and an undeclared
     type through `writer.View.Undeclared`.
  3. The plang writer's own record (`_view`, as today).
- No value reads it: app, goal, step, identity and the property row become tags; source, text and template become
  kept/used; type's face becomes the text look.
- **`writer.Context`** is read by a value that needs its asker to write:
  - dict and list `Items(writer.Context)`
  - text's and source's render, now asked through the view's "used"
  - prose's `Text(context)`
  - node's `Of(context)`
  - app's asker memory
  - a variable's resolve

  The context-null checks go: source:364, text:134 and :235, wire:65, template:28, prose:69, node:63, pair.list:33.
  app.cs:43's `?? system` moves to where the writer is made.

## Locked surface for Ingi

- item:
  - `Output` from `public virtual (writer, mode, context?)` to `public Output(writer)`, non-virtual.
  - NEW `protected internal virtual Write(IWriter)`, `Write(text.Writer)` and `Write(formal.Writer)`.
  - The 89 `Output` overrides move to `Write(IWriter)` (+6 in tests); 4 overrides move to the text and formal virtuals
    (type, action, action list, kind).
- IWriter: NEW `Write(item)`, `View` and `Context`. `IOutput` goes.
- kind: `Write(value, writer)` goes (17 overrides).
- duration and size: unsealed, their standards become subclasses. number: OPEN 2.
- View: from an enum to objects.
- Data.Output loses mode and context.
- Call sites:
  - the 107 door calls and 34 direct calls become `member.Output(writer)`
  - the 21 `new …Writer(` sites take their view and asker
  - PLNG006 and its tests go

## Tests that pin today

- `.pr` byte-equal: PrByteEqualTests, ActionPropertyPrTests.
- Pages: ModulePageTests, TypePageTests.
- formal: FormalWriterTests and FormalReaderTests (formal_golden.json), FormalKindTests, and the decider fixtures
  (PickListTests, ConfirmTemplateTests, LineTwinTests, PickOptionTests).
- Views: DebugViewTests (169 moves `%missing%` to `not set`), Cut2_DebugModeTests.
- A type's face: CollectedTypeTests (168: a type in json becomes `{"name": …}`).
- duration and size: DurationWrapperTests and the size/duration setting tests (TheSetting_NamesACountsStandard,
  Received_PrintsInTheSizeSettingsStandard).
- Masking: SensitivePropertyFilterTests, MaskedAttributeTests, IdentityHandlerTests, TheKey_IsNeverShown.
- The surface golden.

## OPEN for Ingi

1. A virtual for each core writer (text, json, plang, formal), or only the two a value looks different in (text and
   formal). A json or plang one would have no override today.
2. number's 16 precisions as subclasses: the same shape as duration, but number's arithmetic ladder would make the
   right class on every result. Or the precisions stay kinds for the ladder and stop writing (then number's own write
   needs its precision's class to choose a writer primitive without a switch).
3. A module's own writer gets only the general write. Its writer's `Write(item)` can't reach item's protected
   internal `Write` from another assembly, so item needs a public door to the general write for it.
