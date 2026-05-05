# Coder v1 — Stage 1: Snapshot Foundation

Implements architect's `stage-1-snapshot-foundation.md`. Lands the snapshot interface and converts the six simple subsystems (Variables, Errors.Trail, Providers, _statics, Build, Testing). No CallStack, no Variables time-travel, no Data signing, no callbacks — those are Stages 2/3/4.

## Baseline

C# tests pre-Stage-1: 2720 total, 2623 passing, 97 failing — and **all 97 failures are test-designer stubs** (`Assert.Fail("Not implemented")`). Pre-existing real failures: 0. Build clean.

## Stage 1 deliverables (architect's list, with my exact landing sites)

| Architect deliverable | File | Notes |
|---|---|---|
| `ISnapshotted` interface | `PLang/App/Snapshot/ISnapshotted.cs` | `void Capture(Snapshot.@this s)` + `static abstract void Restore(Snapshot.@this s, Context.@this ctx)` |
| `Snapshot.@this` payload type | `PLang/App/Snapshot/this.cs` | Tree of named sections; each section holds typed entries. `Section(name)` returns a child Snapshot for the subsystem to write into. Read API on the same type. |
| `App.Snapshot()` | `PLang/App/this.cs` (extend) | Walks the App's `ISnapshotted` properties; calls `subsystem.Capture(snap.Section("Name"))`. Explicit list (not reflection) — keeps the wiring auditable. |
| `App.Restore(snap, ctx)` | `PLang/App/this.cs` (extend) | Dual: dispatches each section to the matching `@this.Restore`. |
| `Variables` Capture/Restore | `PLang/App/Variables/this.Snapshot.cs` (partial) | Honours existing partition (skip `!`-prefix, DynamicData, SettingsVariable). Captures full Data shape (Name, Value, Type, Properties) — not just key→value. |
| `Errors.Trail` Capture/Restore | `PLang/App/Errors/Trail/this.Snapshot.cs` (partial) | Captures the entries; restored Trail is in a "frozen" mode that throws on `Add`. |
| `Providers` Capture/Restore | `PLang/App/Providers/this.Snapshot.cs` (partial) | Two-step: replay non-built-in registrations (with source path), then apply default-selection overrides. Hard error on unresolvable source DLL or unknown default name. |
| `_statics` Capture/Restore | New `PLang/App/Statics/this.cs` + `this.Snapshot.cs`; replace App's inline `_statics` field | The architect calls this out as a `@this` opportunity — App's internal `ConcurrentDictionary<string, ConcurrentDictionary<string, object?>>` becomes its own type. App keeps `GetStatic(key)` as a thin shim. |
| `Build` Capture/Restore | `PLang/App/Build/this.Snapshot.cs` (partial) | `IsEnabled` only. |
| `Testing` Capture/Restore | `PLang/App/Test/this.Snapshot.cs` (partial) | `IsEnabled` only. |
| C# tests for round-trip + Providers hard errors | (test-designer wrote stubs; I fill bodies) | Make all `[S1]` tests green. |

## Snapshot.@this design

The architect rejected exposing entries as a public collection. So the type is a typed read/write surface. Shape:

```csharp
public sealed class @this
{
    private readonly Dictionary<string, @this> _sections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _entries = new(StringComparer.OrdinalIgnoreCase);

    public @this Section(string name) => _sections.TryGetValue(name, out var s) ? s : (_sections[name] = new());
    public bool HasSection(string name) => _sections.ContainsKey(name);
    public IReadOnlyDictionary<string, @this> Sections => _sections;

    public void Write<T>(string key, T value) => _entries[key] = value;
    public T? Read<T>(string key) => _entries.TryGetValue(key, out var v) ? (T?)v : default;
    public bool Has(string key) => _entries.ContainsKey(key);
}
```

Subsystem authors only see the typed `Section/Write/Read` surface; they never touch the underlying dicts. The shape is a tree so each subsystem owns its own subtree.

## Providers two-step Restore

