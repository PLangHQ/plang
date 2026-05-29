# Foundational channels snapshot taken too early — late channel registration invisible inside goal-channel scopes

**From:** builder-ergonomics session, attempting to route all build-time output through a named `"builder"` channel.
**Branch:** `builder-ergonomics`
**Date:** 2026-05-28

## What we tried

Refactored the PLang builder so every build-time `write out ...` line routes through a named `"builder"` channel, registered as goal-backed by `BuilderChannel.goal` (a one-line sink `- write out %!data%`). The intent: future swap (file log, structured JSON stream, TUI) only touches `BuilderChannel.goal`.

PLang shape:

```
# os/system/builder/Build.goal  (top of build)
- set channel "builder" call BuilderChannel
- call EmitBuildEvent kind="build-path", path=%path%
- ...
- foreach %goals%, call BuildGoal goal=%item%

# os/system/builder/EmitBuildEvent.goal  (one-call helper)
- render template "/system/builder/templates/output/build-output.template", write to %msg%
- write out %msg% channel: "builder"

# os/system/builder/BuilderChannel.goal  (the sink)
- write out %!data%
```

Every call site is `call /system/builder/EmitBuildEvent kind="...", <fields>`. Inside `EmitBuildEvent`, the write specifies `channel: "builder"`.

## Symptom

The first few `write out … channel: "builder"` succeed:

```
Building path: /workspace/plang/os
Found  goals
Building goal: Plan
```

Then deeper in the same session, the next `EmitBuildEvent` call from inside `BuildSubGoal` (line 35 of `os/system/builder/BuildGoal/Start.goal`) throws:

```
ChannelNotFound(404) — Channel 'builder' not found
  at /system/builder/EmitBuildEvent.goal:12
  Code: - write out %msg% channel: "builder"

Call stack:
  EmitBuildEvent.goal:12
  BuildGoal/Start.goal:35  (call /system/builder/EmitBuildEvent ...)
  BuildGoal/Start.goal:19  (foreach %parentGoal.Goals%, call BuildSubGoal subGoal=%item%)
  BuildGoal/Start.goal:4   (call /system/builder/EmitBuildEvent kind="goalHeader" ...)
  Build.goal:15            (foreach %goals%, call BuildGoal goal=%item%)
  Build.goal:6
```

So: same code, same channel, same session — works at the top, fails after we descend into a sub-goal that itself loops over sub-sub-goals.

## Root cause (PLang/app side)

Two pieces collide:

**1. Goal-backed channels swap the actor's channel registry to the *foundational* snapshot for the duration of each write.**

`PLang/app/channels/channel/goal/this.cs:47-78` — `InvokeGoal`:

```csharp
var foundational = Actor.FoundationalChannels;
using var _ = Actor.PushChannelsOverride(foundational);
...
return await app.RunGoalAsync(Goal, ctx, ct);
```

This is the intended recursion guard: a goal-backed channel writing to its own name would infinite-loop, so the body resolves channels against the foundational set (the entry-point streams), not the current overlay.

**2. `FoundationalChannels` is snapshotted lazily on first access.**

`PLang/app/actor/this.cs:50-53`:

```csharp
public AppChannels FoundationalChannels
{
    get => _foundationalChannels ??= _channels.Snapshot();
    private set => _foundationalChannels = value;
}
```

Which means: whoever calls `FoundationalChannels` first freezes "the foundational set" to whatever's registered at that moment. The default `output`/`input` channels are wired before user goals run, so a snapshot then doesn't include any channel a user goal registers later (like our `"builder"`).

**3. The override is `AsyncLocal`** (`PLang/app/actor/this.cs:36`), and the foundational snapshot is process-lifetime — so once anything in the call chain triggers a foundational snapshot or a goal-channel scope, downstream async work inherits a Channels view that lacks late-registered names.

## Reproduction (current branch)

