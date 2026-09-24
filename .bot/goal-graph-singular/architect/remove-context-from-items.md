# Remove context from item types

With Ingi, 2026-09-24. **Draft, being designed with Ingi. Not for coder yet.**

## Why

Every problem in the births pass came from an item that **stores** a context, nullable or not. A stored context goes stale as soon as the item is shared or outlives the run that made it. Getting one in also takes a late stamp, and that stamp writes on whatever is shared:

- the program's parameter rows (step 1: two actors running one goal read each other's variables and permissions)
- a value's type object (`Promote()`, the stamps on `%x!type%`)
- a list's stored elements (navigation stamps the running context onto the shared program's step list)

Ingi's spider sense throughout: we were fixing this the wrong way, by making stored contexts correct instead of not storing them.

## The rule

**An item never stores a context. When it needs one, the caller passes it.** The `Data` box carries the context; the item inside it never does. An item stores only facts about itself, for example a path's location as typed and its absolute location.

**Always the context, never the App.** `app.Format` and `context.App.Format` may not be the same thing, so an item never holds or receives an App on its own.

## Why it holds — from plang's side

A value needs a context only while it is being **used**. Every use happens during execution, inside a step, and every step has a context. Two lines carry it:

1. Everything that enters through I/O arrives as a `Data`: channel reads (`channel/this.cs:291`), file reads (`path/file/this.Operations.cs:78, :97`), `.pr` rows. Handlers return `Data`; variables store `Data`.
2. Every variable get and set goes through the memory stack (`variable/list/this.cs`), which is born with its context (`:81`).

The item's doors already receive the context from whoever asks: `Value(data)`, `Get(parent, key)` (navigation), `Output(writer, mode, context)`.

| plang | value | needs | the context comes from | status |
|---|---|---|---|---|
| `set %g% = "Hi %name%"` | text template | variables, to render `%name%` | `Value(data)`: `data.Context` (`text/this.cs:102-120`, stores none today) | done |
| `set %l% = [1, %x%]` | list template | variables | `Value(data)` | render done; element hand-out open (below) |
| `read file.txt` | path | create: the running goal's folder and the root | `Resolve(raw, context)` | settled |
| | | I/O: permission (actor), result `Data`, root, formats | the caller passes it to the verb | settled |
| `read data.json` | file | formats (json → dict) | `Value(data)`, and the file's type via `Data.Type` | settled |
| `if file.txt` | path | "does it exist" | `Data.ToBooleanAsync()` passes its context to `AsBooleanAsync` (`data/this.cs:679`) | settled |
| `get https://…` | url / http path | permission, result `Data`, formats | same as path: verbs get it passed; `Value(data)`; `Data.Type` | settled |
| `%x!type.Description%` | type | the type registry | navigation, with the asker's context (births 3.5, landed) | done |
| `write out %price%` | number | formats (culture later) | `Output(writer, mode, context)` | nothing stored today |

## The path, settled with Ingi

**When it's needed:** the path is created at execution. The instruction becomes an execution at the generator's run copy plus the handler's `await Path.Value()`, and not before. At `.pr` load the row holds only the raw text in a `wire`. `path.Resolve(raw, context)` runs at execution and is the only creation door. Every C# infra caller also passes a context: most pass their running one, and some pass `App.System.Context` explicitly (`app/this.cs:360, 406, 538`; `goal/list/this.cs:337, 343`; `module/list/this.cs:167`; `build/this.cs:78`).

**What it stores:** facts only, namely `_location` (as typed) and `_absolute`. No context, no App.

**Every use gets the context passed:**

| Use | Where the context comes from |
|---|---|
| Create: goal folder + root (`path/file/this.cs:77-111`) | `Resolve(raw, context)`, used there and not kept |
| Verbs: read, write, exists, list, delete, copy, move (about 110 call sites) | the caller passes its context; handler and infra callers both have one |
| Permission (`this.Authorize.cs:27-98`, `file/this.Operations.cs:362-413`) | the context passed to the verb |
| The result `Data` (`Context.Ok/Error`, about 40 sites) | the context passed to the verb |
| `Relative` (root): writing out a derived path (`Portable`, `this.cs:290-307`) | `Output(writer, mode, context)`. `IWriter` stays context-free: it writes tokens, not who is running |
| `Relative`: comparisons (`IsUnder`, `Owns`) | the verb or handler doing them |
| `MimeType` / kind from extension: `file.read` (`:60, :69, :126`), the file channel (`channel/type/file/this.cs:27`), writing a file (`file/serializer/Default.cs:16`) | the handler, `Value(data)`, `Output(…, context)` |
| A file's or image's type (kind from the extension, `file/this.cs:54`) | `Data.Type` passes its own context. Only file and image use it; the other 34 `Type` getters take the parameter and ignore it |
| Navigation `%x!relative%`, `%x!mimetype%` | the memory stack |
| Derived paths (`Parent`, `Combine`, …, `file/this.Derivation.cs`) | nothing to pass: string math on the absolute; the new path stores its facts |

**Rejected:** storing the root as a string, and storing the App reference (Ingi: pass the reference, never a copied value; and never the App, always the context).

**Found:** the JSON path converter (`channel/serializer/json/converter.cs:26, :78`) has a constructor with no context and passes `_context!` to `Resolve`, so it can throw.

## Truthiness

`IBooleanResolvable.AsBooleanAsync()` (`data/IBooleanResolvable.cs:21`) takes no context. Its caller is `Data.ToBooleanAsync()` (`data/this.cs:679`), and a Data has one, so it passes it: `AsBooleanAsync(context)`. Implementers: item base (`type/item/this.cs:470`), `code` (`type/code/this.cs:30`), path (`path/this.Operations.cs:170`), http (`path/http/this.cs:225`), file (`path/file/this.Operations.cs:132`), image (`image/this.cs:234`). One other caller: `ui/code/Fluid.cs:44`.

## Still open — types that store a context today, beyond the table

Items holding a context now (`module.IContext`): `path`, `file`, `url`, `directory`, `source`, `list`, `dict`, `computed`, `clr`. The path family is settled above. Next, one at a time:

1. **list / dict**: element births (`new Data("", Create(raw, _context), context: _context)`, `list/this.cs:137`; `dict/this.cs:134`), stamps on stored elements (`list :134, :147, :335, :345, :495, :503`; `dict :123, :279, :291`), and the element hand-out in navigation. This is coder's uncommitted ruling-1 question: (a) hand out `element.Copy(parent.Context)`, or (b) pass the context through the walk.
2. **source** (`source.cs:37`): loads already use the asking Data's context (births step 1). What is the stored one still for?
3. **computed** (`computed.cs:26`) and **clr** (`clr/this.cs:47`).

## Relation to work in flight

Births step 3 is landed through 3.5 (`d3ac19588`). Coder holds uncommitted changes for my held rulings 1, 3, 4, 5 (sent before Ingi answered). This design may replace them, especially ruling 1 (list elements).
