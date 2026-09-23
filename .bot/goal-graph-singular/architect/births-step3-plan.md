# Births step 3 — a type object holds no context; the asker brings it

Designed with Ingi 2026-09-23 (read the "why" there). **Written, not yet sent to coder.** Step 1 is `ee03f91f2`.

> **You (coder) own this.** The rules are settled with Ingi. Shapes, names not fixed below, and the commit split are yours. Code below is direction; NEW marks what does not exist.

## Why

A value's type (`%x!type%`) is a bare object the item builds: name and kind, nothing else (`goal/this.Item.cs:12`, `Type => new("goal", typeof(@this))`, 36 getters). The facts about a type live in `app.type`: description, fields, a choice's values, example, shape, kinds, and its C# class. To answer them, the bare object needs a context that someone writes onto it afterwards (`data/this.cs:426`). `Promote()` then copies every fact over (`type/this.cs:573-611`), and it throws when nobody wrote the context (`:591`). Where no context arrives, `ClrType` falls back to static tables (`type/this.cs:163`).

## The design (with Ingi)

- **`%x!type%` and `app.Type["image"]` are one concept, a type.** `app.type.list` lists them and `app.Type[name]` selects one. `%x!type.Description%` must never be empty.
- **The facts live on the full type in `app.type`.** A value's type answers them by reading its full type, never by copying.
- **The context comes from the one asking, never from the type object** — the same rule as step 1's loading rule. Ingi: "`%x!type%` goes through memorystack, can we attach the context there?" It is already there: every navigation hop is asked by a Data that carries the memory stack's context (`data/this.Navigation.cs:266-269` builds the `!type` child with x's context; the navigation door passes `parent.Context`, `type/item/this.cs:235-237`).
- **Items need no context for their type.** A text or number describes itself (name, kind, its C# class). The full type comes from `app.type` when someone asks with a context.

## Trace: every C# use that needs `app.type` already holds a context

| Use | Sites | Needs | Context at hand → change |
|---|---|---|---|
| `Is(other)` / `Is(name)` (`type/this.cs:485-501`) | `data.Is` | name only | none needed → the stamp at `data/this.cs:192` goes |
| `ClrType` on a full type from `app.type` | `data/reader/this.cs:99`, `type/item/this.cs:619`, `type/this.cs:258, :287` | stamped class | already asked through a context → no change |
| type-side exit check `x.Type?.ClrType.Exit()` | `data/ShouldExit.cs:31`, `path/this.Operations.cs:43, :144, :158`, `module/action/file/read.cs:74` | the class is `IExitsGoal` | the result Data's context → **one owner** for the check (5 copies today; `ShouldExitExtensions` is also a static class) |
| `ClrType` on a declared type (from `.pr`) | `variable/set.cs:32`, `:217` | the class | handler's `Context` → `Context.App.Type[t]` |
| `Create` doors → `Creatable` → `ClrType` (`type/this.cs:339-367`) | `data/this.cs:250, :303`, `type/item/this.cs:90`, `kind/reflection/this.cs:168`, `path/file/this.Operations.cs:78, :97`, `variable/set.cs:227, :293`, `channel/this.cs:291`, `data/reader/this.cs:101` | the class | every door already receives a context (or a Data) → the binder uses it (`Bind(raw, ctx)` has it and doesn't pass it on) |
| `Compressible` (`type/this.cs:168-181`) | `data/this.Transport.cs:54` | the format registry | the Data's context → it's the format's knowledge: `App.Format` answers for a type (`format/list/this.cs:485` `Compressible(kind)`, `:499` `FamilyOf`) |
| `Scheme` (`type/this.cs:550-551`) | no reader | — | delete |
| the eight facts | builder/catalog read full types; no `.goal`/template reads them off a value's type | the full type | navigation, with the asker's context |

## The changes

**1. `Kind` is read-only; `variable.set` never changes a type object.** `variable/set.cs:207` and `:228` do `type.Kind = ...` on the type object from its `Type` parameter. That value is shared with the program row (step 1's run copy shares the value), so this writes on the shared program today. Once types are `app.type`'s shared objects it would also corrupt the registry. `Kind { get; init; }`. `variable.set` takes a new type for the changed kind through the door in 4.

**2. Step A's slot comes out.** `app.Context` is set in `action.Run` (`goal/step/action/this.cs:153`), `goal.Run` (`goal/this.cs:287`) and `Start()`, and read nowhere in production. Delete the slot, the three set lines, the TUnit hook and their tests.

**3. Callers bring the context.**
- The `Create` doors' binder asks for the class with the context it is handed: the full type from `ctx.App.Type[this]` (or `data.Context` for the Data door).
- `variable/set.cs:32` and `:217` ask `Context.App.Type[t]`.
- The exit check has one owner, reached through the result Data (its context). The 5 inline copies go.
- `Compressible` becomes the format's: the Data asks `App.Format` about its type. `type.Compressible` goes.
- `ClrType`'s fallback chain goes: it answers what was stamped at birth. Full types from `app.type` always carry it.

**4. `app.type` hands out full types; a value's type navigates as its full type.**
- `app.Type[type]` NEW: the full type for a value's type (name, kind, strict, template). It carries the entry's facts plus that identity, is cached per identity, and holds no context. For `{choice, <set>}` it carries that set's values (the choice list is keyed by CLR type today, `type/list/this.cs:205`; it needs the set by name).
- `app.Type[name]` is **the one door for a type by name**. It accepts the spelled forms (`"string"`, `"text/markdown"`) and canonicalises them. That is `app.type`'s knowledge (aliases, format kinds). `context.Type` (`type/factory.cs`) and the static `type.Create(name, …, context)` (`type/this.cs:400-429`) die; their callers (7 + 8) use `app.Type[...]`.
- The type object navigates as its full type, found with the asker's context:
  ```csharp
  // type/this.cs — NEW
  public override ValueTask<data.@this> Get(data.@this parent, string key)
      => new global::app.type.clr.@this(parent.Context.App.Type[this], parent.Context).Get(parent, key);
  ```
  For a full type, `App.Type[this]` returns itself.
- The eight facts are plain `init` properties, set by `app.type` when it builds full types. `Promote()`, `_foldLoaded` and the type object's `Context` die.

**5. The static tables** (open-items #14): `GetPrimitiveOrMime`, `ClrFromMime`, and the static primitive alias table (`type/primitive/this.cs:24`, read by the constructor's `Canonicalise`/`StampPrimitive`). Canonicalising belongs to the `app.type` door (4), so the constructor stops doing it. Types hold canonical names, and raw spellings are canonicalised at the door. Verify that the builder writes canonical names into the `.pr`; I haven't checked. Move what survives into `app.type` as instance members; delete the rest. If you find a place where a raw spelling reaches a type without going through the door, bring it to me.

**6. #27** — one template line: `os/system/builder/llm/templates/propertiesUser.template:20` prints the parameter type's values when it has them. The menu's types already come from `app.type` (`goal/step/action/property/this.cs:36`). Independent; can land first.

## Tests (red first where they aren't red already)

1. `%x!type.Description%` is filled for a text, an image and a goal value; `%x!type.Kind%` still answers x's own kind (`gif`), not the entry's.
2. `%x!type.Values%` for a choice value answers its set's options.
3. `variable.set` with an `as` kind (canonicalised, and derived) run twice leaves the program row's type unchanged. Today it is changed.
4. A declared `{image, gif, strict}` literal through `variable.set` still probes strict (`:32`), with no context on the type object.
5. An `ask` result still stops the goal through the one exit owner, including the typed-absent case.
6. `Compressible` answers the same as before for text, archive and image.
7. The menu (#27) shows a choice slot's options.

Six suites by name after each step; nothing red committed.

## Demolition — what must not survive

- `type.@this`: `Context`, `Promote()`, `_foldLoaded`, `Compressible`, `Scheme`, the `ClrType` fallback chain, the static `Create(name, …, context)`, the `Kind` setter.
- `type/factory.cs` (`context.Type`).
- Context stamps on type objects: `data/this.cs:192`, `data/this.cs:426`, `type/this.cs:428`, `type/kind/this.cs:53`, `type/serializer/Reader.cs:38`, `variable/set.cs:211`.
- `variable/set.cs:207`, `:228` (`type.Kind = ...`).
- The navigation stamp `data/this.Navigation.cs:105-106` (`contextual.Context = _context`) — loading uses the asker's context since step 1. Verify nothing still relies on it.
- The 5 inline exit checks; `ShouldExitExtensions` as a static class.
- `GetPrimitiveOrMime`, `ClrFromMime`, the static primitive table (as statics).
- Step A: `app.Context`, its three set lines, the TUnit hook, their tests.

**Stays:** the 36 item `Type` getters (each describes itself; no context); `Is`, `Equals`, `Write`, `ToString`; the `Create` doors (they gain the context they're handed); the static identity helpers `String/Int/…` as constants.

## Order — each step compiles and is its own commit

1. #27 template line.
2. `Kind` read-only + `variable.set` (test 3 red first).
3. Step A out.
4. Callers bring the context: `Create` binder, `variable.set`, exit owner, `Compressible` → format; `ClrType` fallback dies (tests 4-6).
5. `app.Type[type]` + navigation + one door by name; `Promote`/`Context`/stamps/factory die (tests 1-2).
6. Static tables.

#15 (`ReturnTypeName`) and #24 (`test.Create` static) come after this plan.

## OBP validation

| Surface | Check |
|---|---|
| type object | holds only its own facts; no context, no copy of another object's facts (no flat copy, no late stamp) |
| `app.Type[name]` / `app.Type[type]` | the registry selects and mints full types; one door per question; `context.Type` + static `Create` were a second door (named twice) |
| navigation `Get` override | the type answers as its full type using the asker's context: the same rule as loading |
| exit check | one owner instead of 5 copies (raw hand-off); no static class |
| `Compressible` | moves to the format, whose knowledge it is |
| `Kind` | read-only: a shared type object is never written |
