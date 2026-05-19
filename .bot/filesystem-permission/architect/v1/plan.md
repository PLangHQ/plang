# Filesystem Permission — Plan v1

## What this is

A PLang app today can't say "Messages may read Email's `system.sqlite`." The filesystem layer has a runtime-only `FileAccessControl` record populated by a yes/no/always prompt thrown from inside `ValidatePath` — no signature, no expiry, no audit, no cross-app story.

This branch replaces that with a signed `Data<FilePermission>` model where each grant carries the app's identity, the path pattern, the match mode, and the verb (with sub-options). The grant lives in the app's signed-data store, surfaces lazily on the variable that needs it (`%path%.Properties["permission"]`), and is enforced inside `IPLangFileSystem` itself — every operation method takes a `Path`, returns `Data`, and checks permission internally.

It also lands the runtime machinery for permission generally — not just filesystem. An action that needs user consent returns `Data.Fail` with an error implementing the `Ask` marker. PLang's existing `error.handle` is the umbrella — its built-in behaviour recognises Ask-marked errors and runs the consent/input flow: render via template per kind under `os/system`, user signs, store the signed grant, engine re-runs the action. Non-Ask errors take the normal `error.handle` path (user-configured handler, or propagate). Both kinds of asks — a free-text user-input question (`ask user "..."`) and a permission consent request — share the mechanism; both are errors whose reason implements `Ask`. HTTP and Payment permissions on follow-up branches use the same machinery.

## The pattern, named

Each subsystem that needs user consent for a privileged operation grows its own `permission/` folder. The folder's location depends on how that subsystem is structured in the C# tree — there is no single mandated parent path. This branch ships filesystem; other kinds follow when they ship.

This branch — filesystem permission lives next to the FS abstraction layer:
```
App/FileSystem/Permission/    ← here, because FS has its own non-module layer
                                (Path class, IPLangFileSystem, Default code)
```

Future kinds will pick the location that fits their existing structure. HTTP is a module (`App/modules/http/`), so its permission likely lives at `App/modules/http/permission/` when that branch lands. Payment is less settled — it might be its own module, might be transport material that doesn't have a local subsystem at all. Those decisions belong to those branches.

Each `permission/` folder, wherever it lives, owns:
- A pure-data Permission record (kind-specific fields — file has `Path/Verb`).
- A `Covers(grant, request)` method on the record. Both grant (broad — Match.Glob) and request (narrow — Match.Exact) use the same record; the asymmetry lives in the Match field and the matching algorithm, not in two different types.
- A storage view (`Permission/@this`) over the app's signed-data store for that kind.

There is no abstract base `Permission` class and no `IPermission` interface. Polymorphism in the type system would force every kind to share a shape it doesn't fit. The records are independent; the *pattern* is documented (in `good_to_know.md` or similar), not enforced by the compiler.

What's shared across kinds and lives once:
- `Ask` marker interface + built-in path inside `error.handle` (runs the consent/input flow for Ask-marked `Data.Fail`s).
- `Match` enum (`Exact | Glob | Regex`).
- The output.ask template mechanism under `os/system/permission/`.

## The runtime flow

A PLang developer writes:

```plang
- read /apps/Email/system.sqlite, write to %row%
```

The narrative:

1. Builder produces a `file.read` action call with `Path = "/apps/Email/system.sqlite"`.
2. Engine invokes `file.read`. The action handler is one line: `await fs.ReadText(Path.Value)`.
3. Inside `IPLangFileSystem.ReadText`: asks the Path itself for permission — `path.CheckPermission(Verb.Read)`. Path owns its own question.
4. Inside `path.CheckPermission`: Path consults its Properties cache first (`path.Properties["permission"]`); empty/invalid, so it asks the calling actor's `Permission` view (`path.Context.Actor.Permission`) for a matching grant. Found → returns `Data.Ok` and caches in Properties. Not found → returns `Data.Fail(new FilePermissionAsk(...))` describing what's missing.
5. `ReadText` propagates whatever Path returned. If Fail-with-Ask, that bubbles up. If Ok, `ReadText` proceeds to the BCL read.
6. Engine: `error.handle` runs. Its built-in path checks: does the error implement `Ask`? Yes → run the consent flow. No → fall through to user-configured handler or propagate.
7. Consent flow picks `os/system/permission/file.template`, renders the consent text, writes to the current actor's output channel.
8. User picks `y` / `n` / `a`. Choice maps to: `y` = sign with no expiry, kept in-memory only. `a` = sign with long expiry, persisted to sqlite. `n` = no signature produced.
9. Consent flow stores any signed `Data<FilePermission>` it produced, then reports outcome: **handled** (consent granted, grants written) or **not handled** (denial or fail).
10. Engine, back in its action-dispatch loop, branches on the outcome:
   - **handled** → re-run the same action with the same params. Action runs fresh; no continuation state passed between calls.
   - **not handled** → the original `Data.Fail` falls through to user-configured `error.handle` (or propagates if none).
