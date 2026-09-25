# Stage 4a check-in — where the formal reader and writer fit

Branch `builder-formal`. Traced, nothing built. Your call on the four questions at the end.

## What's there (traced)

**Write side: the value pushes itself into an `IWriter`, and the format writer lays it out.**
- `app/channel/serializer/IWriter.cs:19-105`: leaf pushes (`String`, `Long`, `Double`, `Bool`, `Null`, …), `BeginArray/EndArray`, `BeginObject/Name/EndObject`, `BeginRecord/Value/EndRecord` (the Data envelope, laid out per format), and `Raw` (verbatim content in the target format).
  - `Format` (`:22-29`) is the short token a value branches on "when it renders differently per format". That's the sanctioned per-format branch.
- Writers: `json/writer.cs` ("json", or "plang" with `emitsSchema`) and `text/writer.cs`. The `application/plang` serializer (`plang/this.cs:42`) makes a `json.Writer` and calls `item.Output(writer, view, ctx)`. `SerializeItemAsync` (`:114`) is the bare-item write a goal uses for its `.pr`.
- **The action writes itself:** `goal/step/action/this.Item.cs:40-77`, `{module, name, property, default?, modifier[], child?, recovery?}`. `property.list` writes its rows `{name, type, value}`; `action.list.Output` (`action/list/this.cs:153`) writes the bare array.

**Read side: a type pulls itself off an `IReader`.**
- `IReader.cs:42-111` is a forward-only token stream (`Peek`, leaf pulls, `BeginObject/NextName`, `RawValue`, `Skip`), and `json.Reader` is a `ref struct` over `Utf8JsonReader`.
- `type/reader/this.cs` is the `ITypeReader` registry. It scans `…/serializer/*.cs` and registers only readers with a parameterless ctor (`:199-205`).
- **The action's reader is born with its step** (`goal/step/action/serializer/Reader.cs:16-20`), which is what keeps it out of the parentless registry: "there is no parentless way to make an action".
  - It reads `module` through `ctx.App.Module[name]` (fails at load), and property rows `{name, type, value}`, where `type.Read` makes the value (`:116-156`).
  - It reads modifiers as the modifier subtype, recovery as actions of the same step, and `child` as steps (`:75-105`).
- **The step reader** (`goal/step/serializer/Reader.cs:44-50`) reads `"action": [ … ]` through that action reader; an old `"actions"` key is `PrFormatOutdated` (`:55`).
- **Declared properties:** the catalogue action `module[name]` holds its `property.list` (reflected from the handler, filtered at `type/property/list/this.cs:82`). Each row carries `Type` (with choice `Values`), `Nullable`, `Default`. That's what the stage-3 template reads as `a.Property`.
- **Modifier order today:** `step.Nest` (`goal/step/this.cs:66-100`) turns a flat LLM list into modifiers on the preceding action, then sorts them by `Position` (`[Modifier(Order)]`, outermost first).

## Where formal fits — proposal

**Writer: a format, beside json and text.**
- A new `app/channel/serializer/formal/writer.cs`, `formal.Writer : IWriter`, `Format => "formal"`. Leaves push formal literals:
  - `String` → a quoted, escaped text; `Long/Double/Decimal` → bare; `Bool`, `Null` → `true`/`false`/`null`;
  - `BeginArray` → `[a, b]`; `BeginObject/Name` → `{"k": v}` (a dict literal, keys quoted);
  - `Raw` → verbatim; `BeginRecord/Value` → the value alone (formal carries no Data envelope).
- **Every value writes itself through it unchanged:** text, number, list and dict already push these tokens. No converter per type.
- **The action writes itself in formal.** `action.Output` branches once on `writer.Format == "formal"`, the per-format branch `IWriter.Format` exists for:
  - it writes `module.name(` then each row, then `)`;
  - a condition's body is ` { a; b }`; each modifier wraps it, outermost first, with the wrapped action on its own indented line;
  - error.handle's recovery is the row `Recovery: list<action> = [ … ]`.
- **The rows are written by `property`**, which owns name, type and value: `Name: type = value`, or `?=` for a row from `action.Default` (a frozen default).
  - The one decision the property makes: a value that is exactly a `%variable%`, in a slot not typed `text`, is written bare (`Value: item = %!data%`); anything else is the value writing itself (`"Total: %x%"` quoted).
- **`action.list` writes `a; b`.** For the `.pr` (4b), the step writes its facts as JSON and its `action` as one string: the action list rendered through a `formal.Writer`.

**Reader: not an `IReader`. A parser beside the action's JSON reader, born with the step.**
- `IReader` is a token stream for self-describing formats. Formal isn't one, for two reasons:
  - (1) **the host comes after its modifier:** `error.handle(…) { goal.call(…) }` wraps, so a forward-only token reader would have to buffer and restructure;
  - (2) **untyped literals take their type from the catalogue**, not from the stream.
- Instead, a new `goal/step/action/serializer/Formal.cs`, `Formal(step)`, reads a formal string into the step's `action.list`: a recursive-descent parser, the C# twin of `tools/decider/formal.py`.
  - It is born with the step, so every action it makes holds that step (Reader.cs's birth rule). It also stays out of the parentless registry (no parameterless ctor).
  - The module goes through `ctx.App.Module[name]`, so a .pr naming a gone module fails at load, as today.
  - **Typing:** each row's type is the catalogue action's declared `Property[name].Type`. In an `item` slot it is the written type if there is one, else the literal's. A written type against a declared one must be equal.
  - **Values** are born through the type's own doors, as the JSON reader's are: `type.Create(raw, ctx)`, `Variable.Resolve` for a variable slot (a bare `"x"` too), and a recursive read for an `action`-typed value (same step).
  - Choice options are checked, and a bare option is accepted.
  - Modifier nesting becomes `wrapped.Modifier`, outermost first. `Recovery=[…]` becomes `modifier.Recovery`. A condition's `{ }` becomes `Child`.
  - Errors carry line, column and the fix: an `AppException`, key `FormalInvalid`, the fix in `FixSuggestion`, so the builder's retry can quote it.
- Callers: the step reader's `action` (4b), and the builder's answer (4c; `build.match` reads the answer through the same door, as it does today with JSON).

**No registration.** Formal isn't a channel format (no MIME, not a Data transport); it's the action's own text form, reached from the step. A `text/x-plang-formal` serializer could come later if a channel ever needs it.

**Modifier order.** With formal, the written nesting is the order, and the parser nests. `step.Nest`'s flat-list-then-sort-by-`Position` is only needed for a flat JSON answer. It goes with the JSON rows in 4b, and `[Modifier(Order)]` stays only as what the prompt teaches. The `modifier.list` refactor (owning `Wrap` and the catch grouping) lands in 4b as planned.

## Questions for you

1. **The action's formal write: a branch in `action.Output` on `writer.Format == "formal"`**, as above. The alternative is a separate face (`action.Formal`) outside the `IWriter` path. I'd take the branch: one door, the sanctioned pattern, and the values write through the same writer.
2. **The reader as a class beside `Reader.cs`, not an `IReader`.** The two reasons are above. Confirm.
3. **An inline `{ }` body becomes one child step whose text is the body's formal** (the stage-2 open point, needed by 4b's envelope). Indented child steps keep their own index, text and line. So a condition's `Child` holds either steps from indentation (real text) or one step from `{ }` (formal text). Confirm, or say how you want the envelope to tell them apart.
4. **Byte-equality with python's `formal_golden.txt`.** That file writes nested modifiers over lines with 4-space indents, and conditions inline. The C# writer will match it exactly, and the test compares bytes. OK?
