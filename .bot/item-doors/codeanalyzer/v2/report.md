# item-doors — the door reshape, independent OBP re-check (decision 184)

Scope: `git diff 6ff57ceb4^ 12fa4b1bd -- PLang PlangConsole PLang.Generators` (172 production files). Read only; I did not build or run the tests, and I did not read the auditor's report. Log rows 167–183 are claims; each was checked against the code below.

## Plain summary

**The reshape is the right shape and the structure holds.** What my first audit called F5 (the pairs, the door, PLNG006) is gone at the root, not patched: `rg` finds no `.pair`, `IOutput`, `WriteReflected`, `OutputTagged` or `Plng006` left in `PLang`, `PLang.Generators`, `PlangConsole` or `PLang.Tests`. A value writes itself (`item.Output(writer) => writer.Write(this)`, `item/this.cs`), the writer hands it back through double dispatch, the view is a constant the writer holds, and `app.Output` is now `[Out]` tags plus the re-entry guard (`app/this.cs:36`), so F10 is closed too. Every "no value reads the view" claim, however, is not true yet, and the reshape left six places where one idea is decided in several files.

**Verdict: FAIL for the coder, but the shape stays.** Nothing here asks to undo the reshape. Seven findings are "fix at the root"; four "hold" (with a note).

| # | Finding | Verdict |
|---|---|---|
| R1 | The view is still asked by a value (`wire/template.cs:21`) and by the plang writer (`writer.cs:39`, `:74`) with `== View.Store` | fix at the root |
| R2 | "Kept or used" is decided at four holders (Data, property row, plang writer, Store.Own), through two doors, with a throwaway Data | fix at the root |
| R3 | Two mechanisms show a Debug member (tags in `View.Shows`, the `Own` hook), and `Own` does two jobs under one word | fix at the root (lower) |
| R4 | `kind` undoes `type`'s text look (the v1 "undo pair", moved to an override) | fix at the root |
| R5 | size and duration standards: by-name entry lookup is back; the `as <standard>` fork is copied; iec/si are one class twice | fix at the root |
| R6 | One definition of "tag-aware" in two places; the tag rules now sit in three files | fix at the root |
| R7 | The 11-case CLR-number switch is copied into three writers | fix at the root |
| R8 | The `View? view = null` + `?? View.X` default is re-decided at five sites, two of them disagree | fix at the root |
| H1 | `item` names two core writers (`text.Writer`, `formal.Writer`); the general write branches on assembly and on `Data` | holds |
| H2 | Formal's record door and its parallel `_records` stack | holds, with notes |
| H3 | Out/Debug build a throwaway Data; Debug swallows the error | holds, with notes |
| H4 | Text/source no longer render themselves (behaviour change) | holds if every holder is a Data; one to verify |

## Claims 167–183 checked

| Claim | In code |
|---|---|
| 178: pairs, `kind.pair`, `IOutput`, the 107 door calls, PLNG006 gone | **True.** Zero hits; `Plng006.cs` deleted. |
| 178: `item.Output` non-virtual; the bodies are `Write(IWriter)` | **True** (`item/this.cs:842` non-virtual; `:854` the virtual). |
| 176/177: "values don't ask the view" | **Partly false.** `wire/template.cs:21` branches on `writer.View == View.Store` inside a value, and its comment says so ("where the face is chosen is the writer-rule sweep's"). The plang writer does it twice. goal, step and property call the view's `Own`/`Write`. See R1–R3. |
| 176: context-null checks gone | **True** for the listed sites. |
| 179: formal's record door is "the writer's layout, no value check" | **Mostly.** `formal/writer.cs:241` reads `record.Name` (`!string.IsNullOrEmpty`) to decide. A value fact, though a small one. H2. |
| 180: `Host`; `kind.Output(object, writer)`, `WriteReflected`, `OutputTagged` gone | **True.** `Host` is `private protected virtual` (`item/this.cs:885`), clr overrides it (`clr/this.cs:183`). |
| 181: `[Choice]` on abstract size; `setting.Standard` a `choice<kind<size>>` | **True** (`size/setting/this.cs:15`). |
| 181/183: "a standard makes its own size, not through the by-name lookup both audits flagged" | **False after 12fa4b1bd.** `size/this.cs:38–39` and `duration/this.cs:47,62` look the entry up by `new type("size", standard.Name)` again. R5. |
| 183: duration/size iec/si/short/iso/dotnet are `[Kinds]` subclasses that write and parse themselves | **True.** But five classes repeat the same text (R5). |
| 183: "a kind writes nothing" | **True of `Write(value, writer)`** (gone). A kind still has to undo type's text look (R4). |

