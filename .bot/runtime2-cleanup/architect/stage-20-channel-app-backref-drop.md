# Stage 20: `channel-app-backref-drop`

**Read first:**
- `plan/principles.md` — OBP discipline, especially the smell about overlapping back-refs (god-bag pattern) — Channel held both `App` and `Channels` after stage 1; one was redundant.
- `plan/scope-map.md` — Channels is per-actor; Channel-instance back-refs follow the parent (Channels) chain to reach App when needed.

**Goal:** Drop `Channel.@this.App` back-ref now that stage 1 added `Channel.@this.Channels`. The single reader (`MatchingBindings` line 194) navigates via `Channels?.Actor?.App` instead of the direct back-ref. Single navigation point per the OBP discipline.

**Scope:**
- *Included:* delete `public global::App.@this? App { get; internal set; }` at `Channel/this.cs:75`; delete `channel.App = _app;` at `Channels.this.cs:90` (the registration setter); update the one reader at `Channel/this.cs:194` to navigate via `Channels`.
- *Excluded:* anything else. Pure 3-line cleanup.

**Deliverables:**
- `PLang/App/Channels/Channel/this.cs`:
  - Delete the `App` property (line 75) and its doc-comment (line 74).
  - Update `MatchingBindings` (line 194 area):

```csharp
// Today (lines 192–199):
// App-level bindings — match across actors so one binding can cover
// every channel-of-name "logger" regardless of which actor owns it.
if (App != null)
{
    foreach (var b in App.Events.GetBindings(type))
        if (string.Equals(b.ChannelName, Name, StringComparison.OrdinalIgnoreCase))
            yield return b;
}

// After:
// App-level bindings — match across actors so one binding can cover
// every channel-of-name "logger" regardless of which actor owns it.
// Navigation: Channels (parent collection) → Actor → App.
if (Channels?.Actor?.App is { } app)
{
    foreach (var b in app.Events.GetBindings(type))
        if (string.Equals(b.ChannelName, Name, StringComparison.OrdinalIgnoreCase))
            yield return b;
}
```

- `PLang/App/Channels/this.cs:90`:
  - Delete `channel.App = _app;`. The `channel.Channels = this;` line (added by stage 1) stays — it's the new path.

- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** Stage 1 (added `Channel.Channels` back-ref). Already landed.

## Design

### The smell this closes

After stage 1 added `Channel.@this.Channels`, the existing `Channel.@this.App` became redundant. App was reachable two ways:
- `channel.App` (direct back-ref, set during registration).
- `channel.Channels.Actor.App` (navigation via the new back-ref → actor → App).

Two paths to the same destination is the god-bag pattern (multiple overlapping back-refs on a single class). The OBP discipline says one navigation point. Stage 1 added the more-specific one (Channels — the immediate parent). Stage 20 drops the less-specific shortcut (App — the grandparent reachable through Channels).

### Why navigate instead of keeping the shortcut

The principle in `principles.md`: "classes hold whatever back-ref(s) they actually need." Channel needs to reach its parent Channels (for Serializers — stage 1's work). Channel doesn't need a direct App back-ref because every place that needs App can navigate via Channels.Actor.App. Removing the redundant ref keeps Channel's surface honest about its real dependencies.

### Files touched

**Files modified (2):**
- `PLang/App/Channels/Channel/this.cs` — property deletion + one reader update.
- `PLang/App/Channels/this.cs` — one line deletion.

### Risk + dependencies

**Risk: very low.** One reader, mechanical navigation update. Only failure mode is a grep miss on `channel.App` readers — but the grep was thorough (zero readers outside the Channel.@this file's own `MatchingBindings`).

### Tests

**No new tests required.** Behavior unchanged.

**Existing test coverage to verify:**
- `PLang.Tests/App/Channels/` — channel I/O and event binding tests.
- `Tests/` — full PLang suite.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -n "Channel\.@this\? App\|public global::App\.@this? App" PLang/App/Channels/Channel/this.cs` — zero hits.
- `grep -rn "channel\.App\b" PLang/ PLang.Tests/ --include='*.cs'` — zero hits.

### Watch for (coder eyes-on)

- **The `Channels?.Actor?.App` chain** — three null-checks. The pattern `is { } app` captures the non-null in one go. Both Channels and Actor are nullable on Channel.@this (set internally, may be null pre-registration). After registration, both are non-null in steady state.
- **Tests that construct Channel directly** without registering — they may have bypassed Register and the App back-ref was never set anyway. Stage 20 doesn't change their pre-registration state; if they currently work, they'll still work.
- **The doc-comment on the App property** at line 74 — delete it along with the property.

### Stages that follow this one

- **Stage 14** (`timespan-iso-8601-sweep`) — same Tier 4 batch; independent.
- Other Tier 4 stages (15–19, 21, 22) follow.

### Out of scope

- Anything else on Channel.@this — Actor, Channels, Events properties stay.
- Reorganization of MatchingBindings logic — only the one branch updates.

## Commit plan

```
runtime2-cleanup stage 20: drop Channel.App redundant back-ref

Stage 1 added Channel.@this.Channels (parent collection back-ref).
That made Channel.@this.App redundant — App is reachable via
Channels.Actor.App. Two back-refs to a parent and grandparent is the
god-bag pattern; OBP says single navigation point.

Drops the App property on Channel.@this (line 75). Drops the
channel.App = _app; setter line in Channels.Register (line 90).
Updates the one reader (MatchingBindings line 194) to navigate via
Channels.Actor.App instead.

Same destination, longer path. Channel's surface is now honest about
its real dependency: Channels (the parent it actually navigates).
```
