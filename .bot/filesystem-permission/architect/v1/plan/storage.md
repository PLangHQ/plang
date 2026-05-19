# Storage — Where Signed Grants Live

## Where grants live

In **`App.SettingsStore`** — the existing `IStore` instance backed by `<AppRoot>/.db/system.sqlite`. Same store Settings uses. New table: `permission`.

`Permission/@this` (an actor-scoped typed view that we'll see in stage 3) reads and writes this table via the existing `IStore.Set / GetAll / Remove` API. There is no new persistence layer.

## Two-column rule — `id` and `data` only

Every actor-scoped storage table in PLang follows the same shape:

```
id   TEXT PRIMARY KEY
data TEXT                  -- serialized Data (Value, Signature, everything)
```

No additional columns ever. The schema is locked at two columns; nothing else exists at the SQL level. The meaning of what's stored lives in the Data, not in extra columns.

Why this matters:

- **No migrations.** Adding a field to FilePermission (e.g. a new verb sub-option) doesn't touch SQL. Only C# changes.
- **Generic storage layer.** `IStore` knows nothing about FilePermission, AppId, actor kinds, or expiry. It stores Data objects in tables; that's all.
- **Different data kinds coexist cleanly.** Same shape works for settings, permissions, and any future actor-scoped thing.

For content-based queries (filter by JSON path), SQLite's `json_extract` works in WHERE clauses. If volume hits where scans get slow, generated columns + indexes on JSON paths can be added later without changing the row shape. Filed as a v2 concern.

## On-disk row shape

```
id:   <uuid>
data: <serialized Data — Value (FilePermission record including actor field), Signature, etc.>
```

UUID as row key — simple, random, no parsing logic. `AddOrUpdate` writes by id; `Remove` deletes by id.

## Actor lives in the data, not in the schema

The `FilePermission` record carries the actor as a field:

```
FilePermission(string AppId, string Actor, string Path, Match Match, Verb Verb)
```

Where `Actor` is one of `"system" | "user" | "service"`. There are only three actor kinds; no arbitrary per-actor IDs.

The signature signs the FilePermission record (signs Value), so the actor field is tamper-evident. Settings the actor on a grant after signing would invalidate the signature.

Per-actor scoping is enforced at query time, not at schema time:

```
WHERE json_extract(data, '$.Value.Actor') = 'user'
```

One `permission` table, all actors. Each actor's `Permission` typed view knows its own actor kind and filters its queries to its own rows. This matches the broader observation that Settings should be actor-scoped — the same approach will work for it: one `settings` table, actor as a JSON field, queries filter per actor.

## Two homes by lifetime — both behind one `actor.Permission`

Grants come in two flavours by user choice:

| Choice | Signature expiry | Storage |
|--------|------------------|---------|
| y (session only) | no expiry | in-memory on `actor.Permission` |
| a (always)       | long expiry | sqlite `permission` table |

`actor.Permission` is the typed thing on each actor that unifies both. Internal routing by signature expiry: no expiry → keep in-memory list; long expiry → hand to `App.SettingsStore`. The consumer (Path.CheckPermission) just asks `actor.Permission.Find(request)` — doesn't know or care which home held the answer.

Why in-memory for "y": "session only" means the grant should not survive process exit. SQLite is persistent; storing "y" there would either lie about its lifetime or require cleanup at startup. Simpler to just hold it in memory on the actor, where it naturally dies with the process.

Why one type that unifies both: the consumer asks once. Splitting the lookup across two layers in calling code (transaction script) puts orchestration where it doesn't belong.

## Path's lookup chain

`path.CheckPermission(Verb.X)` walks two layers, cheapest first:

1. **Path's own `Properties["permission"]` cache** — per-variable. If a previously-resolved grant for this verb is here and still valid, return it.
2. **`actor.Permission.Find(request)`** — one delegation, regardless of whether the matching grant lives in-memory or sqlite. `actor.Permission` internally walks its in-memory list, then queries `App.SettingsStore`. Match found → grant returned. No match → null.

On hit at layer 2, Path caches the grant on its own `Properties` so layer 1 catches it on subsequent calls against the same variable.

On a miss at both: Path returns `Data.Fail(new FilePermissionAsk(...))`.

## How `actor.Permission` queries the store

For a request `(absolutePath, Verb.X)`:

1. SQL prefilter:
   ```
   SELECT data FROM permission
     WHERE json_extract(data, '$.Value.Actor') = <actor kind>
       AND json_extract(data, '$.Value.Path')  LIKE <coarse prefix from absolutePath>
   ```
   The actor filter is exact; the path prefix is a coarse prune for exact-match grants and prefix-style glob patterns. Returns a small candidate set.

2. For each candidate row: deserialize to `Data<FilePermission>`. Validate signature. Run `grant.Value.Covers(request)` (full path matching including glob/regex, verb sub-option coverage).

3. First grant that passes all checks → return. None pass → null.

Verb sub-option coverage and full pattern matching stay in C# — they are too complex for clean SQL.

## Signature verification

When `actor.Permission.Find(...)` returns a grant, the caller asks the Data whether the signature is valid. Verification result is cached per-Data instance so repeated checks in the same session are cheap.

## Revocation

`permission.revoke` PLang action goes through `actor.Permission.Revoke(grant)`:

- For in-memory grants: remove from the actor's in-memory list.
- For persisted grants: `App.SettingsStore.Remove("permission", id)`.

After revocation, the next `path.CheckPermission(...)` finds no matching grant and returns `Data.Fail` with an Ask.

## Snapshot / restore

Persisted grants live in sqlite — snapshot of the App's `.db/system.sqlite` captures them. In-memory grants are part of the actor's runtime state and ride the existing actor snapshot path.

(See foundation-verify TODO from 2026-05-11: "End-to-end PLang tests for full-app Snapshot save+restore round-trip.")

## Scaling — known limitation

`json_extract` filtering plus full-scan-of-candidates handles tens to hundreds of grants per actor comfortably. At thousands+, the scan starts to matter. Two natural moves when that hits:

- **Generated column + index on `Value.Actor`.** Per-actor lookups become O(log n) instead of full scan with JSON extract on each row.
- **Generated column + index on a path-prefix.** Pre-filter exact-match grants by indexed prefix.

Both are post-v1 optimizations. The 2-column rule still holds — generated columns are derived from the existing `data` column, not new schema. `actor.Permission.Find`'s interface doesn't change.

## What this does NOT do

- **Doesn't define a new persistence layer.** `App.SettingsStore` is what's already there.
- **Doesn't encrypt grants.** Decoupled from the `Settings encryption-at-rest` TODO. Permission signing is integrity; encryption is confidentiality.
- **Doesn't define an audit log.** Could be added later as a parallel table; not in scope here.
- **Doesn't use the Variables system.** Permissions live in `IStore`, not in the in-memory Variables tree.
- **Doesn't refactor Settings to be actor-scoped.** Settings should be (your broader observation), but that's a separate branch. This branch establishes the pattern (actor as a field, JSON filter) that Settings can later adopt.
