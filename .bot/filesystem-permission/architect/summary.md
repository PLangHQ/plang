# Summary

## 2026-05-19 (third pass) — stage 2 reframed around `PermissionAskCallback`

After the verification pass surfaced that `error.handle` doesn't have a built-in runtime path and that the right pattern is callback-suspension (mirroring `output.ask` + `AskCallback`), stage 2 was rewritten end-to-end and the other stage files swept for mechanical corrections.

**Key shifts from the prior 2026-05-19 draft:**

- **No `Ask` marker interface; no `error.handle` built-in path.** Deleted. The signal that an action needs consent is the same one `output.ask` already uses: the action returns `Data` whose `Value is ICallback`. Engine/channel detects, serializes, suspends.
- **`PermissionAskCallback : ICallback`** lives at `PLang/App/Callback/PermissionAskCallback.cs`, mirrors `AskCallback` (Position, ActorName, Variables, Answer) and adds `Requests : List<FilePermission>`. On resume, `Run(ctx)` parses the Answer itself ("a"/"y"/"n"/garbage→deny — semantics owned by the callback, not the channel), signs + stores grants, then re-dispatches via `ctx.App.Run(Position.Action, ctx)`. Verbatim what `AskCallback.Run` does at `AskCallback.cs:99`.
- **`Path.Authorize(Verb verb)`** is the consumer surface (was `CheckPermission`). Name chosen because the method does real work (may construct a callback) and isn't boolean-shaped. Reads `Context.Actor` directly — Path already carries Context (`Path.cs:57`).
- **Channel is payload-agnostic.** It forwards a raw answer string to `callback.run`. It does not know about Decision, Always/Session/Deny, or `PermissionAskCallback` — that's the callback's job.
- **Actor source confirmed: `Context.Actor`.** `App.CurrentActor` is slated for deletion and is irrelevant to permission decisions.
- **Mechanical corrections** across stage files: `Path.@this` → `Path` (class isn't @this-shaped), `id` → `key` (sqlite column name), `AddOrUpdate` → `Set` (IStore API), Ask-marker / error.handle language → callback-from-action.
- **Snapshot for in-memory grants deferred.** Known limitation: "y" (session) grants don't survive snapshot/restore. Acceptable for v1.
- **Per-actor lock dropped.** Two concurrent asks against one actor may both prompt; revisit if real.

### Stage status

| Stage | File | Status |
|-------|------|--------|
| 1 | [Permission types](stage-1-permission-types.md) | pending |
| 2 | [PermissionAskCallback](stage-2-permission-ask-callback.md) | pending |
| 3 | [Storage binding](stage-3-storage-binding.md) | pending |
| 4 | [Filesystem surface (bundle)](stage-4-filesystem-surface.md) | pending |
| 5 | [Messages end-to-end + final consent UI](stage-5-messages-end-to-end.md) | pending |

## 2026-05-19 (continued) — design walkthrough with Ingi; further refinements

Long walking-the-plan session with Ingi after the initial 2026-05-19 rewrite. Further refinements:

**Storage rides `App.SettingsStore` directly — no new persistence layer.** Permissions are just signed Data stored under the existing `IStore`, in a `permission` table. The "Permission manager" disappears as a separate concept; what was `Permission/@this` becomes `Actor.@this.Permission` — a per-actor typed view unifying in-memory ("y") and persisted ("a") grants behind one Find/Add/Revoke surface.

**Two-column rule for all actor-scoped tables.** `id TEXT PRIMARY KEY, data TEXT` — that's the entire schema. Permanently. No migrations ever. Content-based filtering via `json_extract` in WHERE clauses; indexes via generated columns if scale demands (v2 concern).

**Actor on the record, not in the table.** `FilePermission` gains an `Actor` field (`"system" | "user" | "service"`). One `permission` table holds all actors' grants. Per-actor scoping is a JSON filter at query time, not a schema partition. Three actor kinds total — no arbitrary per-instance IDs. (The same shape extends to Settings when it becomes actor-scoped — separate branch.)

**`path.CheckPermission(Verb.X)` takes just the verb kind.** Inside, Path constructs the properly-narrowed `Verb.@this` request (Read full, Write/Delete all-false, etc.). No public `ReadOnly()`/`WriteOnly()` factories on Verb — narrowing logic lives in one place.

**Verb sub-option filtering stays in C#.** SQL filter is on `actor` + path-prefix only. Full pattern match (glob/regex), full verb Covers, signature validation — all C# on the candidate set.

**Action handlers / FS layer / Path / actor.Permission — four delegation levels.** Action handler calls FS method, FS method asks `path.CheckPermission(verb)`, Path asks `actor.Permission.Find(request)`, actor.Permission consults in-memory then sqlite. Each level owns its own concern.

**`error.handle` is the umbrella; ask routing is its built-in path.** No parallel "ask router" — the Ask-marker check happens inside `error.handle`'s built-in handler, before any user-configured `on error, call ...` modifier sees the error. Ask handler is stateless: signs, stores, reports handled/not-handled; engine re-runs on handled.

**Two CLAUDE.md proposals filed for OBP smells caught during the session:**
- Helper-soup smell: "Helper that takes a domain object and returns a derived answer" (filed in `claude-md-proposals.md`).
- Two related smells reinforced: transaction-script methods that wire private helpers; "envelope" mental model for Data (treats it as decomposable from outside) — corrected to "Data is a self-owning object, not a wrapper-with-contents."

## 2026-05-19 — v1 revised: major design shift across the plan

Session with Ingi reshaped the design substantially. The plan files were rewritten end-to-end. Key shifts from the 2026-05-14 v1:

**(1) Generalization named, not codified.** Permission is now a *pattern* repeated per subsystem. Folder location depends on how each subsystem is structured — FileSystem has its own non-module layer (`App/FileSystem/Permission/`), HTTP is a module so its permission would live at `App/modules/http/permission/` when its branch lands, Payment's location is unsettled. Each kind has its own independent record (FilePermission, etc.) with kind-specific fields. No abstract `Permission` base class — payment's shape doesn't fit file's, so trying to unify forces a lie. Shared infrastructure (`Ask` marker, `Match`, the template loader under `os/system/permission/`) lives once; everything else is per-kind.

**(2) Asks are `Data.Fail` with an `Ask` marker, handled inside `error.handle`.** No new Data terminal state, no parallel router. An action that needs user consent (or free-text input) returns `Data.Fail` whose Error implements the `Ask` marker. PLang's existing `error.handle` is the umbrella; its built-in path recognises Ask-marked errors and runs the consent/input flow — render via template, collect response, sign if a permission, store. Non-Ask errors take the normal `error.handle` path. User-configured `on error, call ...` modifiers run only for non-Ask errors or Asks the built-in flow couldn't resolve. Both permission asks and free-text user-input asks from `output.ask` share the mechanism — they're different concrete Ask types.

**(3) Permission lives on the variable.** The FS check reads `path.Properties["permission"]`. If empty/invalid, looks up the store (lazy). PLang developers can read `%path.permission%` like any other variable property. The store is the source of truth; Properties is a cache.

**(4) Same record for grant and request.** Asymmetry encoded in `Match` (Glob for broad grants, Exact for narrow requests). `Covers(grant, request)` reads naturally either way. No parallel `Permission` vs `PermissionRequest` types.

**(5) Pure-data Permission record.** No `Check` method, no `Describe` method. Just `Covers` (pure function over the record's data plus a peer record). The manager is storage, not a policy site.

**(6) Signature stays on `Data` (existing convention).** Signs over Value. No wrap-around-original-data idea — that was a wrong turn during the conversation. Stored grants are plain `Data<FilePermission>` with the typed value, envelope signature.

**(7) `IPLangFileSystem` refactor bundled in.** Stage 4 is now the big refactor — drop `System.IO.Abstractions.IFileSystem` inheritance, redesign every method to take `Path` and return `Data<T>`, bake permission check into each operation. No public `CheckPermission` or `ValidatePath` for callers to remember; enforcement is structural.

**(8) Two storage modes, one field.** Signature expiry distinguishes session-only (`y` = no expiry, in-memory) from persisted (`a` = long expiry, sqlite-backed). `Permission/@this.Add` routes based on the field.

**(9) Three permission scopes.** User/system (local) vs service (remote). FilePermission is always local — never travels on the wire. Service permissions (Payment, etc.) ride on outbound Data envelopes to PLang servers. This branch implements local only; service is the http/payment follow-ups.

**(10) Multi-path operations bundle their consent.** `file.move` and `file.copy` use a batched check internally — accumulate all missing permissions and surface in one Request. Fail-fast: no partial work.

### Stage status (superseded by the 2026-05-19 third-pass status table above)

### What previously-open questions resolved

From the 2026-05-14 list:
- App-side cascade for requested verbs → **out of scope.** Policy-layer concern above grants.
- `Content` sub-option on Read → **deferred.** Add when a real consumer needs it.
- Process-only vs persisted distinction → **kept**, expressed as a single field (signature expiry).
- Verb specification mechanism → **implicit at action layer, explicit inside FS.** Each FS method bakes its verb internally.
- Variable attachment timing → **lazy.** Standard PLang pattern.
- Multi-verb operations → **batched check.** Bundled Request, fail-fast.
- Path matching basis → **always absolute.**
- Throw vs return → **return.** New `IPLangFileSystem` v2 surface is `Data`-returning end-to-end.

### Remaining open (in `plan/open-questions.md`)

Five lower-stakes items: glob library choice, signature verification caching strategy, apps-without-identity edge case, non-interactive consent flows, concurrent permission requests per actor. None block stage 1.

## 2026-05-14 — v1: Filesystem permission system design (original)

Designed a signed-grant-based permission system for cross-app filesystem access, plus a Path-shaped rewrite of `IPLangFileSystem` to drop `System.IO.Abstractions.IFileSystem` inheritance.

The motivating use case is the forthcoming `os/apps/Messages` app, which needs to read each app's `system.sqlite` to consolidate messages. Today's `FileAccessControl` is a runtime-only record populated by a yes/no/always prompt thrown from inside `ValidatePath` — no signature, no expiry, no audit, no delegation.

**Key decisions (all settled in conversation with Ingi):**

- Permission record: `Permission(string AppId, string Path, Verb.@this Verb, Match Match)`. PLang-native field names, not Subject/Resource jargon.
- Singular OBP folders going forward: `Permission/`, `Verb/`. The doubled type name (`App.FileSystem.Permission.Permission`) is the accepted cost.
- Record lives inside `this.cs` alongside `@this`, not in a separate file.
- Verbs are records with default-true booleans (`Read(Recursive=true, Metadata=true)`, etc.). Always present, never nullable. Narrowing is a record copy with explicit `false`s.
- Each variant owns its own `Covers` method. Permission owns `HasAccess(Path, Verb.@this)`. The manager (`Permission/@this.Check`) is four lines because every comparison is delegated to the type that owns the data.
- Methods take whole domain objects (`Path`), not pre-decomposed primitives.
- Storage: signed `Data<Permission>` lives in the app's system variables (likely `filesystem.permission` — confirm in stage 2). No new on-disk file format.
- `IPLangFileSystem` drops `IFileSystem` inheritance, methods become Path-shaped, return `Data<T>`. The BCL stays as an implementation detail of the Default code only.
- Goal-mapped FS code (a "Code" provider that routes ops to a goal call) is **parked** for a later pass. Default disk only in v1.

**OBP refinements codified into `Documentation/v0.2/good_to_know.md`:**

- Variant design pattern: folder per concept, file per variant, always-present records with default-allow, owners-do-their-own-coverage. (New section)
- Singular folder naming rule going forward.
- "Methods take whole domain objects" rule.
- "Verb-named methods are fine when they do real work" — the `GetX`/`IsX` smell is about property-shaped questions, not verbs in general.