11. Re-run attempt: `ReadText` resolves path, runs permission check; this time the store has a matching signed `Data<FilePermission>`, signature validates, `Covers` returns true. Reads file, returns `Data.Ok(content)`.

The action handler never sees permission logic. It calls `fs.ReadText(path)`, gets back a Data, returns it. The Data is either `Ok` or `Fail` — and on `Fail`, `error.handle`'s built-in path checks for the `Ask` marker and routes appropriately. Same shape for `file.save`, `file.delete`, `file.copy`, `file.move`, `file.list`.

For multi-path operations (move, copy), the FS method bundles its checks: calls a batch form that checks both paths' verbs up front, accumulates any missing permissions, returns one `Data.Fail` whose Ask carries the full list of FilePermissions needed. User sees one consent prompt covering everything. Fail-fast: if any permission is missing the operation doesn't run.

## Cross-cutting decisions

- **Asks are `Data.Fail` with an `Ask` marker, handled inside `error.handle`.** No new Data state, no parallel router. An action that needs user input or consent returns `Data.Fail` whose Error implements the `Ask` interface (or extends an `Ask` base type). `error.handle` recognises Ask-marked errors and runs the consent/input flow as its built-in behaviour. Non-Ask errors take the normal `error.handle` path (user-configured handler or propagate). Both `output.ask "what's your name?"` and `Permission` requests share the mechanism — they're different kinds of `Ask`.
- **Ask payload carries the full description of what's needed.** For permission: a single `FilePermission` (or a list of them for batched checks). For free-text input: the prompt text and any expected-answer hints. `error.handle`'s built-in path renders based on the Ask's concrete type.
- **Permission is on the variable, lazy from store.** When the FS check reads `path.Properties["permission"]` and finds empty/invalid, it asks `path.Context.Actor.Permission` for a matching grant; on hit, populates Properties. PLang developers can read `%path.permission%` like any other variable property.
- **Storage is `App.SettingsStore` with a 2-column rule.** Permissions live in a `permission` table in the existing `IStore` (sqlite at `<AppRoot>/.db/system.sqlite`). Every actor-scoped storage table uses the same shape: `id TEXT PRIMARY KEY, data TEXT` — no other columns ever. Schema is locked; meaning is in the Data.
- **Actor scoping via JSON filter, not via tables or columns.** The `FilePermission` record carries an `Actor` field (`"system" | "user" | "service"`). Per-actor lookups filter via `json_extract(data, '$.Value.Actor') = '<actor>'`. One table holds all actors' grants; each actor's `Permission` view filters to its own rows.
- **Two lifetime modes unified behind `actor.Permission`.** No-expiry signature = in-memory only on the actor instance, lives till process exit ("y"). Long-expiry = persisted in sqlite, survives restart ("a"). `actor.Permission.Add(signed)` routes internally based on the expiry. Consumer doesn't know which home was used.
- **`IPLangFileSystem` becomes Data-shaped.** Every method takes `Path`, returns `Data<T>` (or `Data`), with verb baked into the method (`ReadText` checks Verb.Read internally). No more `System.IO.Abstractions.IFileSystem` inheritance.
- **Absolute path matching.** All permission checks happen against the resolved absolute path, not the raw value.
- **Three permission scopes (user / system / service).** User and system are local — checked locally, stored locally, never travel on the wire. Service is remote — for outbound calls to a PLang server, the permission travels along with the outbound Data. This branch implements local (user/system); service work is the http/payment follow-up branches.
- **No wrapping.** Permissions don't wrap the original Data. Stored grants are plain `Data<FilePermission>` (typed value, signed). No `type="permission"` discriminator, no nested Data layers.
- **Signature on Data, signs over Value.** Existing convention. `Data<FilePermission>` has its `Signature` populated by the existing `signing.sign` pipeline. Permission carries the same shape on the wire as in the store.

## Stage index

