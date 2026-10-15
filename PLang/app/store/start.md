# The app store: a key → Data map on SQLite

The app store is where an app keeps durable state of its own: the setup steps it
has run, and the LLM response cache. This page is for the C# developer who needs
to know what the store is, which tables live in it, and when its file is opened.
Read it before you add a table or change how a table is read.

```
app.store.@this          PLang/app/store/this.cs          namespace app.store
app.store.sqlite.@this   PLang/app/store/sqlite/this.cs   namespace app.store.sqlite
app.@this.store          PLang/app/this.cs                the one instance
```

> **Scope.** The store holds rows of `Data`. It is not the step `Cache`
> (`app.module.cache`, in memory by default), and it is not the settings store
> (§4).

---

## 1. One store per app

`app.store` is one instance, built by the app's constructor
(`PLang/app/this.cs`, the `store = new global::app.store.sqlite.@this(...)`
line). Every actor reaches the same instance, so `System` and `User` share it.

The database is `.data/data.sqlite` under the app root. While the app is in test
mode, the database is in memory instead, under the name `system-{app.Id}`. Each
test app therefore gets its own database: shared-cache in-memory databases with
one name would merge.

---

## 2. The surface: an abstract store

The type is the abstract class `app.store.@this`. There is no `IStore`
interface. The sqlite kind is its only subclass. Every verb is a `Task` that
answers `Data`, and none throws.

| Verb | Answers | Behaviour |
|---|---|---|
| `Get<T>(table, key)` | `data.@this<T>` | the stored value as `T`; a null citizen (`IsNull`) when the key is missing |
| `GetAll<T>(table)` | `data.@this<list>` | every value in the table, each forced to `T` |
| `Set(table, key, data)` | `data.@this` | stores the whole Data: value, type and signature |
| `Remove(table, key)` | `data.@this` | removes the key; succeeds when the key was absent |
| `Exists(table, key)` | `data.@this<bool>` | whether the key is present |
| `Tables()` | `data.@this<list>` | the table names, sorted |
| `Dispose()` | `void` | releases the database |

`Get<T>` and `GetAll<T>` require `T : item, ICreate<T>`, so a caller names the
plang type it expects. There is no untyped get.

> **Errors answer; they do not throw.** Each sqlite verb catches `AppException`
> and returns its `Error`, and catches any other exception and returns it as a
> `SettingsError`. Table names are sanitized to letters, digits and `_`,
> lower-cased. An empty name becomes `default_table`.

---

## 3. The kind: sqlite

`app.store.sqlite.@this` is the one kind (`Kind => "sqlite"`). Its storage shape:

- One table per name, two columns: `key TEXT PRIMARY KEY, data TEXT`.
- `data` is a whole `Data` in plang's own wire format (`application/plang`),
  written with `View.Store`. Members tagged `[Store]` persist, and the value's
  type and signature come back with it.
- Every verb except `Tables()` first runs `CREATE TABLE IF NOT EXISTS`. A `Get`
  on a new table therefore creates the table.
- A connection is opened per operation. The database runs in WAL journal mode.

---

## 4. Who owns tables in it

The store owns no tables. Its owners name the tables they use:

| Table | Owner | Key | Row |
|---|---|---|---|
| `setup` | `app.goal.setup` (`PLang/app/goal/setup/this.cs`) | `step.Hash` | a `Data` of metadata: goal path, step index and text, `executedAt`, error |
| `LlmCache` | the OpenAI module (`PLang/app/module/llm/code/OpenAi.cs`) | the cache key computed from the request | the cached response `Data` |

Setup calls `Exists` to skip a step that has already run, and `Set` to record one.
The LLM module calls `Get` on a cache hit and `Set` after a call. It does both
only when the action's cache is `use` and the call has no tools.

> **Settings are not in this store.** `app.actor.setting` keeps its rows in a
> separate database, `.data/setting/data.sqlite`, table `settings`
> (`PLang/app/actor/setting/this.cs`). The settings root makes it on first use.

---

## 5. Lazy open

Constructing the store opens nothing. The constructor keeps the file path, the
in-memory name function and the context. The first verb calls `Opened()`, which
runs `Open()` once under a lock. `Open()` decides the connection string,
authorizes the write, creates the parent directory, and enables WAL. Every later
verb awaits that same task.

Two consequences:

- An app that never calls a verb never creates `.data/` or the database file.
- A failed open is kept. `_opened` holds the faulted task, so every later verb
  sees the same failure until the app restarts.

`Dispose()` closes the in-memory sentinel connection, and clears the connection
pool only if the open completed. A store that never opened holds nothing.

---

## Find the source

| You want | Source |
|---|---|
| the store's verbs and their contract | `PLang/app/store/this.cs` |
| the table layout, the open, the row format | `PLang/app/store/sqlite/this.cs` |
| where the store is built, its path and its test name | `PLang/app/this.cs` (the `store = new` line) |
| the setup table | `PLang/app/goal/setup/this.cs` |
| the LLM cache table | `PLang/app/module/llm/code/OpenAi.cs` (`CacheTable`) |
| the settings database | `PLang/app/actor/setting/this.cs` |

---

## The tree from here

| Page | Covers |
|---|---|
| `../start.md` | the app root; `store` is one of its members (§5) |
| `../module/start.md` | the module system, whose LLM and file actions reach the store |
