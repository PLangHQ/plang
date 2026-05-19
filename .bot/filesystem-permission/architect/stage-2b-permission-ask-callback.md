---
name: Stage 2b — PermissionAskCallback
description: Permission-specific callback subtype + Path.Authorize. Rides on stage 2a's generic callback round-trip.
type: stage
---

# Stage 2b: `PermissionAskCallback`

**Goal:** When a filesystem operation needs a permission grant that doesn't exist, surface a typed callback on the success path so the engine suspends (stage 2a's short-circuit), the channel writes the envelope (stage 2a's serializer), the channel collects the actor's raw answer, `callback.run` dispatches into `PermissionAskCallback.Run`, the grant is signed and stored, the action re-runs, and the goal continues (stage 2a's resume continuation).

Stage 2b is the permission-specific layer. Stage 2a is the runtime infrastructure it relies on.

## What this stage is NOT

- **Not the engine round-trip.** Stage 2a owns `IsCallback`, the step-loop short-circuit, resume continuation, and typed-Value reconstruction in the serializer.
- **Not a per-callback Serialize / Deserialize.** Removed in stage 2a (#5). The channel serializer handles the wire shape generically.
- **Not the HTTP channel.** Stage 2a's serializer is channel-agnostic. The HTTP/Web channel is parked.
- **Not the storage layer.** Stage 3 implements `actor.Permission.Find/Add`. Stage 2b uses the surface as a mock until 3 lands.
- **Not the FS surface.** Stage 4 rewrites `IPLangFileSystem` to consume `path.Authorize`. Stage 2b provides the method; stage 4 wires it in.

## Flow (canonical, grounded in stage 2a)

First dispatch of `- read /path/to/file.txt`:

1. `file.read` handler calls the FS method (stage 4 surface).
2. The FS method calls `path.Authorize(new Read())`.
3. `Path.Authorize` reads `Context.Actor`, asks `actor.Permission.Find(this, Read)` (stage 3 surface). Covered → return `Data.Ok` (green light). Not covered → Path constructs a `PermissionAskCallback` carrying the unmet `FilePermission` request(s) and returns `Data<PermissionAskCallback>`.
4. The FS method, on a non-green-light result, returns it as-is. `file.read` returns it as-is.
5. Stage 2a's step-loop short-circuit fires (Type is a callback). Goal returns the callback Data. Channel serializes via `Plang/Data.cs` and writes the envelope.

User answers (e.g. `a`):

6. Channel reads the actor's raw response string. Constructs a `Data` containing the deserialized callback (stage 2a's typed-Value reconstruction did this on inbound), sets `Answer` on the callback to the raw string, invokes `callback.run`.
7. `callback.run` verifies the signature, dispatches `PermissionAskCallback.Run(ctx)`.
8. `PermissionAskCallback.Run` parses the answer itself — `"a"→Always`, `"y"→Session`, `"n"→Deny`, anything else → Deny (fail-closed). Parsing logic lives on the callback because the payload's semantics are the callback's job, not the channel's.
9. Deny → returns `Data.Fail(PermissionDenied)`. Flows up like any other failure.
10. Allow → for each `req` in `Requests`: build `Data<FilePermission>`, set expiry (short for Session, long for Always — constants on this file), sign via the existing signing path, call `ctx.Actor.Permission.Add(signed)`. Then return what stage 2a's resume continuation expects.

Second dispatch (auto-triggered by stage 2a's resume continuation):

11. `file.read` runs again. `path.Authorize(new Read())` now finds the grant; returns `Data.Ok`.
12. FS method reads bytes, returns `Data<byte[]>`. The goal's remaining steps run.

## Deliverables

### 1. `PermissionAskCallback : ICallback`

Lives at `PLang/App/Callback/PermissionAskCallback.cs`. Shape mirrors `AskCallback` (post-stage-2a shrinkage):

- `Position` — `Call.Position?`, from `ICallback`. Set from `Context.App.CallStack.BottomFrame` at construction.
- `ActorName` — `string`, from `Context.Actor.Name`. Carried so resume binds to the same actor identity.
- `Requests` — `List<FilePermission>` (stage 1 type). The unmet grants the actor is being asked to approve.
- `Answer` — `object?`, set by the channel (via `callback.run`) from the actor's raw input. Opaque to the channel.
- `Run(ctx)`:
  - `decision = ParseAnswer(Answer)` — private method on the callback.
  - Deny → `Data.@this.FromError(new PermissionDenied(Requests))`.
  - Allow (Session or Always) → for each `req` in `Requests`: build `Data<FilePermission>`, set expiry per Decision, sign, call `ctx.Actor.Permission.Add(signed)`. Then let stage 2a's resume continuation run the suspended action + the rest of the goal.

### 2. `Path.Authorize(Verb verb) : Data`

Method on `App.FileSystem.Path` (class is plain `Path`, not `@this`). Reads `Context.Actor` directly — Path already carries Context (`Path.cs:57`).

Behaviour:

- `actor.Permission.Find(this, verb)` — returns matching `Data<FilePermission>` if any covers; null/empty otherwise. (Find's exact signature is stage 3.)
- Match found → `Data.Ok` (no payload — caller treats Ok as green light).
- No match → construct:
  ```
  new FilePermission(
      AppId  = Context.App.Id,           // App.this.cs:34
      Actor  = Context.Actor.Name,
      Path   = this.Absolute,
      Verb   = verb,
      Match  = Match.Exact)
  ```
  Wrap in a `PermissionAskCallback { Requests = [ that ] }` and return as `Data<PermissionAskCallback>`.

The verb argument is one of the stage 1 records (`new Read()`, `new Write()`, `new Delete()`). Construct it at the FS-method call site — the FS method knows which verb it represents.

### 3. `Decision` and `ParseAnswer`

`Decision` enum on the callback file: `Always`, `Session`, `Deny`. Private `ParseAnswer(object?)` returns one of these — `"a"`/`"always"` → Always; `"y"`/`"yes"` → Session; `"n"`/`"no"` → Deny; any other value, null, or empty → Deny (fail-closed).

### 4. Expiry constants

Live on `PermissionAskCallback`:
- `SessionExpiry` — `TimeSpan`, tentative 30 minutes.
- `AlwaysExpiry` — `TimeSpan?`, tentative 30 days (or `null` = forever, ratified by stage 3 based on routing).

Stage 3's `actor.Permission.Add` reads these via the signed Data's expiry field (set when `Run` signs) and routes by their relative magnitude (short → in-memory; long → sqlite).

### 5. `PermissionDenied` error

New error class at `PLang/App/Errors/PermissionDenied.cs` (alongside `AskError.cs`). Carries the `Requests` list so consumers can see what was denied. Returned by `PermissionAskCallback.Run` when `ParseAnswer` returns Deny.

### 6. Tests

- `Path.Authorize` returns `Data.Ok` when grant exists (mock `actor.Permission`).
- `Path.Authorize` returns `Data<PermissionAskCallback>` when no grant exists. Constructed `FilePermission` carries `AppId = Context.App.Id`, `Actor = Context.Actor.Name`, the absolute path, the verb record, and `Match.Exact`.
- `PermissionAskCallback.Run` with `Answer="a"` signs grants with `AlwaysExpiry` + calls `actor.Permission.Add` for each `Request`.
- `PermissionAskCallback.Run` with `Answer="y"` uses `SessionExpiry`.
- `PermissionAskCallback.Run` with `Answer="n"` returns `Data.Fail(PermissionDenied)` carrying the unmet `Requests`.
- `PermissionAskCallback.Run` with `Answer="garbage"` or `null` → fail-closed → Deny.
- `ParseAnswer` unit tests: every accepted alias + several fail-closed inputs.
- Round-trip via stage 2a's serializer: `Data<PermissionAskCallback>` → bytes → `Data<PermissionAskCallback>`. Verifies typed-Value reconstruction works for this kind.
- End-to-end (integration): a 2-step goal whose first step does `- read /apps/Other/file.txt`. With no grant, step 1 returns the callback; goal short-circuits. Inject `Answer="a"`, invoke `callback.run`. Grant signed and stored (mock or real actor.Permission). Resumed `file.read` succeeds; step 2 executes with the file contents.

## Dependencies

- **Stage 1** — `FilePermission`, `Verb.Read/Write/Delete` records, `Match` enum.
- **Stage 2a** — generic callback round-trip (`IsCallback`, step-loop short-circuit, resume continuation in `ICallback.Run`, typed-Value reconstruction in `Plang/Data.cs`). Stage 2b cannot ship without 2a.
- **Stage 3** — `actor.Permission.Find` and `.Add` (stage 2b tests can mock; stage 3 lands the real surface). Stage 2b does **not** block on stage 3 starting; the callback and Path method can land with mocks, stage 3 fills in.

## Acceptance

- `PermissionAskCallback` compiles, implements `ICallback`, round-trips through stage 2a's serializer.
- `Path.Authorize` is the only public surface for permission checks. No `HasAccess`, no `Check*`, no decomposition into bool+factory.
- `file.read`-against-ungranted-path test issues the callback; resume signs/stores grant, re-runs the action, and the rest of the goal completes.
- Channel doesn't import permission types — it sees `ICallback` Data and forwards a raw answer string.
- No `dotnet run --project PLang.Tests` regressions.
