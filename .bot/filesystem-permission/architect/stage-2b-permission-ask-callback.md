---
name: Stage 2b — Permission ask via Path.Authorize
description: Path.Authorize uses output.ask (stage 2a) — no new callback type. Sign/store on approval.
type: stage
---

# Stage 2b: Permission ask via `Path.Authorize`

**Goal:** When a filesystem operation needs a permission grant that doesn't exist, `Path.Authorize` calls `output.ask` to consult the actor. On a stateful channel (console), the prompt blocks and the answer flows back synchronously. On a stateless channel, `output.ask` returns a callback, the engine suspends (stage 2a #2), the channel writes the envelope (stage 2a #4), the user answers, `callback.run` resumes via `AskCallback.Run` which re-runs the suspended action — and `Path.Authorize` is called again, calls `output.ask` again, this time receives the answer (sentinel short-circuit), signs the grant, stores via `actor.Permission.Add`, returns Ok. The file operation proceeds.

**No new callback type.** Everything rides on `AskCallback` + the channel's existing stateful/stateless split. Stage 2b is purely the orchestration layer + storage + sign.

## What this stage is NOT

- **Not a new callback subtype.** `PermissionAskCallback` was the earlier idea; dropped. The single `AskCallback` plus the channel's AskCore split is sufficient.
- **Not the engine round-trip.** Stage 2a owns `IsCallback`, step-loop short-circuit, resume continuation, and typed-Value reconstruction.
- **Not the HTTP channel.** Stage 2a is channel-agnostic. The HTTP/Web channel is parked.
- **Not the storage layer.** Stage 3 implements `actor.Permission.Find/Add`. Stage 2b uses the surface as a mock until 3 lands.
- **Not the FS surface.** Stage 4 rewrites `IPLangFileSystem` to consume `path.Authorize`. Stage 2b provides the method; stage 4 wires it in.

## Flow

### Stateful channel (console)

`- read /path/to/file.txt`, no grant exists:

1. `file.read` → FS method → `path.Authorize(new Read())`.
2. `actor.Permission.Find` misses.
3. Authorize calls `output.ask("Allow user to read /path/to/file.txt? (y/n/a)")`.
4. `output.ask` delegates to the channel's `AskCore`. Stream channel writes the prompt, blocks on stdin, reads `"a"`.
5. `output.ask` returns `Data.Ok("a")`.
6. Authorize parses, signs the `FilePermission` with `AlwaysExpiry`, calls `actor.Permission.Add(signed)`, returns `Data.Ok`.
7. FS method does the actual read; returns `Data<byte[]>`. Goal continues normally.

No suspend, no callback short-circuit, no resume continuation — everything is synchronous in-process.

### Stateless channel (HTTP / Message)

Same first dispatch:

