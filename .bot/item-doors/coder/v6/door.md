# coder — item-doors — v6 — the door and the pairs (167): the trace

Nothing built. Row 167 read.

## Today, as traced

- `writer.Kind.pair` (kind/pair/list/this.cs) holds one output kind's looks, keyed by C# class. On the first write it scans every
  class of every assembly for an `IOutput` named for the kind in a `.format` namespace (`Find`, :45–63). The lookup
  walks the value's C# base classes (:37–38), and falls back to `value.Output`.
- 107 `pair.Output(…)` calls in 53 production files (41 in tests); 89 `Output` overrides (6 in tests); 34 direct
  `x.Output(writer, mode, …)` calls; PLNG006 forbids a direct one.
- The five looks (`IOutput`):
  - `type/format/plang.cs`: a type in plang is its identity, `((type)value).Write(writer)`
  - `type/kind/format/plang.cs`: a kind in plang is its name, `value.Output(...)`, an undo of the type one, because kind
    derives from type in C#
  - `goal/step/action/format/formal.cs`: an action's call (casts the writer to formal's)
  - `goal/step/action/list/format/formal.cs`: a step's actions on one line (casts)
  - `type/item/list/format/formal.cs`: a list of named Data is a list of arguments, else its elements (casts; an
    `if` on the list's shape)
- A type's face (`type/this.cs:62`): `mode != Out → identity, else its word (text, list<path>)`.
- Plang's own type relation is the fact chain `type → kind → Family` (`Values => Own ?? kind.Own ?? Family._x`,
  type/this.cs:657–). A probe (not committed) showed:
  - `action.list` is `{list, action}` and `step.list` is `{list, step}`; each has family `list` and an element kind,
    and its ClrType is `list<T>`, not the sealed subclass. `list<path>` is `{list, path}`.
  - The `kind` class has no type entry of its own (it falls to `clr`). A type or kind value is born with no type: its
    `.Type` is rebuilt from its C# class (`FamilyOf`, item/this.cs:390) — family (1), Ingi's.

## The shape

The jump: value → its type → that type's form for the writer's kind → it writes. No door, no scan, no base walk.

```csharp
// item.@this
public ValueTask Output(IWriter writer) => Type.Form(writer.Kind).Output(this, writer);        // NEW: non-virtual, the jump
protected internal virtual ValueTask Write(IWriter writer)                                      // NEW: the value's own write, for every format with no form
    // (today's 89 Output bodies move here, minus `mode`; base = the reflection kind's tagged bag, as today)

// type.@this
internal form.IForm Form(kind.@this kind)                                                       // NEW: own ?? kind's ?? Family's ?? itself
    => _form.GetOrAdd(kind, k => Own(k) ?? this.kind.Form(k, of: this) ?? Family.Form(k) ?? Itself);
// Own(k): the class `<type's namespace>.format.<k.Name>` in the type's assembly — one GetType, no scan,
// read the first time this type writes in k and kept on the type.
// Itself: the form that is the value's own Write.

