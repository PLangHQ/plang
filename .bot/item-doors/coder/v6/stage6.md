# coder — item-doors — v6 — stage 6 (settings): the plan

Nothing built for this doc. 257's two pieces are in (blind store `Set` + the command line's check, 26a05f2c8), and so
is 259's alias half (`setting.Paths`, the index asks each setting). What's left is 258 (Get<T> keeps), the rest of
28's demolition, 266 (the store from code) and the three nits on 26a05f2c8.

## 1. What each ruling changes

### 258 — `Get<T>` keeps the setting it made, until a write clears it

```csharp
// actor/setting/this.cs
public async ValueTask<data.@this<T>> Get<T>() where T : setting.@this   // T's address → this run's values (closest
                                                                         // scope) ← the saved row ← T's defaults;
                                                                         // made once, kept per address until a write there
public data.@this Set(string address, object? value)                     // blind (in); clears what's kept under it
```
- `_built` / `_version` / `Build` / `this[path]` fold into `Get<T>`: one kept T per address in the scope that made it. A
  write anywhere moves the root's version as today (one counter, owned by the root), so a kept T made before it is
  stale. That's today's mechanism, renamed into the one door; no second store.
- `Of<T>()` (32 prod, 56 test) becomes `await Get<T>()` (149 (1): async). The sync readers:
  - `data/schema/signature.cs:104` (Expiry inside the ref-struct reader) and `size/this.cs:44–45` (Standard in a
    constructor) can't await, so their async makers hand the value in (149 (1)).
  - The other 28 prod sites have an async member one hop up (counted, `grep Setting.Of<`).
- `Of(owner / type / Type / module / action)` (6 prod, the `%!x.setting%` reads) answers through `Get` by the owner's
  setting address. These 6 stay as doors, one per owner kind; they're how a navigation reaches a setting.

### 259 (3) — the setting answers its own paths (done in 257 (2))

`setting.Paths` and `setting.Option(path)`; the store's `Answers()` asks `Make(path).Paths`. Nothing more here
except the nits (§5).

### 257 (5) — the store is blind (done, 26a05f2c8)

## 2. Decision 28's demolition list, as it stands today

| piece | today | stage 6 | why |
|---|---|---|---|
| `_built`, `_version`, `Build`, `this[path]` | the store's built cache | **fold** into `Get<T>` | 258 |
| `Under(path)` | turns this run's per-option rows back into an options dict for `Build` | **stays**, private to `Get<T>`'s make | rows stay one per option (nit 3, §5) |
| `Flatten` | a dict given at the crossing → per-option rows | **stays**, the store's own storage | same |
| `Set(path, IDictionary)` checked form | gone | done | 257 |
| `Option(row, name)` (store, private) | reads one option of a saved row: a setting's property, or an action's Property row | **goes** with the node classes | 149 (4): `Get(action, option)` reads the action's member, then the module's, else the default |
| node classes `setting.action` (2 prod) / `setting.module` (13 prod) | a setting stand-in for an owner with no class | **go** | 149 (4): action and module answer their own address; the 13 module sites are mostly `Of(module)` and the module's `Set` override |
| `Rows(context)` (3 prod, 9 test) | the teaching rows from a setting's reflected options | **stays** for a class; a node's rows come from the action's `Property` | 149 (4) |
| reflection walk: `Apply(dict)`, `Property`, `Options`, `IsComposite`, `Construct`, `Copy` | how T is made from rows, and the CLI's check | **stays** | 257 made it the command line's check (`Option` → `Apply`), and it is T's type making T. Replacing it with a type-system record maker is #4's, not this stage's |
| `NotASettingClass` (Save) | refuses saving a node | **goes** with the nodes | nothing to refuse once a node is no setting |
| `Own` / `OwnAttribute` | 0 classes use `[Own]` | **goes** | dead (149) |
| `Fresh(path)` (1 prod, step scope teaching) | a class at its defaults | **stays** | the build's step scope teaches from it |
| `Clone()` (0 prod, 1 test) | a scope copy | **goes**; its one test moves to two scopes | nothing in the program clones a scope |
| `Load()` (2 prod) | reads the actor's rows once | **stays**, inside `Get` on first ask | 149 (1) |

## 3. Locked members touched

- `app`'s members: none.
- The settings store (`actor.setting`) isn't on the surface golden. `Of<T>` → `Get<T>` and the node classes going are
  ordinary changes.
