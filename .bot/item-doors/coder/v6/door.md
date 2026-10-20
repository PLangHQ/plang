# coder — item-doors — v6 — the door, reshaped with 168–172

Nothing built. The first trace is 71c2eecb2; this replaces it.

## The answer to 172: yes, a type's look in a format is its kind for that format

What's already there:
- **Kinds are found by class:** the registry makes every `kind<T>` class anywhere in the assembly, owned by T's entry
  (`Registry.Enlist`, Registry.cs:302–305). No namespace and no string.
- **Kinds already write values:**
  - `kind.Write(value, writer)` has 17 overrides: number's 16 precisions and duration's kind.
  - number and duration call `Type.kind.Write(this, w)` from their Output (number/this.cs:54, duration/this.cs:33): the
    value's own kind writes it.
  - kind's second value door, `Output(object host, writer, mode, ctx)`, is overridden by the kind base, dict, list and
    reflection; it writes a C# host value of that kind.
- **Formats are kinds:**
  - json is a kind of item, plang a kind of wire, and formal a kind of `{list, action}` (`[Format("formal")]` on
    action.list, action/list/this.cs:18).
  - The text writer's Kind is text's empty kind (text/writer.cs:37).
  - A probe showed that action.list is `{list, action}` and step.list `{list, step}` (family list); `list<path>` is
    `{list, path}`; and the kind class has no type entry of its own.

## The shape