// IWriter
View View { get; }                                                                               // NEW: made with its view
```
- **A form**, which is what `IOutput` is: "the form a type takes in one format". The name `pair` goes.
  ```csharp
  public interface IForm { ValueTask Output(item value, IWriter writer); }                       // was IOutput, minus mode
  public abstract class form<TValue, TWriter> : IForm where TValue : item where TWriter : IWriter  // NEW
  {
      ValueTask IForm.Output(item value, IWriter writer) => Output((TValue)value, (TWriter)writer);
      protected abstract ValueTask Output(TValue value, TWriter writer);
  }
  ```
  Formal's three forms derive from `form<action, formal.Writer>` and so on: each gets formal's writer typed (BeginCall,
  BeginRows) with no cast in the form. The one conversion is in the base. It can't fail, because a type keys its
  form by its own class and the kind whose writer it names.
- **A kinded type** (`{list, action}`, `{text, md}`) gets its form by the fact chain: its own form, else its kind's form
  for a type of it (`kind.Form(k, of: type)`), else its Family's. The element kind of `{list, action}` asks
  action for how a list of actions looks: `goal/step/action/list/format/formal.cs` stays where it is, read as "a list of
  actions in formal". `{list, path}` and every other list fall through to list's form (`type/item/list/format/formal.cs`).
  There is no C# class walk: `step.list`'s look comes from `{list, step}`, its plang type.
- **kind**'s plang form goes. A kind written as a value writes itself (its name) because no form is found for it.
  That holds only when a kind is born with a type entry of its own, not `type`'s: today its `.Type` is rebuilt from its
  C# class, which leads to type's form. Gated on family (1).
- **Views**: `mode` leaves every parameter list. The writer is made with its view: the plang writer already holds
  `_view`, json/text/formal take one where they are made (the 11 fixed-view calls make their writer there). View
  becomes objects (Store, Out, Debug), each answering what it shows, so no value switches on it.

## The view checks: 22 (17 in values, 5 in plang's writer and format)

| check | serves | place in the shape |
|---|---|---|
| app.cs:37 `Store → base` | the app at rest is its [Store] record | the app's form for (any kind, Store) is the default (Itself → its tagged bag); its object write is its Write for Out/Debug. **Form keyed by view** |
| app.cs:69 `Debug → + variable` | the asker's memory shows only in a dump | a member tagged `[Debug]`, written through the tag filter. **View shows its tags** |
| data.Output:61 `!Store → a variable resolves` | a .pr keeps `%ref%`; the wire carries the value | variable's form for Store writes the reference; its Write (Out/Debug) the value it names. **Form keyed by view**, on variable |
| goal.Item:130 `Debug → base` | a dump shows the reflection (with Errors/Warnings) | goal's object write is its form for Out and Store; Debug falls to Itself (reflection). **Form keyed by view** |
| step.Item:75 `Debug → base` | the same for a step | as goal |
| property:224 `Debug & has variable → Store` | a dump shows a template as written | moves to the template value: text's and source's form for Debug is the as-written one (rows 10, 11). The property writes `Value` plainly |
| property:228 `Store → + variable list` | a .pr row lists its variables | a member shown in Store. **View shows its tags** (the row's `variable` as a [Store]-only member) |
| type.cs:62 `!Out → identity, Out → face` | `%!app.type.text%` reads "text"; a .pr holds the identity | type's form for (plang, any) = identity; type's form for (any, Out) = its face; Itself = identity. **Form keyed by view** |
| wire/template.cs:23 `Store → slice` | the .pr keeps the authored slice | dissolves with 158 (the template's two branches become one). Untouched here |
| source:364 `Store or no ctx → raw` | the authored raw at rest; a template renders on the wire | the template source's form for Store/Debug = raw, Itself (Out) = render. **Form keyed by view**. "No context" stays an if in Write (a value can't render with no asker) — **if kept, why: not a view** |
| text:235 same | the same for text | as source |
| Tagged.cs:66 `!Debug → [Debug] members not declared` | what a type declares per view | `writer.View.Declares(type)`. **View object** |
| Tagged.cs:118 `!Out & JsonIgnore` | cycle-protection outside Out | `writer.View.Shows(property)`. **View object** |
| Tagged.cs:141 `!Store → masked` | Masked/Sensitive hidden except at rest | `writer.View.Masks(property)`. **View object** |
| reflection:317 `Debug → name, else throw` | a dump never throws; the wire refuses an undeclared type | `writer.View.Undeclared(obj, writer)`: Debug writes its name; Out/Store refuse (NoWireContract). **View object** |
| identity:61, :65 `Store/Debug → private members; Store → real key` | the key at rest only | `PrivateKey` through `writer.Sensitive` (it exists: `****`, and plang's Store writer keeps it whole); the Store-only members tagged `[Store]`. **View shows its tags** |
| plang writer:37, :54, :77 | the writer's own record and masking | the writer's own view, which the writer owns. Stays, inside the writer |
| plang kind:42, :93 | sign in Store; refuse unsigned in Out | the format's encode and decode, not a value. Stays |

So every value check becomes one of two things: a form selected by (kind, view), or a member the view shows by its
tag. The only `if` left in a value is source's and text's "no asker to render with", which is not a view.

## What goes

- `writer.Kind.pair` and `kind.pair`, `pair.list` with its Find scan and class walk, and the 107 door calls: back to
  `member.Output(writer)`.
- PLNG006 and its tests: calling `Output` *is* the door.
- kind's undo form; the formal casts; `mode` from 89 overrides, 34 direct calls and IOutput.

## Locked surface for Ingi

- `item.Output`: from `public virtual (writer, mode, context?)` to `public (writer)`, non-virtual; its body is the
  jump.
- NEW `protected internal virtual Write(writer)` on item. The 89 overrides move to it (6 in tests). Where `context`
  goes: the writer or the value (see OPEN 3).
- `IWriter`: NEW `View`. `IOutput` becomes `IForm` (minus mode), plus NEW `form<TValue, TWriter>`.
- `type`: NEW `Form(kind)`. `kind`: NEW `Form(kind, of: type)`; `pair` goes.
- `View`: from an enum to objects with `Declares` / `Shows` / `Masks` / `Undeclared`.
- `Data.Output(writer, mode, context, layer)`: mode leaves it too.

## Tests that pin today

- `.pr` byte-equal:
  - `PrByteEqualTests`
  - `ActionPropertyPrTests.WrittenPr_ReadsBackAndWritesAgain_ByteIdentical`
- The page goldens: `ModulePageTests`, `TypePageTests`.
- The formal fixtures: `PickListTests`, `ConfirmTemplateTests`, `LineTwinTests`, `PickOptionTests`, `FormalWriterTests`,
  `FormalReaderTests`, `FormalKindTests`.
- Views:
  - `DebugViewTests`: a goal dump, a parameter as written, a sensitive value masked, a binding dump is its name
  - `Cut2_DebugModeTests`
  - `CollectedTypeTests`: a type's face
- Sensitive and masking: `SensitivePropertyFilterTests`, `MaskedAttributeTests`, `IdentityHandlerTests`,
  `TheKey_IsNeverShown`.
- The surface golden.

## OPEN for Ingi

1. A form keyed by view as well as kind (`<type>/format/<kind>.cs` serving declared views, or a view folder). Or
   values keep the six view-selected checks, reading `writer.View`. The first leaves no if; the second is smaller.
2. View as objects instead of an enum (Store/Out/Debug answer `Shows`, `Masks`, `Undeclared`).
3. `context` on Output: today it rides beside `mode`. It is the asker's, not the writer's format; it could stay a
   parameter, or the writer could be made with its asker as it is made with its view.
4. A kind's own plang type (family 1): the kind look goes only when a kind is born holding its own entry.
5. The list-of-arguments look keeps an `if` on the list's shape (every element named). Either goal.call's
   `Parameter` is its own `{list, argument}` type with its own form, or the if stays inside list's formal form.
6. The name: `IForm` / `form` for "the form a type takes in one format"; `Write` for the value's own write.
