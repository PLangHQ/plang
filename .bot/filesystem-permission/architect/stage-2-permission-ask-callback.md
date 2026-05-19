---
name: Stage 2 — PermissionAskCallback
description: Suspend/resume mechanism for "needs consent" inside a filesystem operation. Mirrors AskCallback.
type: stage
---

# Stage 2: `PermissionAskCallback`

**Goal:** When a filesystem operation needs a permission grant that doesn't exist, suspend the goal, prompt the actor on whatever channel issued the call, sign + store the grant on approval, re-dispatch the original action. Same suspension model as `output.ask` — different payload, different resume work.

This stage builds shared infrastructure for **all** permission kinds (FilePermission first; HTTP/Payment later). What's stage 2's: the callback type, its wire/sign/resume mechanics, and the `Authorize` method on `Path` that produces it. What's stage 3's: the in-memory + sqlite `actor.Permission` view it lands grants into. What's stage 4's: the `IPLangFileSystem` v2 surface that actually calls `path.Authorize(...)`.

## What this stage is NOT

- **No `Ask` marker interface.** The earlier draft proposed one; not needed. Action handlers signal "I need consent" by returning `Data` whose `Value is ICallback` — same signal `output.ask` already uses.
- **No `error.handle` built-in path.** Drafted earlier, doesn't exist in code (`error.handle` is a user-applied Modifier, not a runtime-resident interceptor). Permission consent is **not** an error path. Denial becomes an error after the fact; the ask itself is a success-path callback.
- **No `output.ask` migration.** Already works the way we want.
- **No per-actor lock.** Two concurrent asks against the same actor will currently produce two prompts. Noted as a known gap; revisit if real.
- **No template loader.** Channel renders consent from the typed payload directly in stage 2. Per-kind templating (`os/system/permission/<kind>.template`) is stage 5 polish.

## Flow (canonical, grounded in code)

First dispatch of `- read /path/to/file.txt`:

1. `file.read` handler calls `IPLangFileSystem.Read(path)` (stage 4 surface).
2. The FS method calls `path.Authorize(new Read())`.
3. `Path.Authorize` reads `Context.Actor`, asks `actor.Permission.Find(this, Read)` (stage 3 surface). Covered → return `Data.Ok` (green light). Not covered → `Path` constructs a `PermissionAskCallback` carrying the unmet `FilePermission` request(s) and returns `Data<PermissionAskCallback>`.
4. The FS method, on a non-green-light result, returns it as-is. `file.read` returns it as-is.
5. Engine/channel sees `Value is ICallback`, calls `cb.Serialize(ctx)`, suspends the goal. Same path `AskCallback` already rides today.
6. Channel renders the consent prompt and waits.

User answers (e.g. `a`):

7. Channel forwards the raw answer string to `callback.run` with the wire bytes. **Channel does not parse the answer** — it doesn't know the payload type.
8. `callback.run` verifies the signature, deserializes (`ICallback`-tagged dispatch → `PermissionAskCallback.Deserialize`), sets `cb.Answer = "a"`, calls `cb.Run(ctx)`.
9. `PermissionAskCallback.Run` parses the answer itself — `"a"→Always`, `"y"→Session`, `"n"→Deny`, anything else → Deny (fail-closed). Parsing logic lives on the callback because the payload's semantics are the callback's job.
10. Deny → returns `Data.Fail(PermissionDenied)`. Flows up like any other failure; `error.handle` and propagation handle it the normal way.
11. Allow → signs each `Data<FilePermission>` (expiry = short for Session, long for Always), calls `ctx.Actor.Permission.Add(signed)` for each, then `ctx.App.Run(Position.Action, ctx)`. Verbatim what `AskCallback.Run` does at `AskCallback.cs:99`.

Second dispatch (auto-triggered by `Run`):

12. `file.read` runs again. `path.Authorize(new Read())` now finds the grant; returns `Data.Ok`.
13. FS method reads bytes, returns `Data<byte[]>`. Step completes.

## Deliverables

### 1. `PermissionAskCallback : ICallback`

Lives at `PLang/App/Callback/PermissionAskCallback.cs`. Shape mirrors `AskCallback.cs`:

