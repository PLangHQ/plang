# Stage 2b: Permission ask via `Path.Authorize`

**Goal:** When a filesystem operation needs a permission grant that doesn't exist, `Path.Authorize` calls `output.ask` to consult the actor. Channel-mode decides: stateful blocks and returns the answer synchronously; stateless suspends via stage 2a's Snapshot machinery. On resume, the resumed action sees the answer, Authorize signs the `Permission`, stores via `actor.Permission.Add`, returns Ok.

No new callback type. Permission rides entirely on stage 2a's `Ask` + Snapshot-resume infrastructure.

## Out of scope

- Engine machinery (Snapshot, `Type.Exit()`, resume) — stage 2a.
- Storage layer (`actor.Permission.Find/Add/Revoke`) — stage 3. Stage 2b tests mock the surface; stage 3 lands the real one.
- FS surface rewrite (`IPLangFileSystem` v2) — stage 4. Stage 2b provides `Path.Authorize`; stage 4 wires it into every FS method.
- HTTP/Message channel — parked.

## Flow

### Stateful channel (console)

`- read /path/to/file.txt`, no grant:

1. `file.read` → FS method → `path.Authorize(new Read())`.
2. `actor.Permission.Find` misses.
3. Authorize constructs `new modules.output.ask { Question = "Allow user to read /path/to/file.txt? (y/n/a)" }` and calls `askAction.RunAsync(Context)`.
4. `output.ask` delegates to Stream channel's `Ask`. It writes the prompt, blocks on stdin, reads `"a"`.
5. `output.ask` returns `Data.Ok("a")`.
6. Authorize parses, signs the `Permission` with `AlwaysExpiry`, calls `actor.Permission.Add(signed)`, returns `Data.Ok`.
7. FS method does the actual read; returns `Data<byte[]>`. Goal continues.

No Snapshot capture, no short-circuit.

### Stateless channel (Message)

Steps 1–3 same. Then:

4. `output.ask` delegates to Message channel's `Ask`. It builds `Data<Ask>("", question)` with `Snapshot = action.Snapshot()`.
5. Authorize sees `Type.Exit() == true` — returns it unchanged. FS method propagates. file.read returns it.
6. Step loop's `ShouldExit()` fires. Goal returns the Exit-typed Data. Channel serializes `{ value:question, snapshot, signature }` to the wire.

User answers `"a"`:

7. Channel inbound: deserialize → verified Data, set `!ask.answer = "a"`, invoke `callback.run`.
8. `Data.Snapshot.Resume(ctx)` runs `ResumeChain`. Bottom frame = file.read. `Goal.RunFrom(stepIdx, actionIdx)` re-runs file.read.
9. file.read → Authorize → Find still misses → calls `output.ask` again. The resume-consume sentinel fires; returns `Data.Ok("a")`.
10. Authorize parses, signs, stores, returns Ok. file.read reads bytes; step continues; goal continues.

## Deliverables

### 1. `Path.Authorize(Verb verb, string prefix = "") : Data`

Method on `App.FileSystem.Path` (class is plain `Path`, not `@this`). Reads `Context.Actor` directly — `Path` already carries `Context` (`Path.cs:57`).

```csharp
public async Task<Data> Authorize(Verb verb, string prefix = "")
{
    var actor = Context.Actor;

    if (actor.Permission.Find(this, verb) != null) return Data.Ok();

    var question = $"{prefix}Allow {actor.Name} to {VerbLabel(verb)} {Absolute}? (y/n/a)";
    var askAction = new modules.output.ask { Question = Data.@this<string>.Ok(question) };
    var askResult = await askAction.RunAsync(Context);

    // Stateless: suspended → bubble up.
    if (askResult.Type?.ClrType?.Exit() == true) return askResult;

    // Stateful (or resumed stateless): process the answer.
    var answer = askResult.Value?.ToString()?.Trim();
    return answer switch
    {
        "a" => SignAndStore(actor, verb, persist: true),
        "y" => SignAndStore(actor, verb, persist: false),
        "n" => Data.FromError(new PermissionDenied(BuildRequest(actor, verb))),
        _   => await Authorize(verb, prefix: $"Invalid answer '{answer}'. ")
    };
}

private Data SignAndStore(Actor actor, Verb verb, bool persist)
{
    var data = new Data<Permission>("", BuildRequest(actor, verb));
    data.Context = Context;
    // persist=true → far-future expiry → Add routes to sqlite (stage 3).
    // persist=false → no expiry → Add routes to in-memory list (dies with App).
    data.EnsureSigned(persist ? AlwaysExpiry : null);
    actor.Permission.Add(data);
    return Data.Ok();
}

private Permission BuildRequest(Actor actor, Verb verb) => new Permission(
    AppId: Context.App.Id,
    Actor: actor.Name,
    Path:  Absolute,
    Verb:  verb,
    Match: Match.Exact);
```

