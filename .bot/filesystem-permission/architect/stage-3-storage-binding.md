# Stage 3: Storage Binding — `Actor.@this.Permission` + `Path.CheckPermission`

**Goal:** Build the actor-scoped permission view (`Actor.@this.Permission`) that unifies in-memory and persisted grants behind one surface, plus the `Path` method that consumes it (`path.CheckPermission(Verb.X)`). Stage 3 lands both halves so stage 4's FS layer has a clean `Path`-side surface to call.

**Scope:**
- `Actor.@this.Permission` body: per-actor in-memory list, query/write to the shared `permission` table in `App.SettingsStore` filtered by the actor field. `Find / Add / Revoke`.
- `path.CheckPermission(Verb.X)` method on `App.FileSystem.Path`. Takes just the verb kind (`Verb.Read` / `Verb.Write` / `Verb.Delete`). Constructs the properly-narrowed request internally. Returns `Data.Ok` (matching grant exists or path in-root) or `Data.Fail(FilePermissionAsk(...))`.
- Signature verification with per-Data caching.
- Wiring: `Actor.@this` gains a `Permission` property reachable from a Path's Context.

**Excluded:**
- IPLangFileSystem v2 — stage 4. Stage 3 provides the `path.CheckPermission` method that stage 4's FS operations call.
- Final template — stage 5.
- Apps-without-identity edge case — verified in stage 5.

## Deliverables

- **`Actor.@this.Permission`** — a new typed view on each actor instance. Its API:
  - `Find(absolutePath, Verb verb)` — returns a matching valid signed grant, or null. Consults the actor's in-memory list, then runs a SQL query against the `permission` table filtered by this actor's kind (`json_extract(data, '$.Value.Actor') = '<actor>'`) + a coarse path-prefix prune. Validates signatures and runs `Covers` over candidates.
  - `Add(Data<FilePermission> signed)` — routes by signature expiry. No expiry → adds to the in-memory list (will be discarded at process exit). Long expiry → calls `App.SettingsStore.Set("permission", uuid, signed)`.
  - `Revoke(Data<FilePermission> grant)` — removes from in-memory list or from sqlite, whichever holds it.

  Each actor instance has its own `Permission` property. Different actors (system / user / service) maintain independent in-memory lists and filter persisted rows by their own actor kind.

- **`Path.CheckPermission(Verb verb)`** — the consumer-facing entry. Path:
  1. Resolves itself to absolute form (`Path.Absolute`, already exists).
  2. Returns `Data.Ok` immediately if the absolute is in-root.
  3. Consults `path.Properties["permission"]` cache; if a previously-found valid grant is there, returns `Data.Ok`.
  4. Constructs a narrowed request: full sub-options on the asked verb (`Verb.Read`/`Write`/`Delete`), all-false on the other two verb variants.
  5. Asks `path.Context.Actor.Permission.Find(absolute, request)` for a covering grant.
  6. On hit, caches the grant in `Properties` and returns `Data.Ok`.
  7. On miss, returns `Data.Fail(new FilePermissionAsk(new FilePermission(appId, actor, absolute, Match.Exact, narrowedVerb)))`.

- **Schema migration** for `App.SettingsStore`: the `permission` table doesn't exist today. First write creates it with the standard 2-column shape (`id TEXT PRIMARY KEY, data TEXT`). `IStore` already handles auto-create on first write to a new table; verify and rely on that.

- **Signature verification caching.** A non-serialized flag on the Data instance records verification outcome. Invalidated when the Data is replaced.

- **Tests** (C# under `PLang.Tests/App/FileSystem/PermissionTests/`):
  - Round-trip: add a signed "a" grant for the user actor, find it back via `user.Permission.Find`, validate.
  - Per-actor isolation: add a "user" grant and a "system" grant; `user.Permission.Find` returns only the user one, `system.Permission.Find` only the system one.
  - Two-home unification: add a session grant (no expiry, in-memory), add a persisted grant; `Find` returns the right one for each query.
  - `path.CheckPermission` in-root returns `Ok` without touching the store.
  - `path.CheckPermission` out-of-root with cached grant returns `Ok` from cache.
  - `path.CheckPermission` out-of-root with no cache, store hit, returns `Ok` and populates cache.
  - `path.CheckPermission` out-of-root with no match returns `Fail` with `FilePermissionAsk` describing the missing permission.
  - Verb narrowing: a "full allow" grant covers a `file.read`'s narrowed request; a "Read only" grant does NOT cover a `file.delete`'s narrowed request.
  - Glob matching: glob grant matches exact-path request; non-matching glob does not.
  - Revocation: in-memory revoke removes from session list; persisted revoke removes the row.
  - Invalidation: corrupted signature → `Find` skips it.

## Dependencies

- Stage 1 (`FilePermission` record with `AppId/Actor/Path/Match/Verb`; `Verb.@this` with Read/Write/Delete; `Match` enum; `Covers` methods).
- Stage 2 (`Ask` marker + `FilePermissionAsk` concrete type; `error.handle`'s built-in path).
- Existing `App.SettingsStore` (`IStore`).
- Existing signing infrastructure (`Data.IsSignatureValid` or equivalent).
- Existing `App.FileSystem.Path` class (this stage adds the `CheckPermission` method to it).
- Existing `Actor.@this` class (this stage adds the `Permission` property to it).

## Design

Full storage design lives in [v1/plan/storage.md](v1/plan/storage.md). Permission-record semantics in [v1/plan/permission-design.md](v1/plan/permission-design.md). Coder reads both.

### Four things to get right

1. **Path owns the question; `actor.Permission` is the collaborator.** `path.CheckPermission(...)` is the consumer surface. Inside Path it asks `actor.Permission.Find(...)`. FS code in stage 4 reaches through Path, not through `actor.Permission` directly.

2. **Two homes unified behind one Find.** Session-only ("y", no expiry) and persisted ("a", long expiry) are routed internally by `actor.Permission.Add` based on signature expiry. `Find` walks both. Callers don't differentiate.

3. **Per-actor scoping via JSON filter, not via tables.** One `permission` table in `App.SettingsStore`. Each actor's `Permission` filters its queries to its own actor kind via `json_extract(data, '$.Value.Actor') = '<actor>'`. Adding new actor kinds (unlikely — only system/user/service exist) is a non-event.

4. **Narrowing happens inside `CheckPermission`, not on caller side.** Callers pass `Verb.Read` (the verb kind). Path constructs the full `Verb.@this` with Read full and Write/Delete all-false. Action handlers and FS methods never construct `Verb.@this`.

### What stage 3 does NOT do

- Doesn't implement any FS IO — stage 4.
- Doesn't render the prompt UI — `error.handle`'s built-in path (stage 2) does that.
- Doesn't sign — signing happens in `error.handle`'s built-in path. Stage 3 stores already-signed Data.
- Doesn't add indexed columns or generated columns on the `permission` table. v1 uses `json_extract` scans; indexes are v2 work if scale demands.

## Acceptance

- C# tests in `PLang.Tests/App/FileSystem/PermissionTests/` cover all the cases listed in Deliverables.
- Existing `dotnet run --project PLang.Tests` stays green.
- `path.CheckPermission(Verb.Read)` is callable from a test fixture; returns expected Data shape (Ok with cached grant in Properties, or Fail with FilePermissionAsk).
- `user.Permission.Add(grant)` then `user.Permission.Find(path, verb)` round-trips for both in-memory and persisted grants.
- Per-actor isolation verified.
