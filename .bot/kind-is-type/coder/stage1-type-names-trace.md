# Stage 1 — a type is named by its path from a root: trace and shape

On `kind-is-type` at 968cd0c79. No code changed. The type list was read from a running app (a temporary probe,
deleted), so "today" below is what the registry holds, not what the source looks like it should hold.

## Where a name is made today

- `type/item/this.cs:522–535` `IdentityOf`: a class's namespace, lowercased, `@` dropped. An `@this` class is its
  folder (`app.type.item.text`); any other class is its folder plus its name (`app.module.llm.toolcall`). A **nested**
  class reads only its namespace plus its own name: `app.module.llm.decider+setting` → `app.module.llm.setting`.
- `type/item/this.cs:539–541` `NameOf`: the `[PlangType("…")]` word, else that namespace.
- `type/list/Registry.cs:371–377` `NameOf`: an item class is a type when it's an `@this` or declares
  `[PlangType]`, and is no family's kind.
- `type/this.cs:816–831` `Names`/`Claims`: a type answers to its name, its namespace and its static `Alias`es.
  The guard (`Registry.cs:123–131`) keeps every claim to one class.
- `type/item/this.cs:531`: a family member's kind name is the last word of its `[PlangType]` word, else of its
  namespace.
- `type/item/choice/set/this.cs:47–51`: a closed set (an enum) **must** declare `[PlangType("…")]`. Its word is the
  set's name, and the set is a kind of `choice` (`kind: "operator"` in a `.pr`). `type/reader/this.cs:166` reads
  the word too.
- `[PlangType("…")]` appears in 98 files: about 75 item types and 23 enums.

## Settings today

- Each settings class is a **kind of `setting`** (`Registry.cs:330–334` → `setting/kind/`), not a type.
- Its path is its own rule (`type/item/setting/this.cs:23–32`):
  - a module's drops `app.module.`;
  - plang's own `app.type.item.X` becomes `app.type.X`;
  - the rest keep `app.`;
  - a nested class reads through its owner (`llm.decider.setting`).
- Saved rows are keyed `<actor>!<path>` (`actor/setting/this.cs:174`, `:189`, `:238`).
- Programs read settings by navigation (`%!app.goal.list.setting%`, `%!llm.setting.cache%`), never by type name.

## The rule, applied

