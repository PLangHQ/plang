# Stage 3: Storage Binding — `Actor.@this.Permission`

**Goal:** Build the actor-scoped permission view (`Actor.@this.Permission`) that unifies in-memory and persisted grants behind one surface. Stage 2 lands `Path.Authorize`, which consults this view via `Context.Actor.Permission`.

**Scope:**
- `Actor.@this.Permission` body: per-actor in-memory list, query/write to the shared `permission` table in `App.SettingsStore` filtered by the actor field. `Find / Add / Revoke`.
- Wiring: `Actor.@this` gains a `Permission` property reachable from a Path's Context (`path.Context.Actor.Permission`).
- Signature verification with per-Data caching.

**Excluded:**
- `Path.Authorize` itself — stage 2.
- IPLangFileSystem v2 — stage 4.
- Final consent UI / template — stage 5.
- Apps-without-identity edge case — verified in stage 5.

## Deliverables

- **`Actor.@this.Permission`** — a new typed view on each actor instance. Its API:
  - `Find(Path path, Verb verb)` — returns a matching valid signed `Data<Permission>`, or null. Consults the actor's in-memory list, then runs a SQL query against the `permission` table filtered by this actor's kind (`json_extract(data, '$.Value.Actor') = '<actor>'`) + a coarse path-prefix prune. Validates signatures and runs `Covers` over candidates.
  - `Add(Data<Permission> signed)` — routes by signature expiry. Short expiry (Session, "y") → adds to the in-memory list (discarded at process exit). Long expiry (Always, "a") → calls `App.SettingsStore.Set("permission", key, signed)` where `key` = the Permission's `Path` field.
  - `Revoke(Data<Permission> grant)` — removes from in-memory list or from sqlite, whichever holds it.

  Each actor instance has its own `Permission` property. Different actors (system / user / service) maintain independent in-memory lists and filter persisted rows by their own actor kind.

- **`Actor.@this.Permission`'s home.** New folder `PLang/App/Actor/Permission/` with `this.cs` holding the view class. Wired into `Actor.@this` as `public Permission.@this Permission { get; }`.

- **Schema:** the `permission` table is auto-created by `IStore.Set` on first write (`Sqlite.cs:305-311` already does this with `(key TEXT PRIMARY KEY, data TEXT)`). Two-column rule preserved. No manual migration.

- **Signature verification caching.** A non-serialized flag on the Data instance records verification outcome. Invalidated when the Data is replaced.

- **Tests** (C# under `PLang.Tests/App/FileSystem/PermissionTests/`):
  - Round-trip: add a signed "a" grant for the user actor, find it back via `user.Permission.Find`, validate.
  - Per-actor isolation: add a "user" grant and a "system" grant; `user.Permission.Find` returns only the user one, `system.Permission.Find` only the system one.
  - Two-home unification: add a session grant (no/short expiry, in-memory), add a persisted grant; `Find` returns the right one for each query.
  - Verb narrowing: a "full allow" grant covers a `file.read`'s narrowed request; a "Read only" grant does NOT cover a `file.delete`'s narrowed request.
  - Glob matching: glob grant matches exact-path request; non-matching glob does not.
  - Revocation: in-memory revoke removes from session list; persisted revoke removes the row.
  - Invalidation: corrupted signature → `Find` skips it.

## Dependencies

- Stage 1 (`Permission` record with `AppId/Actor/Path/Match/Verb`; `Verb.@this` with Read/Write/Delete; `Match` enum; `Covers` methods).
- Existing `App.SettingsStore` (`IStore.Set(table, key, data)` — note: `key`, not `id`; `Set`, not `AddOrUpdate`).
- Existing signing infrastructure (`Data.Signature` round-trips through `App.Channels.Serializers.Serializer.Plang.Data` — confirmed at line 9 of that file).
- Existing `Actor.@this` class (this stage adds the `Permission` property to it).

## Design

Full storage design lives in [v1/plan/storage.md](v1/plan/storage.md). Permission-record semantics in [v1/plan/permission-design.md](v1/plan/permission-design.md). Coder reads both.

### Three things to get right

1. **Two homes unified behind one `Find`.** Session ("y", short expiry) and persisted ("a", long expiry) are routed internally by `Add` based on signature expiry. `Find` walks both. Callers don't differentiate.

2. **Per-actor scoping via JSON filter, not via tables.** One `permission` table in `App.SettingsStore`. Each actor's `Permission` filters its queries to its own actor kind via `json_extract(data, '$.Value.Actor') = '<actor>'`. Adding new actor kinds (unlikely — only system/user/service exist) is a non-event.

3. **Per-kind keying lives close to the kind.** For `Permission` the natural key is the **path** itself. Granting the same path twice overwrites — idempotent. Glob grants (`/apps/*/file.txt`) and exact grants (`/apps/Email/file.txt`) are different keys, coexist naturally. If future permission kinds plug into the same table, they bring their own `Key` rule on the record.

### What stage 3 does NOT do

- Doesn't implement any FS IO — stage 4.
- Doesn't render the prompt UI — channel renders the `Ask`'s Question (stage 2a); answer parsing lives in `Path.Authorize` (stage 2b).
- Doesn't sign — signing happens in `Path.Authorize` (stage 2b). Stage 3 stores already-signed Data and routes by expiry.
- Doesn't add indexed columns or generated columns on the `permission` table. v1 uses `json_extract` scans; indexes are v2 work if scale demands.
- Doesn't snapshot in-memory grants. Known limitation: if the App is paused via snapshot mid-flow, "y" grants are lost — user re-prompts on resume. Acceptable for v1.

## Acceptance

- C# tests in `PLang.Tests/App/FileSystem/PermissionTests/` cover all the cases listed in Deliverables.
- Existing `dotnet run --project PLang.Tests` stays green.
- `user.Permission.Add(grant)` then `user.Permission.Find(path, verb)` round-trips for both in-memory and persisted grants.
- Per-actor isolation verified.
- `path.Context.Actor.Permission` is reachable from any `Path` instance whose Context is wired (the normal runtime state).