1–3. Same as above.
4. `output.ask` delegates to Message channel's `AskCore`. AskCore walks the call stack up from output.ask (Step=null, synthetic) to file.read (real step). Returns `Data<AskCallback>` carrying Position=file.read + Variables + ActorName. (See stage 2a #5 for the resume-frame resolution rule.)
5. Authorize sees the result is a callback (Type.IsCallback) — returns it unchanged.
6. FS method passes it through unchanged. `file.read` returns it. Stage 2a's step-loop short-circuit fires. Goal returns the callback Data.
7. Channel writes the envelope via `Plang/Data.cs` (stage 2a #4 — typed value, signed). HTTP response body.

User answers `"a"`:

8. Client POSTs the wire bytes back with the answer (channel-specific protocol).
9. Channel deserializes via `Plang/Data.cs` (stage 2a #4 reconstructs `Value` as typed `AskCallback`). Binds the answer via the existing `!ask.answer` sentinel pattern (the channel sets `ctx.Variables.Set("!ask.answer", "a")` before invoking `callback.run`).
10. `callback.run` verifies signature, calls `AskCallback.Run(ctx)`.
11. `AskCallback.Run`: re-runs `Position.Action` (file.read) via `App.Run`, then continues the goal from `Position.StepIndex + 1` (stage 2a #3).
12. file.read runs again. Calls Authorize. Find still misses (we haven't stored yet). Authorize calls `output.ask`. `output.ask`'s resume-consume sees `!ask.answer = "a"`, returns `Data.Ok("a")`.
13. Authorize parses, signs, stores, returns Ok.
14. file.read proceeds with the read. Goal continues.

## Deliverables

### 1. `Path.Authorize(Verb verb, string prefix = "") : Data`

Method on `App.FileSystem.Path` (class is plain `Path`, not `@this`). Reads `Context.Actor` directly — Path already carries Context (`Path.cs:57`).

**No new callback type.** Authorize uses the standard `output.ask` machinery from stage 2a. Channel decides stateful vs stateless; Authorize just observes "did I get an answer or a callback?" by checking `Type.IsCallback`.

```csharp
public async Task<Data> Authorize(Verb verb, string prefix = "")
{
    var actor = Context.Actor;

    // Already granted?
    if (actor.Permission.Find(this, verb) != null) return Data.Ok();

    // Ask. Stage 2a's smart Position-capture means output.ask works correctly
    // whether called as a step or nested via RunAction — Position lands on the
    // outermost real step frame either way.
    var question = $"{prefix}Allow {actor.Name} to {VerbLabel(verb)} {this.Absolute}? (y/n/a)";
    var askResult = await Context.App.RunAction<modules.output.ask>(
        new modules.output.ask { Question = Data.@this<string>.Ok(question) }, Context);

    // Stateless mode: ask suspended. Bubble up; engine short-circuits.
    if (askResult.Type?.ClrType?.IsCallback() == true) return askResult;

    // Stateful mode (or resumed stateless): process the answer.
    var answer = askResult.Value?.ToString()?.Trim();
    return answer switch
    {
        "a" => SignAndStore(actor, verb, AlwaysExpiry),
        "y" => SignAndStore(actor, verb, SessionExpiry),
        "n" => Data.FromError(new PermissionDenied(BuildRequest(actor, verb))),
        _   => await Authorize(verb, prefix: $"Invalid answer '{answer}'. ")  // recurse
    };
}

private Data SignAndStore(Actor actor, Verb verb, TimeSpan? expiry)
{
    var req = BuildRequest(actor, verb);
    var data = new Data<FilePermission>("", req);
    data.Context = Context;
    // Sign with the requested expiry; stage 3's Permission.Add routes
    // by signature expiry (short → in-memory; long/null → sqlite).
    data.EnsureSignedWithExpiry(expiry);   // helper or signing.sign call
    actor.Permission.Add(data);
    return Data.Ok();
}

private FilePermission BuildRequest(Actor actor, Verb verb) => new FilePermission(
    AppId = Context.App.Id,          // App.this.cs:34
    Actor = actor.Name,
    Path  = this.Absolute,
    Verb  = verb,
    Match = Match.Exact);
```

The verb argument is one of the stage 1 records (`new Read()`, `new Write()`, `new Delete()`). Construct it at the FS-method call site — the FS method knows which verb it represents.

**On the recursion for invalid answer:** in stateful mode this is an immediate re-prompt (AskCore blocks on stdin again). In stateless mode it produces a fresh AskCallback whose Question carries the "Invalid answer 'foo'. " prefix — user sees the error inline with the next request's prompt.

### 2. Expiry constants

Live as private const/static fields on `Path` (or a small `Expiry` static class colocated with `Authorize`). Tentative:
- `SessionExpiry` — `TimeSpan`, tentative 30 minutes (`"y"` → in-memory grant).
- `AlwaysExpiry` — `TimeSpan?`, tentative 30 days, or `null` = forever (`"a"` → persisted grant).

Stage 3's `actor.Permission.Add` reads these via the signed Data's expiry field and routes by their relative magnitude (short → in-memory; long/null → sqlite).

### 3. `PermissionDenied` error

New error class at `PLang/App/Errors/PermissionDenied.cs` (alongside `AskError.cs`). Carries the `FilePermission` that was denied so consumers can see what was refused.

### 4. Tests

- `Path.Authorize` returns `Data.Ok` immediately when grant exists (mock `actor.Permission.Find` to return a grant; no `output.ask` call observed).
- **Stateful path (Stream channel):**
  - Mock channel's `AskCore` to return `Data.Ok("a")`. Authorize signs with `AlwaysExpiry`, calls `Permission.Add`, returns Ok.
  - Mock returns `Data.Ok("y")`. Signs with `SessionExpiry`, Add, Ok.
  - Mock returns `Data.Ok("n")`. Returns `Data.Fail(PermissionDenied)` carrying the constructed `FilePermission`.
  - Mock returns `Data.Ok("garbage")`. Authorize re-asks with `"Invalid answer 'garbage'. "` prefix on the next question. Recursion depth bounded by sane input on retry.
- **Stateless path (Message channel / mock):**
  - Mock channel's `AskCore` returns `Data<AskCallback>`. Authorize returns it unchanged (Type.IsCallback check). FS method propagates. Engine short-circuits.
- **Constructed `FilePermission`** on the unmet-grant path: `AppId = Context.App.Id`, `Actor = Context.Actor.Name`, `Path = this.Absolute`, `Verb = the requested verb`, `Match = Match.Exact`.
- **End-to-end (integration):** a 2-step goal whose first step does `- read /apps/Other/file.txt`. With no grant and a stateful Stream channel piped with `"a"` on stdin, step 1 runs Authorize, prompt blocks, answer arrives, grant stored, file read, step 2 executes with the file contents. No callback machinery exercised.
- **End-to-end (stateless integration):** same goal against a fake Message channel. Step 1 returns `Data<AskCallback>`. Goal short-circuits. Inject `!ask.answer = "a"` and invoke `callback.run`. AskCallback.Run re-runs file.read; Authorize re-runs; output.ask sees the sentinel; answer returned; grant stored; bytes read; step 2 executes.

## Dependencies

- **Stage 1** — `FilePermission`, `Verb.Read/Write/Delete` records, `Match` enum.
- **Stage 2a** — generic callback round-trip (`IsCallback`, step-loop short-circuit, resume continuation in `ICallback.Run`, typed-Value reconstruction in `Plang/Data.cs`). Stage 2b cannot ship without 2a.
- **Stage 3** — `actor.Permission.Find` and `.Add` (stage 2b tests can mock; stage 3 lands the real surface). Stage 2b does **not** block on stage 3 starting; the callback and Path method can land with mocks, stage 3 fills in.

## Acceptance

- `Path.Authorize` is the only public surface for permission checks. No `HasAccess`, no `Check*`, no decomposition into bool+factory.
- No new `ICallback` subtype was introduced. `AskCallback` carries the suspend for permission asks just as it does for any `output.ask` use.
- **Stateful integration:** `file.read` against ungranted path, console channel piped with `"a"`, completes synchronously — no suspend, no callback short-circuit, no resume continuation triggered.
- **Stateless integration:** same scenario against a fake Message channel — suspend → resume → grant stored → file read → goal continues.
- Channel doesn't import permission types — it sees `ICallback` Data through stage 2a's generic infrastructure.
- No `dotnet run --project PLang.Tests` regressions.
