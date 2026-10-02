# v17 — issue 25 core: the template kind and the Option question (shape, report only)

## 1. The template kind
Same family shape as `crypto/type/hash` (a type, its kind base, one folder per kind):
```
type/item/template/this.cs          template — what fills a value's %variables% when it is read
type/item/template/kind/this.cs     the kind base; static Choices(context) = the kinds registered (as code/kind does)
type/item/template/kind/plang/this.cs   plang — %name% filled from memory
```
`file.read`: `public partial data.@this<choice.@this<template.kind.@this>>? Template { get; init; }` — nullable, no
default (a plain read). `Start`: `Path.Use(path => path.Read(Context, Template))`, and `path.Marked` becomes the kind's
`Name` (`Template?.Name`) instead of `bool → "plang"` (`path/this.Operations.cs:67`).

**Does the string mark become this kind? Not in this change.** `"plang"` is the template mark on `type.Template`,
`text.Template`, `ReadContext.Template` and the `.pr` row's `"template": "plang"` — 33 sites, plus the .pr format.
The kind's `Name` IS that mark, so they agree by construction; moving the mark itself to the kind is its own change
(and a .pr-format question). Proposed: later, its own commit.

## 2. The Option question
```csharp
// question/this.cs
public enum Kind { Popular, Branch, Use, Action, Option }
/// The action an Option question asks about, and its option (a property whose notes line carries ask:).
public global::app.goal.step.action.@this? Action { get; init; }
public global::app.type.property.@this? Property { get; init; }
/// What an Option question offers: the option's own values, then "none" — always a choice.
public IReadOnlyList<string> Values => Property?.Type.Values is { } own ? [.. own, None] : [];
public const string None = "none";
/// The words the notes give the question (the ask: tag).
public text? Ask => Action?.Note.Line.FirstOrDefault(l => l.Name == Property.Name)?.Ask;
```
- **note line**: a third tag, `ask:` beside `say:` / `builder:` (`note/this.cs:103-107` reads it, `line.Ask`). The
  words are prose: `Template — … · ask: does the step fill the file's %variables% from memory?`.
- **Questions()** (`pick/list/this.cs:459`): for each action picked Certain whose notes have a line with `ask:` →
  `new question { Id = Key($"@option.{module}.{action}.{property}"), Kind = Option, Action, Property }`. The
  `@option.` prefix keeps it apart from `_named` (any id with a `.` is read as a common action today, :191).
  Questions() becomes async: the notes are read on first use (`await action.Note.Value(…)`).
- **Take()**: `@option.…` answers go to `_option[(action, property)] = choice`.
- **Prefill** (`:294`): `Call(action)` adds each chosen option as `Name=value` — `file.read(Path, Template=plang)`;
  "none" (or not asked) adds nothing. `Code()` (the known code) stays as it is — the writer is told, the check isn't.
- **Algorithm** (issue 28): `Algorithm` as `choice<hash.kind>` asks `{sha256, keccak256, none}` through the same
  question; "none" leaves it out and the `[Default]` applies. No count==1 yes/no path.

## Limits to know
- **Stage**: questions are computed in `Take` after each decider answer from that stage's picks; there is no stage
  3. An option is asked only of an action already picked at stage 1 (a near-certain common action — `file.read`
  is one). An action first picked at stage 2 is never asked its option; its writer still may add it from the words.
- The decider template's `Option` case and the `ask:` lines are the builder bot's.

## Names checked
Kind.Option, Action, Property, Values, Ask, None — nouns; no verb+noun.
