# Stage 3: Storage binding — `Actor.@this.Permission`

**Goal:** The actor-scoped permission view that unifies in-memory and persisted grants behind one `Find / Add / Revoke` surface. `Path.Authorize` (stage 2b) consults it via `Context.Actor.Permission`.

## Out of scope

- `Path.Authorize` itself — stage 2b.
- `IPLangFileSystem` v2 — stage 4.
- Consent UI / template — stage 5.

## Deliverables

### 1. `Actor.@this.Permission` — new typed view

Lives at `PLang/App/Actor/Permission/this.cs`. Wired into `Actor.@this` as `public Permission.@this Permission { get; }`. Each actor instance has its own; user / system / service are independent.

API:

- **`Find(Path path, Verb verb) → Data<Permission>?`** — walks the actor's in-memory list, then queries the shared `permission` table filtered by the actor's kind (`json_extract(data, '$.Value.Actor') = '<actor>'`) plus a coarse path-prefix prune. Validates signatures and runs `Covers` over candidates. Returns null if nothing covers.

- **`Add(Data<Permission> signed)`** — routes by presence of signature expiry:
  - **No expiry** (session, `"y"`) → adds to the in-memory list. Lives as long as the App lives. No timestamp comparison at lookup.
  - **Expiry set** (always, `"a"`) → calls `App.SettingsStore.Set("permission", key, signed)` where `key` = the Permission's `Path` field.

- **`Revoke(Data<Permission> grant)`** — removes from whichever home holds it.

### 2. Schema

The `permission` table is auto-created by `IStore.Set` on first write (`Sqlite.cs:305-311` does this with `(key TEXT PRIMARY KEY, data TEXT)`). Two-column rule preserved. No manual migration.

### 3. Signature verification caching

A non-serialized flag on the Data instance records the verification outcome — Find walks the same Data across multiple lookups and shouldn't re-verify each time. Invalidated when the Data is replaced.

## Three things to get right

1. **Two homes unified behind one `Find`.** Session ("y", no expiry, in-memory) and persisted ("a", expiry set, sqlite) routed internally by `Add` based on signature expiry. `Find` walks both. Callers don't differentiate.

2. **Per-actor scoping via JSON filter, not via tables.** One `permission` table. Each actor's view filters by its kind via `json_extract`. Adding new actor kinds is a non-event.

3. **Per-kind keying lives close to the kind.** For `Permission` the natural key is the path. Granting the same path twice overwrites — idempotent. Glob grants and exact grants are different keys; coexist naturally. Future permission kinds bring their own `Key` rule on the record.

## What this stage does NOT do

- No FS IO — stage 4.
- No prompt UI — channel renders `Ask.Value` (stage 2a); answer parsing in `Path.Authorize` (stage 2b).
- No signing — `Path.Authorize` (stage 2b) signs; stage 3 stores already-signed Data.
- No indexed/generated columns — v1 uses `json_extract` scans. v2 work if scale demands.
- No snapshot of in-memory grants. **Known limitation:** if the App is snapshotted mid-flow, "y" grants don't survive snapshot/restore — user re-prompts. Acceptable for v1.

## Tests

C# under `PLang.Tests/App/FileSystem/PermissionTests/`:

- **Round-trip:** add a signed "a" grant for the user actor; `user.Permission.Find` returns it; signature validates.
- **Per-actor isolation:** add a user grant and a system grant; each actor's `Find` returns only its own.
- **Two-home unification:** add an in-memory grant and a persisted grant; `Find` returns the right one for each query.
- **Verb narrowing:** a full-allow grant covers a narrowed request; a Read-only grant does NOT cover a Delete request.
- **Glob matching:** glob grant matches an exact-path request; non-matching glob does not.
- **Revocation:** in-memory revoke removes from session list; persisted revoke removes the row.
- **Signature failure:** corrupted signature → `Find` skips it.

## Dependencies

- Stage 1 — `Permission` record, `Verb`, `Match`, `Covers` methods.
- Existing `App.SettingsStore` (`IStore.Set(table, key, data)`).
- Existing signing infrastructure (`Data.Signature` round-trips through `App.Channels.Serializers.Serializer.Plang.Data:9`).
- Existing `Actor.@this` class (this stage adds the `Permission` property).

## Acceptance

- All test cases above pass.
- `dotnet run --project PLang.Tests` zero regressions.
- `path.Context.Actor.Permission.Add(g)` then `.Find(path, verb)` round-trips for both in-memory and persisted grants.
- Per-actor isolation verified.
