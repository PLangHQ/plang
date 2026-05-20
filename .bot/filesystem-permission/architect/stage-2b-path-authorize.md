---
name: Stage 2b — Permission ask via Path.Authorize
description: Path.Authorize uses output.ask (stage 2a). No callback class. Sign + store on approval.
type: stage
---

# Stage 2b: Permission ask via `Path.Authorize`

**Goal:** When a filesystem operation needs a permission grant that doesn't exist, `Path.Authorize` calls `output.ask` to consult the actor. On a stateful channel, the prompt blocks and the answer flows back synchronously. On a stateless channel, `output.ask` returns `Data<Ask>` (Exit-typed); stage 2a's engine captures Snapshot and short-circuits the goal. On resume, `output.ask` sees `!ask.answer` and returns the bound answer; Authorize parses it, signs the `Permission`, stores via `actor.Permission.Add`, returns Ok. The file operation proceeds.

No new callback type. No `PermissionAskCallback`. Everything rides on `output.ask` + stage 2a's Snapshot-resume.

## What this stage is NOT

- **Not a new Exit-typed kind.** `Ask` is the only Exit type used here; defined in stage 2a.
- **Not the engine machinery.** Stage 2a owns `Type.Exit()`, the step-loop Snapshot capture, the single resume entry, `output.ask` delegating to `channel's `Ask``.
- **Not the HTTP channel.** Stage 2a is channel-agnostic. HTTP/Message is parked.
- **Not the storage layer.** Stage 3 implements `actor.Permission.Find/Add`. Stage 2b uses the surface as a mock until 3 lands.
- **Not the FS surface.** Stage 4 rewrites `IPLangFileSystem` to consume `path.Authorize`. Stage 2b provides the method; stage 4 wires it in.

## Flow

### Stateful channel (console)

`- read /path/to/file.txt`, no grant:

1. `file.read` → FS method → `path.Authorize(new Read())`.
2. `actor.Permission.Find` misses.
3. Authorize calls `output.ask("Allow user to read /path/to/file.txt? (y/n/a)")` via `RunAction`.
4. `output.ask` delegates to the Stream `Ask`. channel's `Ask` writes the prompt, blocks on stdin, reads `"a"`.
5. `output.ask` returns `Data.Ok("a")`.
6. Authorize parses, signs the `Permission` with `AlwaysExpiry`, calls `actor.Permission.Add(signed)`, returns `Data.Ok`.
7. FS method does the actual read; returns `Data<byte[]>`. Goal continues normally.

No Snapshot capture, no short-circuit — everything synchronous in-process.

### Stateless channel (HTTP / Message)

Same first dispatch:

1–3. Same as above.
4. `output.ask` delegates to Message `Ask`. Returns `Data<Ask>(Question="Allow…")`.
5. Authorize sees `result.Type?.ClrType?.Exit() == true` — returns it unchanged.
6. FS method passes it through. `file.read` returns it. Stage 2a's step-loop branch fires: captures Snapshot via `App.Capture`, attaches to result, returns.
7. Goal returns the Exit-typed result with Snapshot attached. Channel layer serializes `{ question, snapshot }` to wire, signs, writes response.

User answers `"a"`:

8. Channel inbound: deserialize, verify signature, set `!ask.answer = "a"`, invoke the resume entry (stage 2a #5).
9. Resume entry: `App.Restore(snapshot, ctx)` → `App.Run(BottomFrame.Action = file.read, ctx)` → `Steps.RunAsync(ctx, fromIndex: BottomFrame.StepIndex + 1)`.
10. file.read runs again. Calls Authorize. `actor.Permission.Find` still misses (grant not stored yet). Authorize calls `output.ask`. `output.ask`'s resume-consume sees `!ask.answer = "a"`, returns `Data.Ok("a")`.
11. Authorize parses, signs, stores, returns Ok.
12. file.read does the read, returns `Data<byte[]>`. Step loop continues to step+1 of the goal.

## Deliverables

### 1. `Path.Authorize(Verb verb, string prefix = "") : Data`

Method on `App.FileSystem.Path` (class is plain `Path`, not `@this`). Reads `Context.Actor` directly — Path already carries Context (`Path.cs:57`).

```csharp
public async Task<Data> Authorize(Verb verb, string prefix = "")
{
    var actor = Context.Actor;

    // Already granted?
    if (actor.Permission.Find(this, verb) != null) return Data.Ok();

    // Ask. Stage 2a's smart Position capture (via Snapshot) means output.ask
    // works correctly whether called as a step or nested via RunAction.
    var question = $"{prefix}Allow {actor.Name} to {VerbLabel(verb)} {Absolute}? (y/n/a)";
    var askResult = await Context.App.RunAction<modules.output.ask>(
        new modules.output.ask { Question = Data.@this<string>.Ok(question) }, Context);

    // Stateless: suspended (Exit-typed). Bubble up; engine short-circuits.
    if (askResult.Type?.ClrType?.Exit() == true) return askResult;

    // Stateful (or resumed stateless): process the answer.
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
    var req  = BuildRequest(actor, verb);
    var data = new Data<Permission>("", req);
    data.Context = Context;
    data.EnsureSignedWithExpiry(expiry);   // helper / signing.sign call
    actor.Permission.Add(data);
    return Data.Ok();
}

private Permission BuildRequest(Actor actor, Verb verb) => new Permission(
    AppId = Context.App.Id,
    Actor = actor.Name,
    Path  = Absolute,
    Verb  = verb,
    Match = Match.Exact);
```

The verb argument is one of the stage 1 records (`new Read()`, `new Write()`, `new Delete()`). Construct it at the FS-method call site — the FS method knows which verb it represents.

**On the recursion for invalid answer:** in stateful mode it's an immediate re-prompt (channel's `Ask` blocks on stdin again). In stateless mode it produces a fresh Exit-typed result with Question prefixed by `"Invalid answer 'foo'. "` — user sees the error inline with the next request's prompt.