- `Position` — `Call.Position?`, same shape as on `AskCallback`. Set from `Context.App.CallStack.BottomFrame` at construction.
- `ActorName` — `string`, from `Context.Actor.Name`.
- `Variables` — `List<Data.@this>`, for any surviving state the FS layer wants on resume. v1: empty.
- `Requests` — `List<FilePermission>` (stage 1 type). The unmet grants the actor is being asked to approve.
- `Answer` — `object?`, set by `callback.run` from the channel's raw input. Opaque to the channel.
- `Serialize(ctx)` / `Deserialize(bytes, ctx)` — same encrypt-via-`crypto.encrypt` + JSON wire as `AskCallback`. Wire payload is the `Requests` list plus `Position`/`ActorName`/`Variables`. Defense-in-depth `MaxWireBytes` cap, same shape as `AskCallback.MaxWireBytes`.
- `Run(ctx)`:
  - `decision = ParseAnswer(Answer)` — private method on the callback.
  - Deny → `Data.@this.FromError(new PermissionDenied(Requests))`.
  - Allow (Session or Always) → for each `req` in `Requests`: build `Data<FilePermission>`, set expiry (short / long — concrete values in stage 3 with the storage layer), sign via the existing signing path, call `ctx.Actor.Permission.Add(signed)`. Then `await ctx.App.Run(Position.Action, ctx)`.

### 2. `Path.Authorize(Verb verb) : Data`

Method on `App.FileSystem.Path` (NOT `Path.@this` — class is plain `Path`). Reads `Context.Actor` directly — Path already carries Context (`Path.cs:57`).

Behaviour:

