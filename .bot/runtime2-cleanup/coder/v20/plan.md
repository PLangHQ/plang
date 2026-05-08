# Stage 20 — coder plan (`channel-app-backref-drop`)

After stage 1 added `Channel.@this.Channels` (parent collection back-ref),
`Channel.@this.App` became redundant — App is reachable via the parent
chain. Drop the redundant App back-ref.

## Files

- `PLang/App/Channels/Channel/this.cs`:
  - Delete `public global::App.@this? App { get; internal set; }` and its doc.
  - `MatchingBindings` (line 192 area): `App != null` branch → `Channels?.App is { } app`. The brief said `Channels?.Actor?.App` but that's null for Service-owned Channels (Service is not an Actor); navigating directly through `Channels.App` covers both User/Service/System actor-owned and Service-owned cases.
  - `InvokeChannelHandler` (line 247 area): the diagnostic `App?.Debug?.Write(...)` reach was a *second* reader the brief's grep missed. Updated to `Channels?.App?.Debug?.Write(...)`.
- `PLang/App/Channels/this.cs`:
  - Delete `channel.App = _app;` from `Register`.
  - **Add** `public App.@this App => _app;` getter — needed because Channel needs to navigate through Channels to App. The brief's `Channels.Actor.App` chain breaks for Service-owned Channels; exposing `Channels.App` directly is the natural navigation point.
  - Qualified existing `App.Data.*` / `App.Errors.*` references with `global::` because the new `App` property shadows the namespace inside this class.

## Verification

- `grep -rn "channel\.App\b\|Channel\.@this\.App" PLang/ PLang.Tests/ --include='*.cs'` → 0
- `grep -n "public global::App.@this? App" PLang/App/Channels/Channel/this.cs` → 0
- C# 2752/2752; PLang 199/199; build clean.

## Note

The brief assumed `Channels.Actor.App` was sufficient navigation. Running
the tests surfaced that Service-owned Channels have no Actor (Services
aren't Actors), so the chain returned null and the cross-actor binding
test failed. Exposing `Channels.App` (which is always non-null because
every Channels takes an App in its ctor) is the simpler and complete
navigation point.