- `app.type.item.X` → `X` (plang's own); `app.type.item` itself → `item`.
- `app.module.M[.type].X` → `M.X` (a module's: the module, then its `type.` folder dropped).
- `app.X` → `X` (the rest).
- Other assemblies → their namespace as is.

### Types whose name changes (today → by the rule)

**In `.pr` files** (tracked `.pr`: uses / files):

| today | rule | in .pr |
|---|---|---|
| `action` | `goal.step.action` | 26 / 21, plus 75 as the kind of a `list` (`list` of `action`) |
| `query` | `list.query` | 29 / 26 |
| `conversation` | `llm.conversation` | 10 / 7 |
| `limit` | `llm.limit` | 10 / 7 |
| `message` | `llm.message` | 0 as a type; 10 as the kind of a `list` (`list` of `message`) |
| `redirect` | `http.redirect` | 2 / 1 |
| `archive` | `archive.archive` | 1 / 1 |
| `entity` | `http.entity` | 1 / 1 |
| `screen` | `screen.screen` | 1 / 1 |

**Not in any `.pr`** (C# lookups and teaching only):
- `step` → `goal.step`; `clause` → `goal.step.action.clause`; `keep` → `goal.step.action.keep`; `loop` → `goal.step.action.loop`
- `ask` → `output.ask`; `browser` → `browser.browser`; `cursor` → `browser.cursor`; `element` → `screen.element`
- `window` → `window.window`; `progress` → `http.progress`; `hash` → `crypto.hash`; `process` → `terminal.process`
- `held` → `archive.archive.held`; `timing` → `test.timing`
- `clr` → `type.clr`; `code` → `type.code`; `table` → `type.table`
- `error` → `error.error`: a class named `error` in `app/error/`, not an `@this`
- `identity` → `identity.identity`: a module class outside a `type/` folder

**Shorter** (no `[PlangType]`, an `@this` named by its full namespace today), all `app.` dropped: `app.call` → `call`,
`app.call.list` → `call.list`, `app.channel.list`, `app.channel.type.*` → `channel.type.*`, `app.event.on`,
`app.event.binding*`, `app.parent`, `app.store.sqlite`, `app.task.list`, `app.type.root` → `type.root`,
`app.actor.context.trace`. Plang's own: `app.type.item.note`/`note.line`/`prose` → `note`, `note.line`, `prose`.
Modules': `app.module.browser.type.cdp(.session)` → `browser.cdp(.session)`;
`app.module.screen.type.element.list` → `screen.element.list`; `app.module.window.type.window.list` →
`window.window.list`; `app.module.code.defaultoverride`/`registration` → `code.*`.

**Unchanged:** every `app.type.item.*` type (`text`, `list`, `path`, …, 1123 `item` and 963 `text` uses in `.pr`),
and `actor`, `channel`, `event`, `goal`, `module`, `shortcut`, `snapshot`, `store`, `task`, `test`, `type`.

**Aliases** (`string`, `array`, `dictionary`, `map`, `boolean`, `bytes`) are static `Alias`es, not words. The
ruling doesn't name them; my reading is they stay.

### Settings classes, each its own type (today's path → rule name)

| path today (row key) | type name by the rule |
|---|---|
| `llm.setting`, `terminal.setting`, `signing.setting`, `math.setting`, `math.setting.equal`, `identity.setting`, `debug.setting`, `debug.setting.llm`, `debug.setting.length`, `build.setting`, `archive.setting` | the same |
| `llm.decider.setting` | `llm.decider.setting` **only if** a nested class reads through its owner (today it reads `llm.setting`; clash below) |
| `app.goal.list.setting` | `goal.list.setting` |
| `app.call.setting`, `.frame`, `.diff` | `call.setting`, `call.setting.frame`, `call.setting.diff` |
| `app.actor.permission.setting` | `actor.permission.setting` |
| `app.test.setting` | `test.setting` |
| `app.type.size.setting` | `size.setting` |
| `app.setting` | `setting`, **which clashes with plang's own `setting`** |

## Clashes the rule makes

1. **`setting`**: plang's own `app.type.item.setting` (the base and the bare node) and the app's own settings class
   `app.setting` (path `app.setting`) both read `setting`.
2. **`llm.setting`**: `app.module.llm.setting` and the nested `app.module.llm.decider+setting`. `IdentityOf` reads a
   nested class as its namespace plus its own name. The fix is for a nested class to read through its declaring
   class (`llm.decider.setting`), as its setting path already does. That's a behaviour fix inside
   `IdentityOf`, no surface change.

No other two classes share a rule name.

## What reads or writes these names (sites that move)

- **Code:** `type/item/this.cs:516–551` (IdentityOf, NameOf: no word, the root rule); `Registry.cs:371–377`
  (a type is an `@this`; no word); `Registry.cs:330–334` and `type/item/setting/kind/` (settings as a family's
  kind: goes); `type/item/setting/this.cs:23–32` (the path rule: one with the name, see OPEN 3);
  `PlangTypeAttribute` and its 98 uses.
- **C# naming a changing type by string:**
  - `"action"`: `app/this.cs:304`, `test/Coverage.cs:46`, `goal/step/action/this.Validate.cs:54–55`,
    `action/serializer/Reader.cs:153–154`, `formal/reader.cs:221`, `:349` (F8.1's "is this an action slot")
  - `"step"`: `app/this.cs:303`, `test/this.cs:176`
  - `"message"`: `llm/type/message/serializer/Reader.cs:18`
  - `"conversation"`: `llm/type/conversation/this.cs:31`
  - `"hash"`: `crypto/code/Default.cs:41`, `:70`
  - `"archive"`: `archive/type/archive/this.cs:43`, `:74`; `archive/code/Default.cs:34`, `:89`
  - Each becomes `type.list[typeof(X)]` (asked by class, no name), or the new name.
- **`.pr` files:** the 9 names in the table. Read by name through the type list, so the reader must find the old
  name or the files are rebuilt (OPEN 2).
- **plang goals:** none assert a changing name (the `%x!type.name%` checks name `text`, `date`, `datetime`, `image`,
  `bool`, `time`, `number`, `null`, `file`, `duration`, `store`, `path`, `list`). No goal reads
  `%!app.type["…"]%` by a changing name.
- **Teaching:** `os/system/type/<name>/` is one folder per type by today's name (46 folders: `action`, `archive`,
  `clause`, `code`, `conversation`, `entity`, `hash`, `held`, `keep`, `limit`, `loop`, `progress`, `query`,
  `redirect`, `screen`, …). The type pages (TypePageTests goldens) and the decider's prompt C list type names.
  That's a teaching edit: re-pin the decider fixtures and the page goldens.
- **C# tests** asserting today's names: the stage's sweep will list them. `NameOf`/`NamespaceOf` are read directly
  in 2 test files.

## Shape

1. A type's name is its namespace read from its root:
   - `app.type.item` → `item`, and `app.type.item.X` → `X`;
   - `app.module.M[.type].X` → `M.X`;
   - `app.X` → `X`;
   - another assembly's namespace as is;
   - a nested class through its declaring class.
   One member on `item` makes it (`IdentityOf`); `NameOf` returns it. `[PlangType]` goes from item types.
2. A type is an `@this` item class, or a nested or named item class (the settings classes, `llm.decider+setting`).
   The registry claims each type's name, plus its aliases; the full namespace stays a claim. The one-class guard
   refuses a rule clash, naming both classes.
3. A written name that resolves under two roots is refused, naming both. This is the guard's error at load (a clash
   of two classes) and the lookup's error when a short written name could mean two (OPEN 4).
4. Each settings class is a type of its own, named by the rule. `setting/kind/` and `Registry.cs:330–334` go. Plang's
   own `setting` stays the base and the node (OPEN 1).
5. Closed sets (enums) keep their `[PlangType]` word as their set name until stage 2 (a kind is a type). The word
   stays only there (OPEN 5).

## OPEN (for Ingi / the architect)

1. **`setting` clash:** the app's own settings class reads `setting`, as does plang's own `setting`. Name it
   `app.setting` (the app as its root), or move the class?
2. **`.pr` files** hold 9 changing names (about 80 rows plus 85 as list kinds). Reading an old name: the type list
   answers the old word as a claim during a transition (each moved type keeps its old word as an alias), or the
   affected `.pr` files are rebuilt (builder, about 45 files). Which?
3. **Settings' path and name:** path `app.goal.list.setting` vs name `goal.list.setting` (7 classes differ).
   - (a) The path becomes the name: one source, but saved rows keyed `system!app.goal.list.setting` are orphaned
     unless read once under the old key.
   - (b) The path stays the navigation address and the row key, and the name is the type's identity.
4. **Short names:** does `window` find `window.window` when it is the only type ending in `window`? The ruling says
   "nothing: a module's type is named by its module". And `screen.window` is a kind of `screen` (a family member),
   not a type, so "screen.window finds the screen's" is through screen's kinds. What may be dropped from a written
   name is the ruling's call.
5. **Closed sets** (23 enums, their names as `choice` kinds in `.pr`: `cache`, `execution`, `errororder`, `overflow`,
   `precision`, `scope`, `subfolder`, `when`, `httpmethod`, …): keep the word to stage 2? If the word goes now,
   `kind: "cache"` becomes `cache.cache` in every `.pr`.
6. **`error.error`, `identity.identity`:** classes outside a `type/` folder or not an `@this` read oddly by the rule.
   Make them the folder's `@this` (`error`, `identity`), or accept the names?
7. **The three stage-1 tests** named in the brief (ShortNameFindsTheType, SettingClassIsAType,
   ModuleTypeIsNamedByItsModule) aren't on origin/kind-is-type yet; only `start.md` and `start.goal` are there.