1. Hand-edit, or apply the source already on `builder-ergonomics`:
   - `os/system/builder/Build.goal` has `- set channel "builder" call BuilderChannel` near the top.
   - `os/system/builder/EmitBuildEvent.goal` does `write out %msg% channel: "builder"`.
   - `os/system/builder/BuildGoal/Start.goal` calls `EmitBuildEvent` at the goal header and inside `BuildSubGoal`.
2. Rebuild all four touched .pr files (this part is rocky on its own — see "side issues" below). The rebuilt `Build.pr` has `channel.set` ✓, the rebuilt `BuildGoal/Start.pr` has `goal.call` ✓.
3. Trigger a build that exercises a goal-with-sub-goals path:
   ```
   cd os && ../PlangConsole/bin/Debug/net10.0/plang \
     '--build={"files":["/system/builder/BuildGoal/Plan.goal"],"cache":false}'
   ```
   Plan.goal has a sub-goal (QueryAndValidatePlan), so `BuildSubGoal` fires, which calls `EmitBuildEvent`, which fails with ChannelNotFound.

The top-level `Building goal: Plan` line prints (from Start.goal:4), proving the channel **was** registered. The sub-goal call fails ~one step later in the same session, proving the channel resolution view shifted underneath.

## Expected behavior

`set channel "builder" call BuilderChannel` should be visible to every `write out … channel: "builder"` for the rest of the build, including writes inside sub-goals invoked through goal-backed-channel recursion guards.

## What probably needs to change (PLang/app)

A few angles, pick one:

1. **Foundational snapshot opt-in**, not lazy. Don't snapshot on first access — snapshot at an explicit lifecycle point (after entry-point channel wiring is done but before any user goal runs). Or take the snapshot fresh per `InvokeGoal` call. Goal-channel's recursion guard probably only needs "don't resolve to myself", not "freeze the whole registry."
2. **Refresh foundational on registration.** When `channel.set` adds a new channel, update `_foundationalChannels` to include it (or invalidate so the next access re-snapshots).
3. **Override stacks instead of swaps.** Goal-channel pushes an overlay that hides its own name but defers other lookups to the live `_channels`. Recursion guard intact; new registrations visible.

Option 3 is probably the cleanest in spirit ("don't resolve to myself"), but option 2 is the smallest patch.

## Workaround we used to keep building

For now `EmitBuildEvent.goal` writes to the default output channel (no `channel:` clause). We lose the swap-the-sink-later seam. `BuilderChannel.goal` and `set channel "builder" ...` stay in source as documentation of intent, but nothing routes through them until the runtime fix lands.

## Side issues surfaced during this work (not the focus, just so you know)

- **Planner sometimes picks `event.on` for `call <Identifier>`** lines whose identifier or parameters contain "Start" / "Event" / "Goal". Plan.llm explicitly says `call X` is always `goal.call`, but the planner ignores it in those cases. Rename of the offending word (e.g. `kind="goalStart"` → `kind="goalHeader"`) unblocks it. Worth strengthening Plan.llm or making the catalog teach the planner more aggressively. (Renaming the helper away from `EmitBuildEvent` might also help — the "Event" in the name primes `event.on` too.)
- **Absolute goal paths**: `call /EmitBuildEvent` compiled with `prPath: null` (the resolver couldn't translate the leading `/` from a non-sibling goal). Workaround: spell the full path — `call /system/builder/EmitBuildEvent`. Worth supporting `/Name` as "absolute from app root" the way the test runner resolves goals.
- **Compound `if X is "A" or X is "B"` compiles to a broken `condition.if(Operator="or")`** that always evaluates true. Splitting into two if-statements (one per branch) works.
- **Variable-to-variable type inference in `goal.getTypes`** (`PLang/app/modules/goal/getTypes.cs:92-95`): for `set %x% = %y%`, the snapshot uses `valueParam.Type` (often "object") rather than looking up `%y%`'s known type in the working map. Not directly causing this bug, but came up while diagnosing.
