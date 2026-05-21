# Coder — filesystem-permission

## Version
v1

## What this is
PLang's filesystem permission system + unified suspend/resume mechanism
(Snapshot replaces AskCallback/ErrorCallback). 5 stages per the architect's
plan; this session completed stages 1, 2a (all 8 slices), 2b, 3. Stages 4
and 5 remain.

## What was done

| Stage | Status | Commits |
|-------|--------|---------|
| 1 — Permission types | ✅ | `4143621b8` |
| 2a.1 — primitives (IExitsGoal, Type.Exit, Data.Snapshot, Action.Synthetic) | ✅ | `b06c64380` |
| 2a.2 — step-loop short-circuit | ✅ | `1744d3f13` |
| 2a.3 — Step.RunFrom + Goal.RunFrom | ✅ | (squashed in v1 chain) |
| 2a.4 — Channel.Ask takes action; Message returns Data<Ask> | ✅ | `30f503396` |
| 2a.5 — action owns its execution (drop App.Run/cause) | ✅ | `dff9051c4` |
| 2a.6 — Snapshot.Resume + callback.run rewrite | ✅ | `797f7eee0` |
| 2a.7 — delete dead callback classes | ✅ | `d47e025a2` |
| 2a.8 — cross-goal `.test.goal` fixtures | ✅ | `8dc9e0e01` |
| 2b — Path.Authorize + PermissionDenied | ✅ | `3bf37c4a9` |
| 3 — Actor.Permission storage (in-memory + sqlite) | ✅ | `15c165ee0` |
| 4 — IPLangFileSystem v2 (FS surface rewrite) | **TODO** | — |
| 5 — Messages end-to-end fixture | **TODO** (needs 4) | — |

## Test suite

C#: **2824 tests, 40 failing**. All remaining failures are stubs for stages
4 and 5 (Move/Copy bundled consent, OutOfRoot stream/message,
FileSystemSurfaceShape, FileAccessControl absence, ValidatePath absence,
end-to-end Messages flow, etc.).

PLang `--test`:
- `Callback/StatelessCrossGoalResumes` → Pass
- `Callback/StatefulAskMidGoalBindsValue` → Pass
- Existing test corpus → no new regressions

## Decisions / where I diverged from the spec

- **Goal.RunFrom doesn't push a goal frame** — Snapshot.Resume restores the
  CallStack chain before dispatching here, so re-pushing would double-up.
- **Step.RunAsync keeps `|| Handled` break alongside ShouldExit** —
  preserves before-event short-circuit semantics.
- **Variables.Remove for `!ask.answer`** — uses `Remove("!ask")` (path
  semantics: sentinel rides as property of infra root `!ask`).
- **AskCore/WriteCore naming kept** — architect flagged "Just Ask" as a
  taste preference; renaming would have touched `WriteAsync`/`ReadAsync`
  pattern across the channel surface. Deferred.
- **RunAction<T> kept on App.@this** as the inline-C#-composition entry —
  spec called for full deletion but doing so cleanly requires a source-
  generator surface change (handler classes would need their own RunAsync
  that wraps in an entity). RunAction now routes through
  Action.@this.RunAsync via a PreboundHandler property — same dispatch
  path as PR-loaded actions, synthetic-stamped.
- **PreboundHandler property added to Action.@this** for the inline path.
  When set, DispatchAsync skips the entity-driven property reset (calls
  `handler.ExecuteAsync(null!, ctx)`) so inline-set properties survive.
- **Cause linkage dormant** — `cause` parameter removed from
  `CallStack.Push` and `Action.RunAsync`. `Call.@this.Cause` property stays
  but is always null (renderers no-op naturally). Recovery dispatch in
  `error/handle.cs:RunRecovery` no longer threads cause.
- **`Synthetic = true` default + PR-loader flip** in
  `Goals.LoadGoalFile` / `Setup.LoadFile` — PR actions get `Synthetic=false`
  on materialisation.
- **Glob path matching uses `Microsoft.Extensions.FileSystemGlobbing`** as
  the architect specified — added to `PLang/PLang.csproj`.
- **Permission signature routing**: signed grants go to sqlite, unsigned
  to in-memory. The signing surface doesn't expose an Expires parameter
  today, so the "AlwaysExpiry" constant is unused — Path.Authorize's
  `"a"` branch calls `EnsureSigned()` (no expiry arg). Tracked: when the
  signing layer grows Expires, pipe `AlwaysExpiry` through.

## Code example

`Type.Exit()` — the engine's only Exit-type discriminator:

```csharp
// PLang/App/Types/Exit.cs
public static class TypeExitExtensions
{
    public static bool Exit(this System.Type? clrType)
        => clrType != null && typeof(global::App.IExitsGoal).IsAssignableFrom(clrType);
}
```

`Path.Authorize` (stage 2b) — the permission gate:

```csharp
public async Task<Data.@this> Authorize(Verb verb, string prefix = "")
{
    var actor = Context?.Actor ?? throw …;
    if (actor.Permission.Find(this, verb) != null) return Data.@this.Ok();
    var askAction = new modules.output.ask { Context = Context, Question = … };
    var askResult = await Context!.App.RunAction(askAction, Context);
    if (askResult.Type?.ClrType.Exit() == true) return askResult; // stateless bubble
    return askResult.Value?.ToString()?.Trim() switch {
        "a" => SignAndStore(actor, verb, persist: true),
        "y" => SignAndStore(actor, verb, persist: false),
        "n" => Data.@this.FromError(new PermissionDenied(BuildRequest(actor, verb))),
        _   => await Authorize(verb, prefix: $"Invalid answer '...'. "),
    };
}
```

## To continue (stages 4 + 5)

**Stage 4** — `IPLangFileSystem` v2. Per the architect's 5 sub-stages:

1. Define v2 interface (Path-in, Data-out, verb-baked). ~11 methods.
2. Implement `Default` against v2 — each operation calls
   `path.Authorize(verb)` first, propagates Exit-typed results unchanged.
3. Rewrite each `PLang/App/modules/file/*.cs` action handler — one
   commit per action (read, save, delete, copy, move, list, exists).
4. Rewrite non-action call sites — builder, snapshot, settings, channels,
   cache, runtime infra. ~50-100 sites.
5. Delete old surface: `ValidatePath(string)`, `FileAccessControl`,
   `IFileSystem` inheritance.

Watch-outs:
- Path resolution semantics must match `ValidatePath` (system fallback,
  // OS-rooted, root-resolved default).
- Builder `.pr` snapshotting in `Default.Read` — preserve.
- Stream operations: a few real consumers (uploads, large reads). Decide
  whether v2 keeps stream-returning methods or routes through bytes/text.

**Stage 5** — Messages end-to-end fixture under `Tests/Permission/`. Six
steps testing no-grant suspend / grant-a-store / no-prompt re-query /
restart persistence / revoke-reprompt / narrowed grant. Depends on Stage 4
being green.

Stubs already exist for both stages under
`PLang.Tests/App/FileSystem/SurfaceTests/` and `Tests/Permission/`.