## Findings

### R1 — the view is asked from inside values and the format's writer (fix at the root)

- **Symptom:** `wire/template.cs:21` `if (writer.View == View.Store) { await base.Write(writer); return; }`. `wire/kind/plang/writer.cs:39` writes a Data's `name` only `if (View == View.Store)`; `:74` `Sensitive` prints the real characters only `if (View == View.Store)`. Decision 168 says forms are keyed by format only and a view is asked by tags.
- **Why-chain:**
  1. `template.Write` keeps what was authored in Store (`wire/template.cs:21`). →
  2. A template is a `wire` subclass; its sibling `wire.Write` forks on `_reader.Owns(writer)` (`wire/this.cs:62`), the verbatim/decoded fork (decision 158, held, still not built). →
  3. 158 is held, so `template` has to decide "kept or used" itself, the very decision `View.Write(Data)` now owns for every other holder. The comment on the line names the debt.
  4. For the plang writer: the Store view alone keeps a name and an unmasked secret; those are the view's facts (`View.Masks` already answers the masked half for tags) but the writer branches on the constant instead of asking.
- **Root-cause smell:** *Producer:* a view that answers `Shows`/`Masks` for tags but not for "keeps the name" and "keeps sensitive characters". *Why symptom-patching:* a special-case by literal constant (`== View.Store`), tell 4, a fork. *Fix:* `View` gets the two answers (`Store` keeps the name and the characters, `Out`/`Debug` don't), the writer asks `View.X(...)`, and `template` goes when 158's wire fix lands (it becomes a plain use). The two writer branches are fixable now, independent of 158.

### R2 — "kept or used" is decided in four holders, through two doors (fix at the root)

- **Symptom:** the question "does this value hold variables, so the view must write it" is asked at `data/this.Output.cs:55` (`Error == null && HasVariable`), `type/property/this.cs:223` (`Value is { HasVariable: true }`), `wire/kind/plang/writer.cs:55` (`record.Peek() is { HasVariable: true }`) and again inside `View.Store.Own` (`View.cs:111` `if (!value.HasVariable) return`).
- **Why-chain:**
  1. A text/source no longer renders itself in `Write` (`text/this.cs:229`, `source.cs:371`); the view does (`View.cs:129`, `:137`). →
  2. The view needs a holder to ask at: a Data (it has the record) or a property row (no Data around it). So two doors exist: `View.Write(Data, writer, layer)` (`View.cs:49`) and `View.Write(item, writer)` (`View.cs:58`). →
  3. Out and Debug reach the second by building a throwaway `new Data("", value, context: writer.Context)` just to call `Settle` on it (`View.cs:138`, `:194`). Making a Data to ask a Data's question is the tell that the door is on the wrong object. →
  4. Each holder (Data, property row, plang `EndRecord`) re-asks `HasVariable` before it calls in; the view's own `Own` asks it again.
- **Why this is a fork:** an `if` on what the value is, at the holders, instead of at the value's door. A new holder must remember to copy the check.
- **Root-level fix would look like:** the "holds variables" decision lives once, at the value's own door (the one `item.Output` already is), so Data, the property row and the plang writer just write the value and none re-asks; the throwaway Data in Out/Debug goes with it.

### R3 — two mechanisms put a Debug member in a dump, and `Own` does two jobs (fix at the root, lower)

- **Symptom:** a tagged type shows `[Debug]` members through `View.Shows` (`View.cs:155–157`); goal and step, whose forms are hand-made, show them through `writer.View.Own(this, writer)` (`goal/this.Item.cs:143`, `goal/step/this.Item.cs:98`). `Own` is also how Store writes a row's `variable` list (`View.cs:109–114`), called from `property:226` and the plang `EndRecord` (`:55`).
- **Why-chain:** goal/step are hand-made because tags can't say three things (`file` only for a root goal, empty `tag`/`warning` left out, a warning as `{key,message}`; row 177) → so they keep their own write and hand the view a hook → a second way to be shown in a dump → a new hand-made write that forgets `await writer.View.Own(...)` loses its diagnostics with no error and no test.
- **Naming:** `Own` is a verb whose meaning here is "add what only this view shows". `Own()` already exists elsewhere as "the list a type owns" (`debug/this.cs:126`, `on/error.cs:51`, `on/cache.cs:23`, …). Two meanings of one word in one codebase. For Ingi's naming round.
- **Root-level fix would look like:** make those three facts tag-expressible (the member projects what it shows), delete the hand-made goal/step writes and `Own`'s Debug half; Store's `variable` list becomes the same tag walk. If the architect keeps the hook, the verdict is "holds", but the name must change.

### R4 — kind undoes type's text look (fix at the root)

- **Symptom:** `kind/this.cs:308` `Write(text.Writer) => Write((IWriter)writer)` exists only to cancel what `type/this.cs:63` gives every type (`writer.String(Face)`).
- **Why-chain:** a kind IS-A type (`kind/this.cs` derives from `type.@this`) → it inherits type's text look and type's `Write(IWriter)` identity → in v1 this was the "undo pair" (`kind` needed a pair to cancel type's); decision 178 moved it to two overrides (`kind/this.cs:301`, `:308`) → the undo is the same smell with a different carrier, and a third `Write` overload added to item for formal would need a third undo.
- **Root-level fix would look like:** `Face` (`type/this.cs:71`, `internal string Face`) becomes virtual and `kind` overrides it to `Name`; `type.Write(text.Writer)` stays one line and `kind`'s text override goes.

### R5 — the size and duration standards (fix at the root)

Four things in one family, with the same producer.

- **(a) The by-name entry lookup is back.** `size/this.cs:38–39` (`Of`) builds `new type("size", standard.Name)` and looks it up in the app's type list; `duration/this.cs:47` hard-codes `new type("duration", "short")`, and `:62` builds `new type("duration", standard.Name)`. 181 says "not through `type.list[new type("size", standard.Name)]`, the by-name lookup both audits flagged"; 183's last commit (12fa4b1bd) put it back so "a standard's value holds the app's entry". *Chain:* the value must hold the app's entry (a test says so) → the kind in hand is not the entry → the code goes kind → name → new type → list lookup, a loop that ends where it started. `type/list/this.cs:136–137` already has exactly that call (`Of(kind, context)`: "the full type a held kind makes") and keeps it `private`. *Root:* make that one call reachable (a `type.list` addition: a **locked-core proposal**, not something I would add), delete the three string addresses and the `"short"` literal.
- **(b) The `as <standard>` conversion is copied.** `size/this.cs:65–68` and `duration/this.cs:60–62` are the same condition: `declared?.kind is { IsEmpty: false } asked && !string.Equals(asked.Name, built.Type.kind.Name, …) && list[family].kind[asked.Name] is kind<@this> standard`. A name comparison plus a type test, twice. Same shape, second site: tell 8/14.
- **(c) iec and si are one class twice.** `size/iec/this.cs` and `size/si/this.cs` differ only in the constants `Units` and `Step`. `Text => Form(Step, Units)`, `Write => writer.String(Text)` and the whole `Create` body are line-for-line the same; `size/this.cs:79` (`Bytes(text, step, units)`) and `:94` (`Form(step, units)`) take the subclass's constants as arguments instead of the subclass declaring them. The unwrap `(raw is item leaf ? leaf.Clr<object>() : raw)` is repeated in all five subclasses plus `size.Read` and `duration.Create`. *Root:* the base declares `Units` and `Step` as abstract members; `Text`, `Write` and the common `Create` live once. `Form`/`Bytes` are noun-named methods (the same shape the architect rejected in 180).
- **(d) duration's base names its child.** `duration/this.cs:47` and `:74` build `@short` (the default standard) by name; `short`'s extra constructor (`duration/short/this.cs`, `internal @this(System.TimeSpan value)`) builds its type from the C# class and carries a comment ("until the implicit-conversion branch removes that conversion") that narrates a branch (Pass 2). Architect noted this one (183, "noted for stage 1"); it is the same producer as (a).

### R6 — one rule, three homes: which members are "tagged" (fix at the root)

- **Symptom:** `View.Tagged(type)` (`View.cs:85–87`) and `Tagged.IsTagAware(type)` (`kind/reflection/Tagged.cs:50–59`) are the same loop over the same four attributes; `View.Declares` uses the first, `Tagged.Compute` the second, and `View.Shows` takes the result as a `bool tagged` parameter. `Tagged.cs:6–28` still describes three modes in the old terms.
- **Why-chain:** 175 made View the owner of Shows/Masks → `Tagged` kept the cache and its own copy of the predicate → the rule has two copies (stored twice) and `Tagged` sits in `kind/reflection/`, a folder whose kind no longer writes (`kind/reflection/this.cs:144,187` only read) — a cross-file arrangement.
- **Root-level fix would look like:** one predicate on `View`; the cache is the only thing `Tagged` keeps, in a place named for item's write.

### R7 — the CLR-number switch is copied (fix at the root)

- **Symptom:** the same 11 cases (`byte`, `sbyte`, `short`, `ushort`, `uint`, `ulong`, `Half`, `Int128`, `UInt128`, `BigInteger`) at `kind/json/writer.cs:85`, `text/writer.cs:115`, `formal/writer.cs:184`; plang's writer delegates to json.
- **Why-chain:** 173/174 had number write `writer.Value(…)` for its precisions → every writer's `Value(object?)` switch had to learn the narrow C# numbers → each learned them separately. `IWriter` already has a default-method mechanism (`Variable`, `Given`, `Raw`, `Content`, and now `Write`).
- **Root-level fix would look like:** one default on `IWriter` that narrows these to `Int`/`Long`/`Double`/`String` and hands the rest to the writer's own switch.

### R8 — the view default is re-decided at five sites (fix at the root)

- **Symptom:** `View? view = null` on `kind.Decode`/`Encode` (`kind/this.cs:252,270`, `kind/empty/this.cs:44,51`, `test/junit/this.cs:25`) and then `?? View.Out` in `kind/json/this.cs:55`, `text/this.cs:63`, `wire/kind/plang/this.cs:31` and `:74`; `goal/this.Item.cs:27` defaults to **`View.Store`**. Two defaults for the same missing argument.
- **Why-chain:** View went from an enum to a class (175), so it can't be a default parameter → null became the default → each kind that takes the parameter re-defaults it, and they disagree (a raw hand-off: the producer hands a null, every consumer applies its own default).
- **Root-level fix would look like:** the default lives once, at the door every kind is reached through (`kind.Decode`/`Encode` non-virtual in `kind/this.cs`, or overloads), and the kinds take a `View` that is never null.

### H1 — `item` names two core writers; the general write branches (holds)

`item.Write(text.Writer)` and `Write(formal.Writer)` (`item/this.cs:889`, `:894`) make the root type name two concrete formats. Ingi approved this explicitly (174: "a new core format is one locked virtual"), so it is the cost he took, not a miss. Two small `if`s inside the general write do remain: `type.Assembly == typeof(@this).Assembly` (`:858`, "plang's own type without a declared face") and `value is Data data` (`:877`). The first is a fact about the type that belongs inside `View.Declares`, the second is a fork that could be `Create`'s. Neither is wrong today. Carried from the old reflection kind: the catch at `:869` wraps *any* exception, a carried `AppException` included, into `OutputGetterThrew` by message. It is old code moved, and cracks a caught Error; fix when touched.

### H2 — formal's record door (holds, with notes)

`BeginRecord` (`formal/writer.cs:238–248`) is the shape 179 describes. Two notes: it decides on `record.Name` (a value fact, small), and `_records` (`:234`) is a second stack beside `_frames` that mirrors "did this Data open a record"; the line `if (_frames.Peek().Kind == Frame.Record) Close();` is repeated at `:153`, `:165`, `:216`, `:225`, `:254`. `Row(name, type, frozen)` (`:223`) takes the type entity and reads `type.Face`, so the formal writer holds a value type (the CLAUDE.md "no value type in a writer" rule). The simpler form is `Row(name, face)` or the type writing its own word; 179 chose `Face` deliberately.

### H3 — the throwaway Data, the swallowed error (holds, with notes)

`View.Out.Write(item)` (`View.cs:138`) and `Debug.Write(item)` (`:194`) each build a `Data` to call `Settle`; part of R2. `Debug.Write` catches `AppException` and prints `not set` (`:195`), losing the reason. 169 chose this on purpose ("a dump never ends the program"), so it holds; the dump could still carry the error text instead of dropping it. `Written` and `Settled` (`View.cs:68`, `:73`) are one-line wrappers over `value.Output(writer)` and `data.Settle()`: two names that add nothing.

### H4 — one behaviour change to verify (holds if every holder is a Data)

`text.Write` and `source.Write` no longer render a template (`text/this.cs:229`, `source.cs:371`); the view renders at the Data (`View.cs:129`) and the property row. A list or dict element is a Data (`list/this.cs:242`, `dict/this.cs:154`), so the rows are covered. A *bare* template item written outside a Data or a row (an `[Out]` member of a CLR type: `item/this.cs:878`, `Create(value, writer.Context).Output(writer)`) is now written as authored in Out. I could not find a caller that does that today, and I did not run one; `ViewWriteTests` pins the Data and row cases.

## New names for Ingi's naming round

Each: what it is in one line. A ⚠ is a name I'd raise.

| Name | Where | What it is |
|---|---|---|
| **View** (a class; was an enum) | `app/View.cs` | what a write shows: three constants `Store`, `Out`, `Debug`, each stateless |
| `Store` / `Out` / `Debug` | `View.cs:19–25` | the three views: kept, crossed, dumped |
| `Shows(property, tagged)` / `Masks(property)` | `View.cs:30,33` | whether a member shows / writes `****` |
| `Declares(type)` / `Undeclared(value, writer)` | `View.cs:37,41` | whether a plang type declares a face for this view / the answer when it doesn't (refuse, or name it) |
| ⚠ **`Own(value, writer)`** | `View.cs:64` | what only this view adds beside a value (Store: its variables; Debug: its `[Debug]` members). Clashes with the `Own()` that means "owned list" elsewhere (R3) |
| `Write(Data, writer, layer)` / `Write(item, writer)` | `View.cs:49,58` | a value that holds variables written kept or used |
| ⚠ `Written` / `Settled` / `Used` / `Opened` | `View.cs:68–82,141` | private steps of the above; participles used as method names |
| **Host** | `item/this.cs:885` | the C# object whose tagged members the general write shows (`this`, or a clr's `Value`) |
| ⚠ **Face** | `type/this.cs:71` | the word a type goes by with its kind (`list<path>`); text's look and a formal row's type slot |
| ⚠ **Identity(writer)** | `type/this.cs:41` | writes the type's `{name, kind?, strict?, template?}`; a noun used as a method (the shape 180 rejected). Also a type named `Identity` exists in `module/identity` |
| `Write(IWriter)` / `Write(text.Writer)` / `Write(formal.Writer)` | `item/this.cs:854,889,894` | the value's own write, and its look where text or formal differs |
| `Output(writer)` | `item/this.cs:842` | the one door, non-virtual (was a virtual with mode/context) |
| `IWriter.View` / `.Context` / `.Write(item)` | `IWriter.cs:30,34,39` | what the write shows, who asks, and the double-dispatch hand-back |
| `Row(name, type, frozen)` | `formal/writer.cs:223` | a formal row, taking the type entity (was a string) |
| ⚠ `Form(step, units)` / `Bytes(text, step, units)` | `size/this.cs:94,79` | size's text in a standard / size's text read in a standard; nouns as methods |
| `Of(bytes, standard, data)` | `size/this.cs:38` | a count made by a standard through the app's entry |
| `Text` (abstract) | `size/this.cs` | a size's text in its standard |
| `iec` / `si` (size); `short` / `iso` / `dotnet` (duration) | `size/iec`, `size/si`, `duration/short` … | a standard of the type, as a subclass (folders `size/kind/`, `duration/kind/` gone) |
| `Valued` / `Member` (choice family) | `choice/set/family/this.cs` | a value family whose subclasses are its kinds / the class a member of the family is |
| `Tagged.IsTagAware` vs `View.Tagged` | R6 | the same predicate twice |

`Child` and `declared` are **older than this diff** (`Child` is an action's/goal's branch list; `declared` is the ICreate parameter). `Form` as a *type* member (`type/this.cs:858`) predates the diff; the new `Form` is size's method.

## Open

- R5(a) needs a `type.list` surface change: a locked-core proposal to Ingi; I'd bring it, not build it.
- R1's template half waits on 158; the two plang-writer branches don't.
- Not checked by me: the "C# green but Wire's six; plang the same 11" results in rows 176–183, and whether the `.pr` bytes are still unmoved after the last two commits (I read no test output).
