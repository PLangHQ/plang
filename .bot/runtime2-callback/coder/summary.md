# coder summary — runtime2-callback

## Version
v1 — Stage 1 (Snapshot Foundation)

## What this is

Implements Stage 1 of the architect's 4-stage callback design (`.bot/runtime2-callback/architect/stage-1-snapshot-foundation.md`): the snapshot/restore plumbing every callback record will sit on top of. After this stage, six App subsystems can capture themselves into a typed Snapshot tree and rebuild from it on a fresh App.

This is the foundation only. No CallStack, no time-travel for Variables, no Data signing, no callback records — those are Stages 2/3/4.

## What was done

### New types
- `App.Snapshot.ISnapshotted` (`PLang/App/Snapshot/ISnapshotted.cs`) — `void Capture(Snapshot.@this s)` + `static abstract void Restore(Snapshot.@this s, Context.@this ctx)`. The marker; the type system is the classifier.
- `App.Snapshot.@this` (`PLang/App/Snapshot/this.cs`) — typed read/write tree. `Section(name)` returns a child snapshot for nested ISnapshotted. `Write<T>` / `Read<T>` / `Has` / `HasSection`. Underlying storage stays private.
- `App.Statics.@this` (`PLang/App/Statics/this.cs` + `this.Snapshot.cs`) — extracts the App's inline `_statics` dict into its own `@this` so it can implement ISnapshotted. App keeps a `GetStatic(key)` shim for legacy callers.

### Subsystems converted to ISnapshotted (partial-class additions)
- `App.@this` — `Snapshot()` + `Restore(snap, ctx)` walking the subsystem list (`PLang/App/this.Snapshot.cs`).
- `App.Variables.@this` — captures full Data shape (Name, Value, Type, Properties via `Clone()`); honours existing partition (skip `!`-prefix, DynamicData, SettingsVariable).
- `App.Errors.@this` + `App.Errors.Trail.@this` — Trail captures entries; Restore replaces the live Trail with one populated from the snapshot AND freezes it (rejects further `Add`).
- `App.Providers.@this` — registry-layer two-step capture/restore: non-built-in `(typeName, providerName, source)` tuples + default-selection overrides (only when current default differs from the type's *born* default — tracked via `_builtInDefaults` dict to survive `SetDefault`).
- `App.Build.@this` / `App.Test.@this` — capture/restore `IsEnabled` only. Other fields (Files, Cache, Results, Coverage, etc.) are reconstruct-on-build.

### IProvider extensions
- Added `bool IsBuiltIn { get; set; }` and `string? Source { get; set; }` to `IProvider`. Updated all 12 production providers + 13 test providers to implement them. `RegisterDefaults` now goes through `RegisterBuiltIn<T>` which stamps `IsBuiltIn = true` and remembers the type's born default. `provider.load.cs` stamps `Source = absoluteDllPath`.

### Errors hard-error type
- `App.Providers.ProviderRestoreException` — referent-integrity hard error raised by Providers.Restore when a captured registration's source DLL can't be loaded, the impl type can't be found in the DLL, or a default-selection name doesn't resolve. No silent fallback to system defaults.

### Tests filled
17 of the 24 test-designer Stage-1 stubs flipped from red to green:
- `SnapshotInterfaceTests` × 2
- `AppSnapshotTests` × 4
- `StaticsAndModesSnapshotTests` × 3
- `VariablesSnapshotTests` × 2
- `ErrorsTrailSnapshotTests` × 2
- `ProvidersSnapshotTests` × 6 (including the two hard-error tests using a `/nonexistent/ghost.dll` source path)

C# baseline: 97 stubs failing → 80 stubs failing (the 80 are Stages 2-4 stubs, expected red).
PLang baseline: 192/181/0fail/11stale → 192/181/0fail/11stale. No regression.

## Code example

The OBP shape — every subsystem owns its own snapshot in its own partial file:

```csharp
// PLang/App/Build/this.Snapshot.cs
namespace App.Build;

public sealed partial class @this : ISnapshotted
{
    public void Capture(Snapshot.@this s) => s.Write("isEnabled", IsEnabled);

    public static void Restore(Snapshot.@this s, Actor.Context.@this ctx)
        => ctx.App.Build.IsEnabled = s.Read<bool>("isEnabled");
}
```

App.Snapshot()/Restore() is then a thin walk:

```csharp
public Snapshot.@this Snapshot()
{
    var s = new Snapshot.@this();
    Variables.Capture(s.Section("Variables"));
    Errors.Capture(s.Section("Errors"));
    Providers.Capture(s.Section("Providers"));
    Statics.Capture(s.Section("Statics"));
    Build.Capture(s.Section("Build"));
    Testing.Capture(s.Section("Testing"));
    return s;
}
```

Adding a new subsystem to the snapshot in future stages is two things: implement `ISnapshotted` and add one line here. No central registry, no per-subsystem ordering coupling.

## Next

Stage 2 — `Call.@this` and `App.CallStack.@this` Capture/Restore with Goal-stub + hash-match, `EventsSince(t)` query, `Variables.SnapshotAt(error)`, `Flags.Diff` auto-flip in error path. Will turn the `[S2]` test-designer stubs green (~16 tests).