- `actor.Permission.Find(this, verb)` — returns matching `Data<FilePermission>` if any covers; null/empty otherwise. (Find's exact signature is stage 3.)
- Match found → `Data.Ok` (no payload — caller treats Ok as green light).
- No match → construct `PermissionAskCallback { Requests = [new FilePermission(actor=Context.Actor.Name, path=this.Absolute, verb=verb, match=Exact)] }` and return `Data<PermissionAskCallback>`.

The verb argument is one of the stage 1 records (`new Read()`, `new Write()`, `new Delete()`). Construct it at the FS-method call site — the FS method knows which verb it represents.

### 3. `callback.run` adjustment

`callback.run` already deserializes via `ICallback` dispatch. New work: route an incoming wire with `Type = "permission-ask"` (or whichever discriminator we use) to `PermissionAskCallback.Deserialize`, set `Answer` from the channel-provided raw string, invoke `Run`.

If `callback.run` doesn't currently have a registry-of-types pattern, add one. Coder may need to extend `Wire/this.cs` — investigate at implementation time, do not pre-design.

### 4. `FilePermission.AppId` source

Path constructs the request with `AppId = Context.App.Id` (verified `App.this.cs:34`). Document this on the constructor call so the coder doesn't invent a path.

### 5. Engine: short-circuit on a callback result (NEW — fixes existing gap for `AskCallback` too)

The current step loop at `Goals/Goal/Steps/this.cs:RunAsync` does not stop when an action returns a `Data` whose Type is a callback — subsequent steps run with the callback object as their input. This means **mid-goal `output.ask` is not properly suspending today either**; the existing tests cover round-trip and pre-bound resume but not real mid-goal continuation. Stage 2 fixes this for all callback kinds.

**Changes:**

- **`Type.IsCallback` property** on `App.Data.Type` (`Data/this.cs`). Returns `true` when `ClrType` is assignable to `App.Callback.ICallback`. The engine queries Type, never `data.Value` (OBP — engine never decomposes Data).
- **Step-loop short-circuit** in `Steps/this.cs:RunAsync`, alongside the existing `Returned` and `!Success && !Handled` checks:
  ```
  if (result.Type?.IsCallback == true) return result;
  ```
  The goal's `RunAsync` returns the callback Data as the goal's effective output. The channel serializes it onto the wire and the request completes.

### 6. Resume: continue goal execution from after the suspended step (NEW — also fixes `AskCallback`)

Currently `ICallback.Run` (both `AskCallback.Run` and any future implementations) calls `App.Run(Position.Action, ctx)` — runs **only** the originally suspended action. After that returns, the rest of the goal does **not** execute. This means a goal like `- ask user "name?" / - write out %name%` does not currently complete after a real resume (only the simulated-via-pre-bind path is tested).

**Change:**

- After `App.Run(Position.Action, ctx)` succeeds, the callback's `Run` invokes the goal's step iterator starting at `Position.StepIndex + 1`. This requires either:
  - A new `Steps.RunAsync(context, fromIndex)` overload, OR
  - A method on `Goal` that does "run steps starting at index N."
  Coder picks the cleaner shape; the call lives in one place (callback's `Run`), so the engine API change is small.

The same change is applied to `AskCallback.Run` — same machinery, same fix. After the fix, mid-goal `output.ask` works end-to-end.

A new integration test must prove the full loop: fresh dispatch returns callback → channel suspends → callback.run on the wire → resumed action returns answer → **next step in the goal runs and uses the answer**.

### 7. `PermissionDenied` error

New error class under `PLang/App/Errors/` (alongside `AskError.cs`). Carries the `Requests` list so callers can see what was denied. Returned by `PermissionAskCallback.Run` when the parsed Decision is Deny.

### 8. `Decision` shape

`Decision` enum (or sealed types — coder's call). At least three values: `Always`, `Session`, `Deny`. `ParseAnswer(object?)` lives on `PermissionAskCallback` as a private method, fail-closed (unknown answer → Deny). Lives in the callback file; not shared with other kinds yet.

### 9. Expiry values

Constants live on `PermissionAskCallback` (the file that signs). Tentative values:
- Session ("y"): 30 minutes.
- Always ("a"): 30 days (or `null` / forever — stage 3 ratifies based on storage routing).

Stage 3 reads these constants when `Add` routes by expiry. If stage 3 wants different values, change them here.

### 10. Tests

- `PermissionAskCallback` round-trips through Serialize/Deserialize.
- `Path.Authorize` returns `Data.Ok` when grant exists (use mock `actor.Permission`).
- `Path.Authorize` returns `Data<PermissionAskCallback>` when no grant exists.
- `Path.Authorize` populates the constructed `FilePermission` with `AppId = Context.App.Id`.
- `Run` with Answer="a" stores grant with long expiry + re-dispatches (mock `actor.Permission.Add` and `App.Run`).
- `Run` with Answer="y" stores grant with short expiry.
- `Run` with Answer="n" returns `Data.Fail(PermissionDenied)` carrying the unmet `Requests`.
- `Run` with Answer="garbage" or `null` fail-closes to Deny.
- `Type.IsCallback` returns true for `Data<AskCallback>` and `Data<PermissionAskCallback>`; false for `Data<string>`, `Data<byte[]>`, etc.
- Step-loop short-circuit: a goal whose step 1 returns a callback does not run steps 2+; `RunAsync` returns the callback Data.
- Resume continuation: starting from a Position pointing at step N, after the action's resumed run completes, steps N+1..end execute. Test with a 3-step goal: ask → write the answer → assert it was written.
- End-to-end: `file.read` against ungranted path returns callback; resumed call signs/stores grant and re-runs; second pass reads content; subsequent goal steps run with the read result.

## Dependencies

- Stage 1 — `FilePermission`, `Verb.Read/Write/Delete` records.
- Stage 3 — `actor.Permission.Find` and `.Add` (stage 2 tests can mock; stage 3 lands the real surface). Stage 2 does **not** block on stage 3 starting; the callback and Path method can land with mocks, stage 3 fills in.
- Existing infrastructure (no new work): `ICallback`, `AskCallback` template, `crypto.encrypt`/`crypto.decrypt`, signing, `callback.run`, `App.Run(Position.Action, ctx)`.

## Acceptance

- `PermissionAskCallback` compiles, implements `ICallback`, round-trips.
- `Path.Authorize` is the only public surface for permission checks. No `HasAccess`, no `Check*`, no decomposition into bool+factory.
- `Type.IsCallback` is the engine-side discriminator. The step-loop short-circuit and resume-continuation work for **any** `ICallback`, not just `PermissionAskCallback` — verified by adding a mid-goal `output.ask` integration test that now passes (previously not exercised end-to-end).
- `file.read`-against-ungranted-path test issues a callback; resume test signs/stores grant, re-runs, returns content, and **the next step in the goal runs**.
- Channel doesn't import permission types — it only sees `ICallback` bytes and forwards a raw string. Verified by inspection of channel code touched in this stage (should be zero or near-zero).
- No `dotnet run --project PLang.Tests` regressions. New tests added cover Type.IsCallback, the step-loop short-circuit, the resume continuation, and the full permission round-trip.