- `setting` (a type with `[PlangType]`): `Rows`, `Path`, `Paths`, `Option`, `Apply`, `Enabled`/`Disabled` stay. The
  module/action node classes leave the pinned surface: that is a removal, and I'll re-pin citing 149 (4).
- 266's interface is new and module-owned (§4). It is not a locked surface.

## 4. 266 — the settings store from code

```csharp
// app/store/code/IStore.cs  (NEW): a store provider, named for what it gives
public interface IStore : global::app.module.code.ICode
{
    // the store at file (or in memory under memory's name, when that answers one), born with context
    global::app.store.@this Open(global::app.type.item.path.@this file, System.Func<string?> memory, actor.context.@this context);
}
// app/store/code/Sqlite.cs  (NEW): the built-in default — Open answers new store.sqlite.@this(file, memory, context)
// module/code/this.cs RegisterDefaults: RegisterBuiltIn<global::app.store.code.IStore>(new global::app.store.code.Sqlite());
```
- **Where the settings ask, once:** the root of the chain, at its first use (today's `Lazy` at `actor/setting/this.cs:59`):
  `context.App.Code.Get<IStore>().Provider!.Open(path, memory, context)`. The root holds it as it holds the sqlite
  store today, and every layer reaches it through `Root`.
- **What stays the same:**
  - the path `/.data/setting/data.sqlite`;
  - in memory under test, by app id (`setting-{App.Id}`): the `memory` func is handed in unchanged;
  - `Dispose` through the root.
- **Order at birth:** `Code.RegisterDefaults()` runs at `app/this.cs:337`, before the first settings write
  (`:351`) and before `Load`. The settings' first store use is after it, so the default is in place.
- **Swappable with `code.load`:** a provider loaded and set default before the root's first use is the one asked. After
  that the root keeps what it opened, the same rule as any provider already handed out.
- **So a goal can slot in later (nothing built):** `Open` takes no settings of its own. A goal-backed store's own
  settings come from the default sqlite store (Ingi's example): the root asks `IStore` once, and a provider whose
  configuration lives in settings reads it from a store that isn't itself. That later branch has to break that loop
  (the default sqlite is always the settings' own store, or the provider is asked with the settings it may read).
  The interface needs no change for either.
- **The app's own store** (`app/this.cs:355`) builds sqlite the same way. Same door, one line. In or out of 266? My
  lean: in, since it's the same smell. I'll ask before touching it.

## 5. The nits on 26a05f2c8

1. **`Find` answers `data<setting>`:** the CLI asks its typed value, no cast. (`Find(path) : data.@this<setting.@this>`.)
2. **The setting answers the address:** `setting.Option(path)` answers the option. `setting.Address(option)` answers
   `Path` for the setting's own (no option) and `Path.option` otherwise. The `""` sentinel becomes a null option
   (`data<text>` uninitialized for the setting's own path) and the CLI's concatenation goes. Glued-name check:
   `setting.Address` navigates.
3. **Rows stay one per option:** this run's values are per option address (a flag or a step writes one option), and the
   saved rows are one whole setting per row. `Flatten` is the store's own storage for a dict at the crossing and stays.
   `Set`'s `is IDictionary` / `as data` stays: the CLI's raw dict is the one crossing.

## 6. Test moves

- `Of<T>()` callers in tests (56) → `await Get<T>()`, by script (PLang.Tests).
- The node-class tests (`SettingNodeTests`, 3 + the module's) move to the owner's address: `Get(action, option)` reads
  the action's member, then the module's.
- `Clone_Isolates_Writes` → two scopes over one parent.
- `SettingCacheTests` stays: it pins 258's keep, and a write clearing it (a red to add: a write under an address makes the
  next `Get<T>` new, a write elsewhere keeps it).
- Red first per piece: `Get<T>` keeps (the same instance across two reads, a new one after a write);
  the store is asked through `IStore` (a test provider registered as default is the one the settings open).

## 7. Commit order

1. Nits 1–2 (`Find` typed, `setting.Address`): small, on what's there.
2. 266: `IStore` + `Sqlite` default + the root asks it (red: a test provider is opened).
3. 258: `Get<T>` (async) with the keep folded in. `Of<T>` → `Get<T>` across prod (28 + the 2 makers) and tests.
4. 149 (4): the node classes go; `Option(row, name)`, `NotASettingClass`, `Own` and `Clone` with them. Re-pin the surface.
5. Each one: sweep + plang, then a report.
