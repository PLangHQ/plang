# Permission Design — `FilePermission` Record + `Covers`

## Folder layout

```
PLang/App/FileSystem/Permission/
  this.cs              -- the FilePermission record lives here. The actor-side typed view
                          (Actor.@this.Permission) is wired in stage 3 but its body lives near Actor,
                          not here. This folder owns the data shape and its matching logic only.
  Verb/
    this.cs            -- Verb @this: composes Read/Write/Delete coverage
    Read.cs            -- record Read(bool Recursive = true, bool Metadata = true)
    Write.cs           -- record Write(bool Create = true, bool Overwrite = true,
                                       bool Append = true, bool Mkdir = true)
    Delete.cs          -- record Delete(bool Recursive = true, bool Permanent = true)
```

Namespace: `App.FileSystem.Permission`. Singular. The record is named `FilePermission` (not `Permission`) because there is no shared abstract `Permission` base across kinds; each subsystem owns its own independent record.

## The record — pure data

The record has four fields:

- `AppId` — which app this grant applies to.
- `Actor` — which actor kind holds the grant: `"system"`, `"user"`, or `"service"`.
- `Path` — the path or path pattern this grant covers.
- `Match` — `Exact`, `Glob`, or `Regex`. Determines how `Path` is interpreted when matching.
- `Verb` — `Verb.@this` containing the Read, Write, Delete sub-records.

`Actor` lives on the record, not on a separate column or table — the record is self-describing, the signature covers it, and per-actor scoping at query time uses JSON extract on this field.

The record has **one method only: `Covers(FilePermission request)`**. No `Check`, no `Describe`, no `Request`, no static factories. `Covers` is a pure function: no external state, no I/O, no exceptions.

Conceptually `Covers` checks:
- AppId matches between grant and request.
- Actor matches between grant and request.
- The grant's Path pattern covers the request's specific Path (under the grant's Match mode).
- The grant's Verb covers the request's Verb (each Read/Write/Delete sub-option subset check).

## Same record for grant and request

`grant.Covers(request)` is the only matching operation. Both arguments are `FilePermission`. The asymmetry is encoded in `Match`:

- **A grant** typically has `Match.Glob` (or `Regex`) and broad verb coverage. Example shape:
  - `AppId = "messages-app-id"`, `Actor = "user"`, `Path = "/apps/*/system.sqlite"`, `Match = Glob`, `Verb = full-allow`.
- **A request** has `Match.Exact` and a narrowed verb that asks only for what the operation needs. Example shape for a `file.read` of `/apps/Email/system.sqlite`:
  - `AppId = "messages-app-id"`, `Actor = "user"`, `Path = "/apps/Email/system.sqlite"`, `Match = Exact`, `Verb = Read-only narrowed`.

One record, two roles, one matching rule. No parallel `Permission` vs `PermissionRequest` types.

## The Verb @this

`Verb.@this` composes the three variant records — `Read`, `Write`, `Delete` — each defaulted to "fully granted":

- `Verb.@this.Read` defaults to `Read(Recursive: true, Metadata: true)`.
- `Verb.@this.Write` defaults to `Write(Create: true, Overwrite: true, Append: true, Mkdir: true)`.
- `Verb.@this.Delete` defaults to `Delete(Recursive: true, Permanent: true)`.

All three variants are always present. Narrowing is an explicit record copy with sub-options set to false.

`Verb.@this.Covers(request)` delegates to each variant: `grant.Read.Covers(request.Read)` && `grant.Write.Covers(request.Write)` && `grant.Delete.Covers(request.Delete)`.

## The variant records — `Covers` semantics

Each variant owns its own `Covers` rule, all reading the same way: *"if the request needs feature X, the grant must have X."* Expressed as `(!request.X || grant.X)` per sub-option.

The trick: when the request doesn't need a feature (sub-option is false), the check trivially passes — `!false || anything = true`. This means a "fully granted" grant covers any properly-narrowed request, because the request's false sub-options short-circuit.

Concretely: a grant with all verbs full-allow covers a `file.read` request that asks for `Read(Recursive: true, Metadata: true)` and explicitly empty `Write/Delete` (all sub-options false). The Read check passes because the grant has the needed features; the Write/Delete checks pass because the request asks for nothing in those variants.

## Constructing the request — narrowing happens inside Path.CheckPermission

`file.read` doesn't construct a Verb.@this. It calls `path.CheckPermission(Verb.Read)` with just the verb kind. Inside `CheckPermission`, Path builds the properly-narrowed request: full Read sub-options, all-false Write, all-false Delete. The narrowing logic lives in exactly one place; action handlers stay simple.

No `Verb.@this.ReadOnly()` / `WriteOnly()` static factories on Verb — those would multiply combinatorially as soon as someone needs combined narrowings, and they hide what's actually being constructed. Inline construction inside CheckPermission keeps it explicit and contained.

## Who finds the grants — `actor.Permission`

The `FilePermission` record doesn't find matching grants — that's not its job. Storage and lookup live on a typed view bound to each actor:

- `Actor.@this.Permission` is the actor's permission view. Each actor instance (system / user / service) has its own.
- `actor.Permission.Find(request)` returns the first matching valid signed grant, or null. Internally consults the actor's in-memory list (for "y" grants) and `App.SettingsStore` (for "a" grants).
- `actor.Permission.Add(signed)` routes by signature expiry: no expiry → in-memory; long expiry → sqlite.

`FilePermission` is consumed by `actor.Permission` (which decides what counts as a match by asking each candidate grant `grant.Covers(request)`).

Path's `CheckPermission(Verb.X)` calls `path.Context.Actor.Permission.Find(request)`. Path orchestrates two layers: own `Properties` cache, then `actor.Permission`. See storage.md and runtime-flow.md for the full flow.

## What the verbs mean in operations

- **`mkdir`** is `Write` (with `Mkdir = true`). Creating a directory is making something exist.
- **`rmdir`** is `Delete`.
- **Rename / move** is *two checks*: `Read` on source + `Write` on destination, batched into one request. The FS method decides which verbs it needs and uses the batched check (see `filesystem-surface.md`).
- **Copy** is `Read` on source + `Write` on destination parent. Batched check.
- **Stat / exists** is `Read` with `Metadata = true`. The `Content` distinction (may stat, may not read content) is deliberately not in the model — when it's needed, add a `Content` boolean to `Read` as a separate pass.

## On-the-wire and at-rest shape

A signed grant — for the Messages app reading every app's `system.sqlite`, granted by the user — is just a Data with a typed Value and a signature:

```
Data {
  value:     FilePermission {
                appId: "messages-app-id"
                actor: "user"
                path:  "/apps/*/system.sqlite"
                match: "glob"
                verb:  { read, write, delete records }
             }
  signature: <signing.Signature>
}
```

Stored as `{ id: <uuid>, data: <serialized Data> }` in the `permission` table.

No nested Data, no wrapper around the request, no special permission shape — just a Data with a typed value and a signature, in a row.

## What's NOT in scope here

- **The signing mechanism** — uses the existing `signing.sign` action and `signing.Signature` type. New code attaches to existing rail.
- **The prompt UI** — covered in `runtime-flow.md` (Ask marker + `error.handle`'s built-in path + templates).
- **Where the grant physically lives** — see `storage.md`.
- **The Code routing for goal-backed virtual filesystem** — parked.
- **App-side cascade for *requested* verb config** — out of scope entirely (see plan.md "Out of scope").