### 2. Expiry constants

Live as private const/static fields colocated with `Authorize` (on `Path`, or a small `Expiry` static class):
- `SessionExpiry` — `TimeSpan`, tentative 30 minutes (`"y"` → in-memory grant).
- `AlwaysExpiry` — `TimeSpan?`, tentative 30 days (or `null` = forever) (`"a"` → persisted grant).

Stage 3's `actor.Permission.Add` reads the signed Data's expiry field and routes by magnitude (short → in-memory; long/null → sqlite).

### 3. `PermissionDenied` error

New error class at `PLang/App/Errors/PermissionDenied.cs` (alongside `AskError.cs`). Carries the `Permission` that was denied so consumers can see what was refused. Returned by Authorize on the `"n"` branch.

### 4. Tests

- `Path.Authorize` returns `Data.Ok` immediately when grant exists (mock `actor.Permission.Find` to return a grant; no `output.ask` call observed).
- **Stateful path (Stream channel):**
  - Mock `Ask` returns `Data.Ok("a")`. Authorize signs with `AlwaysExpiry`, calls `Permission.Add`, returns Ok.
  - Mock returns `Data.Ok("y")`. Signs with `SessionExpiry`, Add, Ok.
  - Mock returns `Data.Ok("n")`. Returns `Data.Fail(PermissionDenied)` carrying the constructed `Permission`.
  - Mock returns `Data.Ok("garbage")`. Authorize re-asks with `"Invalid answer 'garbage'. "` prefix on the next question.
- **Stateless path (Message channel / mock):**
  - Mock `Ask` returns `Data<Ask>`. Authorize returns it unchanged (Type.Exit() check). FS method propagates. Engine short-circuits and captures Snapshot (stage 2a #3).
- **Constructed `Permission`** on the unmet-grant path: `AppId = Context.App.Id`, `Actor = Context.Actor.Name`, `Path = this.Absolute`, `Verb = the requested verb`, `Match = Match.Exact`.
- **End-to-end (stateful):** 2-step goal whose first step does `- read /apps/Other/file.txt`. With no grant and a Stream channel piped with `"a"` on stdin, step 1 runs Authorize, prompt blocks, answer arrives, grant stored, file read, step 2 executes with the file contents. No Snapshot capture exercised.
- **End-to-end (stateless):** same goal against a fake Message channel. Step 1 returns `Data<Ask>`. Goal short-circuits with Snapshot attached. Invoke the resume entry (stage 2a #5) with `{ snapshot, answer:"a" }`. file.read re-runs, Authorize re-runs, output.ask returns `Data.Ok("a")`, grant stored, bytes read, step 2 executes.

## Dependencies

- **Stage 1** — `Permission`, `Verb.Read/Write/Delete` records, `Match` enum.
- **Stage 2a** — `Type.Exit()`, step-loop Snapshot capture, single resume entry, `output.ask` delegating to `channel's `Ask``. Stage 2b cannot ship without 2a.
- **Stage 3** — `actor.Permission.Find` and `.Add` (stage 2b tests can mock; stage 3 lands the real surface). Stage 2b does **not** block on stage 3 starting; Authorize + signing can land with a mocked Permission surface; stage 3 fills it in.

## Acceptance

- `Path.Authorize` is the only public surface for permission checks. No `HasAccess`, no `Check*`, no decomposition into bool+factory.
- No new Exit-typed kind, no new `ICallback`-like class, no new wire shape. Permission rides on stage 2a's `Ask` + Snapshot infrastructure exclusively.
- **Stateful integration:** `file.read` against ungranted path, console channel piped with `"a"`, completes synchronously — no Snapshot capture, no short-circuit.
- **Stateless integration:** same scenario against a fake Message channel — suspend (with Snapshot) → resume → grant stored → file read → goal continues.
- No `dotnet run --project PLang.Tests` regressions.
