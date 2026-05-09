# coder — runtime2-cleanup

## Version
v23 — Stage 23: `RestoredFrame` → `Call/Position` rename.

## What this is
Architect's Tier 5 hygiene cleanup carried over from stage 10. The snapshot record for one Call frame was named `RestoredFrame` and lived at `PLang/App/CallStack/RestoredFrame.cs`, while the live counterpart `Call.@this` sits one folder deeper at `CallStack/Call/this.cs`. The property on `ICallback`, `AskCallback`, and `ErrorCallback` was already named `Position` — only the type name disagreed. Renaming type+folder removes the cognitive bump and puts the snapshot beside its live counterpart.

## What was done
Mechanical rename per stage-23 spec:
- `git mv PLang/App/CallStack/RestoredFrame.cs PLang/App/CallStack/Call/Position.cs`
- Namespace `App.CallStack` → `App.CallStack.Call`; record `RestoredFrame` → `Position`. Body unchanged.
- Caller sweep across 11 files (4 production + 7 tests):
  - Production: `CallStack/this.Snapshot.cs`, `Callback/ICallback.cs`, `Callback/AskCallback.cs`, `Callback/ErrorCallback.cs`
  - Tests: 4 CallbackTests files, 2 DataTests files, 1 Serializers test
- Verified: `dotnet build PlangConsole` clean, C# 2752/2752 pass, PLang 199/199 pass, `grep RestoredFrame` zero hits.

### Namespace gotcha
First sweep used `Call.Position` everywhere assuming `using App.CallStack;` would expose the `Call` sub-namespace. C# `using` brings types from the namespace but does not import sub-namespaces by short name. Build failed with CS0246 in Callback files. Fixed by switching every site outside `App.CallStack` itself to fully-qualified `global::App.CallStack.Call.Position`. Inside `this.Snapshot.cs` (namespace `App.CallStack`), the short `Call.Position` form works because `Call` is a direct sub-namespace of the file's own namespace.

## Code example
Before (`ICallback.cs`):
```csharp
global::App.CallStack.RestoredFrame? Position { get; }
```
After:
```csharp
global::App.CallStack.Call.Position? Position { get; }
```

## Stage closure
- File move: ✓
- Namespace + type rename: ✓
- Caller propagation (11 files): ✓
- C# tests green: 2752/2752
- PLang tests green: 199/199
- Behaviour change: none.
