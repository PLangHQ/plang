# event-action — security review (6703697fb..d955b8f5f, stage 8 first)

Verdict: **FAIL** — two High, one Medium open. Patterns: 12 (one answer: "is this goal plang's own?" asked by name, not by owner), 11 (lazy: the load order of a sub app's own system events versus its maker's).

Probes (tests only, branch `security-event-action`): `PlatformStoryProbeTests` (stage 8, items 1–2), `SubOwnEventsProbeTests` (items 1, 5, 6 and the line), `SubNoneSnapshotProbeTests` (3), `ActorReachProbeTests` (4). Earlier snapshot sets re-run on the way in: all green.

## What holds (stage 8)
- A–D: a platform's Meter calls its own Record, whatever the upload offers: its `system/` copy, a root Record, a mirrored `system/platform/` folder, a `/system/…` name, a nested folder (platform's list has not read it). Each answers `platform`.
- `../Record` is refused (`GoalNameClimbs`); a wildcard name is a pattern and never a run (`NotAGoal`); `../*` never reaches a pattern (the `..` check comes first).
- A call by variable (`call %target%`) names only what the maker's list finds (H1).
- Item 3: a `Sub=none` event is not in a sub app (1 event vs 2 with `inherit`); the maker's snapshot does not resume in the sub app (`SnapshotAppMismatch`).
- Item 4: `%!app.actor.system.event%`, `.variable`, `.context`, `.setting`, `.identity` and the same through `%!app.actor.list[n]…%` all answer nothing; `%!app.variable["secret"]%` from the user is not the system's value.

## Findings

### F1 — High — pattern 12 — a sub app's goals under its own `system/` folder are exempt from its maker's "each goal" events
`event.setting.goal: app` skips every general event (`IsMany`) inside a goal where `goal.IsSystem` (`Under("system")`, `goal/this.cs:184`; `event/list/this.cs:218`, `side.cs:53–58`). A sub app's own `system/` folder is the upload's code, not plang's, yet it is `IsSystem`. Probe `SubOwnEventsProbeTests.E`: the platform binds `before each goal, call Check` (Check fails); the upload's `/Work` is stopped (`Denied`), its `/system/Work` runs (`workedUnderSystem=yes`). An upload puts everything under `system/` and the maker's per-goal policy never runs. Also lets the upload bind events with no policy in the way (F3).
Fix (to the architect, not a coder task: it touches what `IsSystem` means for the skip): skip only the os's goals and the running app's own system copies where the app is not a sub app — i.e. ask the goal "is this plang's own for the event's actor", with `goal.App is null` (os) and, for an app's copy, only when the event's actor is that app's root. An inherited event never skips for a sub app's goal.

### F2 — High — pattern 11 — a sub app's own `system/event/` files run before its maker's events join
`event/list/this.cs:170–173`: `Load` runs the files (`Run`) and only then joins the inherited events. The sub's earliest events are first in bind order, so its `on error` event is offered the maker's check failure first and takes it. Probe `B2`: the upload's `system/event/Own.goal` binds `on error` each action; the maker's failing Check is swallowed and `/Work` runs (`worked=yes`). This reverses the snapshot-251 condition ("the maker's bindings fire first, then the child's"), and `start.md:46` says the maker's come first, the code the other way.
Fix: a sub app joins its maker's events first, then runs its own files. (F1 must close with it: ordering alone leaves B3.)

### F3 — Medium — a sub app's own `on error` event is offered a failure raised inside its maker's handler
Probe `B3`: after the maker's events are in (the ordering F2 asks for), an upload goal under `system/` (F1's gap) binds `on error` each action; the maker's Check fails inside an action (`error.throw`) and the sub's event takes it. `Catch` offers the failure to every event of the actor in order (`before.cs:51–64`), including the sub's own, though the action that failed is the maker's. With F1 closed this needs a goal the check lets through, so Medium: a policy that denies only some goals is cleared by an event an allowed goal bound.
Fix: a failure raised while an inherited event is running (`context.call.Event` is an inherited one) is not offered to the actor's own events; or an `on error` event bound by a sub app does not apply inside a call frame that belongs to an inherited event. The same shape for `after` (cannot clear a failure today, D) and `before` (a sub's Handled cannot let a goal through, C).

### F4 — Low — a bare name falls through to any goal the app's list has read, anywhere
`goal/list/this.cs:239`: after the caller's chain and its own folder, `Goal(name, goals)` picks the newest read goal of that name in any folder. If the platform's own list has read a goal of the upload (the upload's folder lies under the platform's root, so a listing or a build over the root reads it), a bare `call Record` written in a folder with no Record beside it runs the upload's. Probe `PlatformStoryProbeTests.E` (red). Pre-existing, but it is the platform story. Fix: a bare name's read-anywhere fallback leaves out goals under another app's root, or Meter calls `/Record`.

### Info
- H2: a maker's `call %target%` with a value the upload set can name a goal of the upload's folder when it lies under the maker's root. A maker's unchecked call by variable; no finding.
- `%!app.actor.list[0].channel%` reaches another actor's channel list (pre-existing; not this branch).
- Item 5, what a system/event file can do as System that the upload could not otherwise: nothing in power (a goal call reaches `Actor=system` with no check, `goal/call.cs:28`, `actor/list/this.cs:36–43`), but its order (F2) and its exemption (F1) are the gap.
- Item 6: `api/*` is rooted at its binding goal's folder; `../*` is refused; a pattern matches by app-relative address, so a sub app's `/*` pattern also matches a maker's handler goals running in its actor (same cause as F1/F3).
