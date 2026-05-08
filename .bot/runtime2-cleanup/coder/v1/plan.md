# Stage 1 — coder plan

## What

Stage 1 of `runtime2-cleanup`: per-actor `Channels.Serializers` is the single home.

- Delete `App.@this.Serializers` property (the App-root shortcut that bypasses actors).
- Drop `Channel.Stream.@this._serializers` field + `Serializers` property (third
  copy lazily allocated per stream).
- Add `Channels` back-ref on `Channel.@this`; set it in `Channels.Register`.
- Route `Stream.WriteCore` through `Channels!.Serializers`.
- Sweep all 5 production caller sites + ~15 test sites to per-actor form.

## File edits

### Production (5 files)

1. `PLang/App/this.cs` — delete the `Serializers` property at line 154 (and adjacent doc comment).
2. `PLang/App/Channels/Channel/this.cs` — add `Channels` back-ref alongside `App`:
   `public global::App.Channels.@this? Channels { get; internal set; }`
3. `PLang/App/Channels/Channel/Stream/this.cs` — delete `_serializers` field
   and `Serializers` property; `WriteCore` calls `Channels!.Serializers.SerializeAsync(...)`.
4. `PLang/App/Channels/this.cs`:
   - `Register` adds `channel.Channels = this`.
   - Lines 176, 204 — `sc.Serializers.X` → `Serializers.X` (this collection's own).
   - Drop the now-stale "Stage 6 promotes…" comments.
5. Caller sweep:
   - `PLang/App/Goals/this.cs:320, 325` — `app.Serializers` → `app.System.Channels.Serializers`
   - `PLang/App/Goals/Setup/this.cs:56` — same
   - `PLang/App/modules/file/providers/DefaultFileProvider.cs:99` — `action.Context.Actor.Channels.Serializers`
   - `PLang/App/Actor/Context/this.cs:172` — `() => Actor!.Channels.Serializers`

### Tests (6 files)

`app.Serializers` → `app.User.Channels.Serializers` in:
- `PLang.Tests/App/Serializers/JsonSerializerRoundTripTests.cs` (3 sites)
- `PLang.Tests/App/Serializers/MimeRegistrationTests.cs` (5 sites)
- `PLang.Tests/App/Serializers/PlangDataSerializerRoundTripTests.cs` (5 sites)
- `PLang.Tests/App/CallbackTests/FailureMatrixTests.cs` (1 site)
- `PLang.Tests/App/Core/EngineTests.cs` (3 sites — `engine.Serializers` → `engine.User.Channels.Serializers`)
- `PLang.Tests/App/ChannelsTests/Stage6_EntryPointWiringTests.cs` —
  rename test `AppThis_SerializersExists_AtAppLevel` → `AppThis_SerializersExists_PerActor`
  and assert `app.User.Channels.Serializers` is non-null.

## Verification

- `grep -rn "app\.Serializers\b\|App\.Serializers\b" /workspace/plang/PLang/ --include='*.cs'` → 0 hits.
- `grep -rn "_serializers" /workspace/plang/PLang/App/Channels/ --include='*.cs'` → 0 hits.
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` 2755/2755.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` 199/199.