```csharp
// item.@this
public ValueTask Output(IWriter writer)                                   // NEW: public, non-virtual, the jump
    => Type.Look[writer.Kind].Output(this, writer);
protected internal virtual ValueTask Write(IWriter writer)                // NEW: the value's own write (today's 89 Output bodies, without mode and context)

// type.@this
internal look.list.@this Look { get; }                                     // NEW: born with the type (name: OPEN 4)
// one table per type entry, made where kinds are made: the family's looks, then the type's own over them.
// Look[format] is one lookup; a miss answers the type's own kind (Type.kind). No ?? chain:
// the family's looks are already in the table.

// kind.@this
public virtual ValueTask Output(item value, IWriter writer)               // CHANGED: was Write(item value, writer) and Output(object host, …), now one door
    => value.Write(writer);                                                // the empty kind: the value's own write
// number's 16 precisions and duration's kind override it, as they override Write today: a {number, short} misses every
// format and its own kind (short) writes it, so number's Output override goes

// a look: a kind of its type written in one format
public abstract class kind<TValue, TWriter> : kind<TValue>                 // NEW: alongside kind<T>
{
    protected abstract System.Type Format { get; }                          // the format, named by a class (below)
    public sealed override ValueTask Output(item value, IWriter writer) => Output((TValue)value, (TWriter)writer);
    protected abstract ValueTask Output(TValue value, TWriter writer);
}

// IWriter
global::app.View View { get; }                                             // NEW (170)
global::app.actor.context.@this Context { get; }                           // NEW (170), never null
// kind (a format): makes its own writer
public virtual IWriter Writer(System.IO.Stream stream, View view, context.@this context)   // NEW: the 21 `new …Writer(` sites become this
```
- **A look names its format by a class**, resolved once to the app's kind object where the look is made: a format kind's
  own class (`wire.kind.plang.@this`, `item.kind.json.@this`), or a type's class for that type's own format
  (`text.@this` → text's empty kind). No string. formal needs a kind class of its own for this. Today it is minted
  from `[Format("formal")]` like md and xml, which share one generic class; the NEW class is
  `goal/step/action/list/kind/formal` (replacing the attribute).
- **The one cast** is the base `kind<TValue, TWriter>.Output`, and it converts both arguments:
  - The value: the table belongs to the value's own type entry, whose class makes the value, so the value is a TValue
    (`{list, action}`'s look for an action.list, a `list<action>`).
  - The writer: the only writer whose Kind is format F is the one F makes (`kind.Writer`), so it is F's TWriter.
- **Where each look is found:** the lookup runs among the value's family's kinds and its own, by the writer's Kind
  object, never a name. `{number, short}` asks its table, which holds number's looks; `{list, action}`'s holds list's,
  then its own.

## The four looks as kinds

| look | as a kind | reads its format back? |
|---|---|---|
| a type in text: the word (`list<path>`) | `kind<type, text.Writer>` naming text | yes: the type list's name door (`app.type.list["list<path>"]` reads the word) |
| a type in plang and in json: the identity | `kind<type, IWriter>`, one per format (plang, json), from type/format/plang.cs | yes: the type reader (`type.Read`, type/this.cs:439) |
| an action in formal: its call | `kind<action, formal.Writer>`, from action/format/formal.cs | the formal reader reads an action today inside a step's line; this kind can hold it |
| an action list in formal: `a; b` | the NEW formal kind of `{list, action}` itself, both the format and the look | yes: the formal reader is its read |

- 168 makes it the format that decides a type's face, so `text` vs `{"name":"text"}` is two looks. A visible change: a
  type in a json response becomes `{"name": "text"}`.
- **A kind written as its name** (a kind held as a value) goes with kind's plang form. That waits on stage 1: a kind
  value has no type entry of its own today, and its `.Type` is rebuilt from its C# class (item/this.cs:390), which
  would find type's looks. Marked, not solved.

## 171: a list of Data in formal is a list of records

- list's formal form (`type/item/list/format/formal.cs`) and its `if` go. A list writes as a list: `[`, each element, `]`.
- An element that is a Data writes its record through formal's record door: NEW `BeginRecord` and `EndRecord` on the
  formal writer (formal/writer.cs has neither today, so a Data writes its value alone). BeginRecord writes `{` and the
  row (`name: type = `, its existing `Row`); EndRecord writes `}`. So
  `Parameter: list = [{to: item = %user.email%}, {subject: text = "Hi"}]`.
- **The formal reader** (formal/reader.cs:359, `Arguments` :432): a list-typed property reads `[{name: type = value}, …]`,
  each element a typed record becoming a Data. The `{name: type = value, …}` form goes. The authored
  `Parameter={name: "value"}` (reader :503; in 5 os .goal files, 10 lines, among them BuildGoal/Start.goal) is OPEN 2.
- **What moves:**
  - teaching: `os/system/modules/goal/call.notes.md`, `ui/render.notes.md`, `ui/render.examples.md`, and
    `os/system/builder/llm/Properties.llm`
  - the formal fixtures: `formal_golden.json` (FormalWriterTests, FormalReaderTests)
  - the decider fixtures and page goldens, re-pinned by the teaching-edit rule
  - the os .goal files that build to `.pr` (rebuilt, never hand-edited)

## The 22 view checks, each as tag, kept/used or format (168)

| check | is | becomes |
|---|---|---|
| app.cs:37 Store → [Store] record | tag | the view shows its tagged members |
| app.cs:69 Debug → the memory | tag | `[Debug]` on the memory member |
| property:228 Store → the row's variable list | tag | a `[Store]` member of the row |
| identity:61, :65 | tag | `[Store]` members; PrivateKey through `writer.Sensitive` |
| goal.Item:130, step.Item:75 Debug → reflection | tag | Debug shows the `[Debug]` members; the custom shape is the members tagged for Out and Store |
| Tagged.cs:66, :118, :141 | tag | the view object: `Declares`, `Shows`, `Masks` |
| reflection:317 undeclared type | tag | the view object: Debug writes its name; Out and Store refuse |
| data.Output:61 a variable resolves unless Store | kept/used | the view, once (below) |
| source:364, text:235 Store → raw, else render | kept/used | the view, once |
| property:224 a template as written in Debug | kept/used | 169: Debug writes both |
| wire/template.cs:23 Store → slice | kept/used | the view, once (and 158) |
| type.cs:62 face vs identity | format | two looks (text vs plang/json) |
| plang writer:37, :54 | kept/used | the view, once |
| plang writer:77 Sensitive | tag | the view's Masks |
| plang kind:42 (hash in the Store view), :93 (Out refuses unsigned) | the format's encode and decode | stays in the format: it is the format's rule, not a value's |

**Kept or used, decided once (168, 169).** The one place is Data.Output, the courier, where a value holding variables
is met: `writer.View.Write(data, writer)` (NEW on View).
- Store writes it as written.
- Out writes what it resolves to.
- Debug writes it as written, then `→` the value it resolves to with its type, or `not set`
  (`Value = %user% → {"name": "Ada"} (dict)`).

A value holding variables writes itself as written; using it is `data.Value()`. So no value knows the view: text,
source, wire and template lose their branches. A property row's value and a dict's entries are Data, so both pass the
courier.

## 170: the context-null checks that go (the writer holds its asker, never null)

- source.cs:364, text/this.cs:134 and :235: the "no asker to render with" branch.
- wire/this.cs:65 and wire/template.cs:28: they throw when there's no context.
- prose/this.cs:69: `context == null ? ""`.
- node/this.cs:63: `context != null ? Of(context) : list`.
- kind/pair/list/this.cs:33 goes with pair.
- app.cs:43's `context ?? system` moves to where the writer is made.
- Stays: wire/this.cs:75, the lowering path (Clr), not a write. It is F7's interim refusal until the walk opens a wire.

## What goes

- `kind.pair`, pair.list (its Find scan and class walk), `IOutput`, and the 107 door calls: back to `member.Output(writer)`.
- PLNG006 and its tests; kind's undo form; list's formal form and its if; the formal casts (one checked conversion in
  the base).
- `mode` and `context` from 89 overrides, 34 direct calls and Data.Output.

## No if, switch or null on the write path, except

- `Look[format]`'s miss answers `Type.kind`: a dictionary's default, a selection, one place.
- The view's own decisions (Store/Out/Debug as objects, each answering for itself): no switch.

## Locked surface for Ingi

- `item.Output`: from `public virtual (writer, mode, context?)` to `public Output(writer)`, non-virtual, the jump.
- NEW `protected internal virtual Write(writer)` on item; the 89 overrides move to it (+6 in tests).
- kind:
  - `Write(item value, writer)` and `Output(object host, writer, mode, ctx)` become one `Output(item value, writer)`:
    17 + 4 overrides.
  - NEW `kind<TValue, TWriter>`; NEW `Writer(stream, view, context)` on a format kind; `pair` goes.
  - NEW formal kind class (replaces `[Format("formal")]`).
- type: NEW `Look`.
- IWriter: NEW `View` and `Context`. `IOutput` goes.
- View: from an enum to objects (`Shows`, `Masks`, `Declares`, `Undeclared`, `Write`).
- Data.Output loses mode and context.

## Tests that pin today

- `.pr` byte-equal: `PrByteEqualTests`, `ActionPropertyPrTests`.
- Pages: `ModulePageTests`, `TypePageTests`.
- formal: `FormalWriterTests` / `FormalReaderTests` (formal_golden.json), `FormalKindTests`, the decider fixtures
  (`PickListTests`, `ConfirmTemplateTests`, `LineTwinTests`, `PickOptionTests`).
- Views: `DebugViewTests` (it moves with 169: `%missing%` becomes `not set`), `Cut2_DebugModeTests`.
- The face: `CollectedTypeTests` (a type in json moves to `{"name": …}`).
- Masking: `SensitivePropertyFilterTests`, `MaskedAttributeTests`, `IdentityHandlerTests`, `TheKey_IsNeverShown`.
- The surface golden.

## OPEN for Ingi

1. Naming 172's clash (a kind is an item). My lean: the value's own write is item's `Write(writer)`, and a kind writing
   a value of it is `kind.Output(value, writer)`, folding kind's two value doors into one. The other reading keeps
   `kind.Write(value, writer)` beside item's `Write(writer)` as one verb with two objects (itself, or a value).
2. The authored `Parameter={name: "value"}` in .goal formal (5 os goals): keep reading it as input, or rewrite those
   goals to `[{name: type = value}]`, so formal has one argument form.
3. formal becomes a kind class of its own (needed so a look names its format by class, not by the string "formal").
4. The table's name on type: `Look` is my placeholder for "the type's kinds by format". It could also be read as the
   kind list keyed by format, with no word of its own.