Add to `IProvider`:
- `string? Source { get; set; }` — DLL path for loaded providers; null for built-in defaults and in-process registrations.
- `bool IsBuiltIn { get; set; }` — set inside `RegisterDefaults()`; skipped during Capture.

`provider.load.cs` sets `instance.Source = fullPath`.

Capture: emit list of `(typeFullName, providerName, source)` tuples for non-built-in registrations + a dict of default-selection overrides where the current default differs from the built-in default.

Restore (in order):
1. For each captured registration, if Source is non-null, load the DLL via `Assembly.LoadFrom`; instantiate; register against each `IProvider`-derived interface (mirrors `provider/load.cs`). Hard error on load failure.
2. Apply default-selection overrides via `SetDefault(type, name)`. Hard error if the named provider isn't registered after step 1.

Hard errors raise a typed `ServiceError` with `Key = "ProviderRestoreFailed"` (or similar referent-integrity code).

## Errors.Trail "frozen" mode

The test `ErrorsTrail_AfterRestore_IsReadOnly` demands the restored Trail throws on Add. Add `bool IsFrozen { get; }`; `Add()` throws `InvalidOperationException` when frozen. Restore creates the new Trail and freezes it.

But `App.Errors.Trail` is auto-instantiated as `new()`. To replace with a frozen one, expose a setter via `Errors.RestoreTrail(entries)` or make Trail's freeze a state mutation. Simplest: `Trail.SetEntries(IEnumerable<IError>)` which clears, populates, and freezes.

## Build / Testing snapshot scope

Only `IsEnabled`. The other fields (`Files`, `Cache`, `_prSnapshot` on Build; `Results`, `Coverage`, `CurrentTest`, config fields on Testing) are out of scope per architect — those are reconstruct-on-build / live-IO.

## Test conventions reminder

Per CLAUDE.md, I cannot create `PLang.Tests.App.Snapshot` namespace because of the global `Snapshot` alias clash — must use `SnapshotTests`. Test-designer already followed this. I'll write helper utilities under the same convention.

Also: `App.Snapshot.@this` namespace would clash with the alias `Snapshot = App.Snapshot.@this`. The actual class lives at `App.Snapshot` namespace + `@this` class — that's the OBP convention everywhere, so it's fine; the alias resolves to the type.

## Workflow

1. Write `ISnapshotted` + `Snapshot.@this` (no behaviour yet).
2. Add the global aliases `Snapshot`, `ISnapshotted` to `PLang/App/GlobalUsings.cs`.
3. Implement each subsystem's Capture/Restore in dependency order: Variables → Errors.Trail → Statics → Build → Testing → Providers.
4. Wire `App.Snapshot()` / `App.Restore()` calling each subsystem.
5. Fill the 24 test-designer test bodies in the `[S1]` set.
6. Build clean; all 24 [S1] tests green; 73 stubs remain red (Stages 2-4).
7. PLang tests: nothing in the [S1] set touches `plang --test`, but I'll run a clean rebuild + the suite to confirm no regressions.
8. Commit + push (no PR per Ingi).

## What's deliberately NOT in this stage

- `App.CallStack` Capture/Restore (Stage 2)
- `Call.@this` Capture/Restore + Goal-stub + hash-mismatch (Stage 2)
- `Variables.SnapshotAt(error)` (Stage 2)
- `Data.Signature` lazy property (Stage 3)
- Any callback record (Stage 4)

## Risks / open questions

- **`_statics` extraction**: The architect calls `_statics` provisional and notes "see todos.md for the goal-backed dynamic property follow-up." I'll extract it into `Statics.@this` to make `ISnapshotted` work cleanly — but won't redesign the property model. Just enough OBP shape to participate.
- **Providers source-path roundtrip**: For DLLs that genuinely don't exist on the resume side, the test demands a hard error. That requires actually trying to load — which is real `Assembly.LoadFrom`, no mocking. The test will create a fake path and assert the error fires.
- **Test-designer's `Variables_RoundTrip` "PreservesValuesAndProperties" + deep-equal**: I'll verify Properties survives. Properties is a `Properties` object — its serialization is up to the Capture body.