| Stage | File | Status | Summary |
|-------|------|--------|---------|
| 1 | [stage-1-permission-types.md](../stage-1-permission-types.md) | pending | Pure types: `FilePermission` record, `Verb/@this` with `Read`/`Write`/`Delete` records, `Match` enum, `Covers`. No FS, no engine, no storage. C# tests pin the coverage matrix. |
| 2 | [stage-2-ask-routing.md](../stage-2-ask-routing.md) | pending | `Ask` marker interface; built-in path inside `error.handle` that recognises Ask-marked errors and runs the consent/input flow. Renders via template per kind under `os/system/permission/`, signs the response into a stored grant, reports handled/not-handled. On handled, engine re-runs the action; on not handled, error falls through to user-configured `error.handle` (or propagates). Existing `output.ask` action emits Ask-marked errors when it suspends; permission asks reuse the same path. |
| 3 | [stage-3-storage-binding.md](../stage-3-storage-binding.md) | pending | Two halves: `Actor.@this.Permission` per-actor typed view (Find/Add/Revoke; unifies in-memory + sqlite-backed via signature expiry); and `path.CheckPermission(Verb.X)` on the Path class — returns `Data.Ok` for in-root or matching grant, `Data.Fail` with `FilePermissionAsk` otherwise. Path consults its own Properties cache before delegating to `actor.Permission`. Storage is one `permission` table in `App.SettingsStore`, 2-column shape, actor scope via JSON filter. |
| 4 | [stage-4-filesystem-surface.md](../stage-4-filesystem-surface.md) | pending | The big bundle. Drop `System.IO.Abstractions.IFileSystem` inheritance. Redesign `IPLangFileSystem` v2 around `Path` parameters and `Data<T>` returns. Each operation calls `path.CheckPermission(Verb.X)` (delegated, Path owns the check) and propagates the result. Move/Copy merge per-Path Asks into one bundled Fail. Inventory every call site across the codebase. |
| 5 | [stage-5-messages-end-to-end.md](../stage-5-messages-end-to-end.md) | pending | Acceptance: Messages app reads each app's `system.sqlite`. First read prompts; user grants; subsequent reads succeed; grant persists in the `permission` table across process restart. Final `file.template`. |

## Topic deep-dives

- [plan/permission-design.md](plan/permission-design.md) — `FilePermission` record, `Verb` records, `Match` enum, `Covers` logic, JSON shape on the wire and at rest.
- [plan/runtime-flow.md](plan/runtime-flow.md) — `Ask` marker, `error.handle`'s built-in path, output.ask handler, signing, engine-side retry loop, variable-as-carrier lazy lookup.
- [plan/filesystem-surface.md](plan/filesystem-surface.md) — IPLangFileSystem v2 shape, every method's verb mapping, batched check for multi-path ops, the inventory pass.
- [plan/storage.md](plan/storage.md) — where signed grants live (`App.SettingsStore` `permission` table, 2-column rule), `actor.Permission` typed view, lazy-attach semantics, per-actor scoping via JSON filter.
- [plan/open-questions.md](plan/open-questions.md) — what's still actually open after this design pass.

## Key invariants

- **Action handlers are oblivious to permission logic.** They call `fs.ReadText(path)` and return whatever Data comes back. Engine handles Request routing. No `try/catch` for permission, no manual `CheckPermission` calls in action code.
- **Permission check happens exactly once per operation, at the Path.** The FS method asks `path.CheckPermission(Verb.X)` and propagates the result. No bypass — Path owns the question.
- **Same record shape for grant and request.** A grant is a `FilePermission` with `Match.Glob` and broad verb coverage. A request is a `FilePermission` with `Match.Exact` and the specific verb needed. `Covers(grant, request)` reads naturally either way.
- **Storage and wire shape are the same.** `Data<FilePermission>` in sqlite is the same Data that crosses an HTTP boundary for service permissions (future branches). No format translation.
- **Local-only permissions never travel on the wire.** FilePermission is local; if a future http action makes an outbound call to a PLang server, the local FilePermissions stay local. Service permissions (Payment, etc.) are what travels.

## Out of scope

- **Goal-mapped FS provider (the Code-routing layer)** — parked per original plan. Default disk is the only Code variant in this branch.
- **HTTP and Payment permission kinds** — follow-up branches. The pattern is named here so they slot in without redesign.
- **Encryption of stored grants** — separate concern (`Settings encryption-at-rest` TODO from 2026-05-11). Permission signing is the integrity story; encryption-at-rest is confidentiality. Decoupled.
- **Cross-app cascade for *requested* verb config** — was an open question previously; now deferred entirely. App-level "this app's writes are append-only by default" is a policy layer above grants. Out of scope for permission machinery itself.
