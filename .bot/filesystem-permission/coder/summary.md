# Coder — filesystem-permission

## Version
v1

## What this is
PLang's filesystem permission system + a unified suspend/resume mechanism
(replaces `AskCallback` / `ErrorCallback` with a single `Snapshot`-based
round-trip). Architect produced 5 stages; test-designer wrote ~70 C# TUnit
stubs + 11 PLang `.test.goal` stubs. Coder makes them pass.

## What was done

### Stage 1 — Permission types (commit `4143621b8`)
Pure types under `PLang/App/FileSystem/Permission/`:
- `this.cs` — `Permission` record (`AppId, Actor, Path, Verb, Match`) +
  `Match` enum + `Covers(Permission)` + private `PathMatches` dispatching
  Exact / Glob / Regex (fail-closed on unknown enum).
- `Verb/this.cs` — container with init-only `Read/Write/Delete` defaulting
  non-null (option B per Ingi: `new Verb()` is fully granted).
- `Verb/Read.cs`, `Write.cs`, `Delete.cs` — sub-records with default-true
  bool options + `Covers`.

Added `Microsoft.Extensions.FileSystemGlobbing 9.0.5` to `PLang.csproj`.
21 PermissionTests green.

### Stage 2a — Snapshot-resume engine (in progress, 4 of 8 slices)

Sub-slicing in `.bot/filesystem-permission/coder/v1/stage-2a-slicing.md`.

- **2a.1** (`b06c64380`) — primitives: `IExitsGoal`, `Type.Exit()`,
  `Ask` payload class (`[PlangType("ask")]`), `Data.Snapshot` property,
  `Data.ShouldExit` extension, `Action.Synthetic` (default true),
  `IContext.Snapshot()` extension.
- **2a.2** (`1744d3f13`) — step loop short-circuits on `ShouldExit()`.
  Steps.RunAsync, Step.RunAsync.
- **2a.3** (`...`) — `Step.RunFrom(ctx, fromActionIdx)` +
  `Goal.RunFrom(ctx, stepIdx, actionIdx)`. Per resolved architect comment,
  no `Steps.RunAsync(fromIndex)` overload — remaining-steps loop lives
  inside `Goal.RunFrom`.
- **2a.4** (`30f503396`) — `Channel.AskCore` takes the `output.ask` action
  directly. Stream extracts question + reads stdin. Message channel
  becomes concrete with `AskCore` returning Data{Type=ask, Snapshot}.
  `output.ask.Run` shrinks to ~10 lines; old `AskCallback` construction
  deleted. Test-channel callers updated.

**Tests flipped green (37 total)**: PermissionCoversTests (10),
VerbCoversTests (11), TypeExitTests (6), StepLoopShouldExitTests (6),
DataSnapshotTests (6), ActionSyntheticTests #1, GoalRunFromTests (5),
OutputAskRoutingTests (6).

Suite: started at 105 stub failures, now at 75 (all remaining are
test-designer stubs for slices 2a.5–2a.8 and stages 2b–5).

### Remaining slices

- **2a.5** — Action owns its execution. Drop `App.Run`/`App.RunAction`/
  `cause` parameter. ~32 call sites across signing, http, GoalCall,
  Data.Envelope, modules/error/handle, etc. CallStack.Push reads
  `Action.Synthetic`. PR-loader flips Synthetic=false on materialization.
- **2a.6** — `Snapshot.Resume(ctx)` recursive `ResumeChain`. `callback.run`
  rewrite to ~10 lines.
- **2a.7** — Delete `ICallback`, `AskCallback`, `ErrorCallback`,
  `Callback/Wire/`. Retype `Error.Callback` to `Data<Snapshot>`. Drop
  `ICallback` branches in `Data/this.Envelope.cs`. Delete the 6 obsolete
  `CallbackTests` files exercising the deleted types.
- **2a.8** — PLang `.test.goal` end-to-end (`Tests/Callback/
  StatelessCrossGoalResumes`).

### Stages 2b, 3, 4, 5
Not started.

## Code example

`Type.Exit()` predicate — the engine's only Exit-type discriminator:

```csharp
// PLang/App/Types/Exit.cs
public static class TypeExitExtensions
{
    public static bool Exit(this System.Type? clrType)
        => clrType != null && typeof(global::App.IExitsGoal).IsAssignableFrom(clrType);
}
```

Used in `Data.ShouldExit`:

```csharp
public static bool ShouldExit(this @this d) =>
    (!d.Success && !d.Handled)
    || d.Returned
    || d.Type?.ClrType.Exit() == true;
```

## Decisions / where I diverged from the spec

- **Goal.RunFrom doesn't push a goal frame** — Snapshot.Resume restores the
  CallStack chain before dispatching here, so re-pushing would double-up.
- **Step.RunAsync keeps the `|| Handled` break** alongside ShouldExit —
  before-event-handled semantics differ from ShouldExit's "Handled means
  recovery, keep going" treatment. Worth a second look if a test flags it.
- **Variables.Remove for `!ask.answer`** — sentinel rides as a property of
  infra root `!ask` (path semantics); Variables.Remove takes flat keys, so
  Remove("!ask") consumes the whole root. Reserved-name pattern.

## Followups baked into commits

- `Synthetic = true` is the default — PR-loader sweep (in 2a.5) needs to
  flip to `false` on materialization.
- `App.Snapshot()` orchestration could relocate to `Snapshot.@this.Capture()`
  per architect's todos.md note — not done.
- The architect-noted naming concern (`AskCore` → `Ask`, `WriteCore` →
  `Write`) wasn't done — keeping the `Core`/`Async` split avoided wider
  rename surgery.

## To continue
Pick up at slice 2a.5. The slicing plan + survey is in
`.bot/filesystem-permission/coder/v1/stage-2a-slicing.md`.
