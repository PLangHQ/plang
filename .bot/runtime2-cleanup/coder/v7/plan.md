# Stage 7 — coder plan (`callstack-promote-app-property`)

Promote `app.Debug.CallStack` to `app.CallStack`. Same instance, same scope
(one shared per app); just relocates the property to align with where the
folder/namespace already lives.

## Files

- `PLang/App/this.cs` — add `public CallStack.@this CallStack { get; } = new();` near the other shared subsystem properties.
- `PLang/App/Debug/this.cs`:
  - Delete the `App.CallStack.@this CallStack { get; }` property.
  - Delete the `CallStack = new App.CallStack.@this();` allocation in the ctor.
  - Update `Apply`'s internal use: `CallStack.Flags = ...` → `_engine.CallStack.Flags = ...`.
- `PLang/App/Actor/Context/this.cs` — read-through accessor: `App.Debug?.CallStack` → `App?.CallStack`. Doc-comment refreshed.

## Caller sweep (production — 9 sites)

The brief listed 7 production callers; I found 2 extra during the grep:
- `PLang/App/Variables/this.SnapshotAt.cs:19` — `_context?.App?.Debug?.CallStack` → `_context?.App?.CallStack`.
- `PLang/App/Errors/this.cs:70` — `App?.Debug?.CallStack` → `App?.CallStack`.

Brief's 7 + these 2:
- `PLang/App/this.cs:431` (self-reference: `Debug.CallStack` → `CallStack`)
- `PLang/App/this.Snapshot.cs:25`
- `PLang/App/Goals/Goal/this.cs:288, 312`
- `PLang/App/CallStack/this.Snapshot.cs:142`
- `PLang/App/modules/output/ask.cs:49`
- `PLang/App/Callback/ErrorCallback.cs:72`

Plus a stale doc-comment in `PLang/App/CallStack/this.cs:7` updated.

## Caller sweep (tests — 11 files)

`PLang.Tests` — `Debug.CallStack` → `CallStack` across:

- `App/Context/PLangContextTests.cs` (1)
- `App/CallStackTests/FlagsDiffAutoFlipTests.cs` (5)
- `App/CallStackTests/CallStackSnapshotTests.cs` (5)
- `App/CallStackTests/EventsSinceTests.cs` (2)
- `App/CallStackTests/CallSnapshotTests.cs` (8)
- `App/CallStackTests/CallbackTests/FailureMatrixTests.cs` (2)
- `App/Debug/DebugCallStackParseTests.cs` (6)
- `App/CallbackTests/ErrorCallbackTests.cs` (4)
- `App/Modules/debug/TagActionTests.cs` (3)
- `App/VariablesTests/SnapshotAtErrorTests.cs` (5)

## Verification

- `grep -rn "Debug\.CallStack\|Debug?\.CallStack" PLang/ PLang.Tests/ Tests/ --include='*.cs'` → 0
- C# 2755/2755; PLang 199/199; build clean.
