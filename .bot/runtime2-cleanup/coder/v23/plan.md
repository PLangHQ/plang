# v23 — Stage 23: RestoredFrame → Call/Position rename

Pure mechanical rename per architect's stage-23 spec.

1. Move `PLang/App/CallStack/RestoredFrame.cs` → `PLang/App/CallStack/Call/Position.cs` via `git mv`.
2. Change namespace `App.CallStack` → `App.CallStack.Call`; record name `RestoredFrame` → `Position`. Body unchanged.
3. Sweep 11 files (4 production + 7 tests) replacing the type reference.
4. Build PlangConsole, run C# suite (`dotnet run --project PLang.Tests`) and PLang suite (`cd Tests && plang --test`). Confirm both green.

## Note on namespace resolution

`using App.CallStack;` doesn't expose the sub-namespace `Call` by short name in C# — only the file whose own namespace is `App.CallStack` (this.Snapshot.cs) can write `Call.Position` directly. Every other site needs the fully-qualified `global::App.CallStack.Call.Position`. Initial pass tried `Call.Position` everywhere; build failed with CS0246 in Callback files, switched to fully-qualified form.