The verb argument is one of the stage 1 records (`new Read()`, `new Write()`, `new Delete()`) — the FS method knows which it represents.

**Recursion on invalid answer:** in stateful mode it's an immediate re-prompt (`Ask` blocks again). In stateless mode it produces a fresh Exit-typed result with the next question prefixed by `"Invalid answer 'foo'. "` — user sees the error inline in the next request's prompt.

**Known awkwardness — `BuildRequest`/`SignAndStore`:** this exists only because `output.ask` is text-only today. The Permission gets built twice — once to format the question, once to reconstruct on the answer side. When `output.ask` grows structured options (tracked in `todos.md`), the Permission record is defined once, passed as the option, the user signs that exact definition, no reconstruction needed.

### 2. Expiry and storage routing

- **`"y"` (session):** in-memory. No expiry value on the signed Data; grant lives as long as the App lives; dies on App exit. `actor.Permission.Add` (stage 3) routes to its in-memory list.
- **`"a"` (always):** persisted. Signed with `AlwaysExpiry` (private const, tentative 100 years — or `null` if the signing layer treats null as forever). `actor.Permission.Add` routes to sqlite.

Only one constant needed: `AlwaysExpiry`. No `SessionExpiry`.

### 3. `PermissionDenied` error

New error class at `PLang/App/Errors/PermissionDenied.cs` (alongside `AskError.cs`). Carries the `Permission` that was denied. Returned by Authorize on the `"n"` branch.

## Tests

- `Path.Authorize` returns `Data.Ok` immediately when grant exists (mock `actor.Permission.Find` → returns a grant; no `output.ask` call observed).
- **Stateful path** (mock the channel's `Ask`):
  - `Data.Ok("a")` → Authorize signs with `AlwaysExpiry`, `Permission.Add` called, returns Ok.
  - `Data.Ok("y")` → signs with no expiry, `Add`, Ok.
  - `Data.Ok("n")` → returns `Data.Fail(PermissionDenied)` carrying the constructed `Permission`.
  - `Data.Ok("garbage")` → recurses with `"Invalid answer 'garbage'. "` prefix.
- **Stateless path** (mock the channel's `Ask`):
  - Returns `Data<Ask>` → Authorize bubbles it unchanged → step loop short-circuits.
- **Constructed `Permission`** on unmet-grant path: `AppId = Context.App.Id`, `Actor = Context.Actor.Name`, `Path = this.Absolute`, `Verb` = requested verb, `Match = Match.Exact`.
- **End-to-end (stateful):** 2-step goal whose first step does `- read /apps/Other/file.txt`. Stream channel piped with `"a"`. Step 1 runs Authorize → prompt blocks → answer arrives → grant stored → file read. Step 2 sees the contents.
- **End-to-end (stateless):** same goal against a fake Message channel. Suspend with Snapshot. Resume with `{ snapshot, answer:"a" }`. file.read re-runs, Authorize re-runs, grant stored, bytes read, step 2 executes.

## Dependencies

- **Stage 1** — `Permission`, `Verb.Read/Write/Delete`, `Match`.
- **Stage 2a** — `Type.Exit()`, step-loop short-circuit, `Snapshot.Resume`, `output.ask` delegating to `Channel.Ask`. Stage 2b cannot ship without 2a.
- **Stage 3** — `actor.Permission.Find/Add`. Tests can mock; stage 3 fills in. Stage 2b does **not** block on stage 3 starting.

## Acceptance

- `Path.Authorize(verb)` is the only public surface for permission checks.
- No new Exit-typed kind, no new callback class, no new wire shape. Permission rides on stage 2a exclusively.
- Stateful integration: file.read against ungranted path completes synchronously with `"a"` on stdin.
- Stateless integration: same scenario against a fake Message channel — suspend → resume → grant stored → file read → goal continues.
- No `dotnet run --project PLang.Tests` regressions.
