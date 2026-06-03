# Plan — A snapshot is the App, serialized

> **Coder/test-designer: you own the final shape.** Every code path, file location, and signature below is a *suggestion* to convey intent. If the real code wants a different shape, take it — and tell me what changed and why. What's *settled* is the model (snapshot dissolves into the App), the four decisions, the two-consumer finding, and the leaf dispositions. The literal C# and the file names are not.

> **Base:** the `type-kind-strict` branch carries a working, C#-proven snapshot implementation (serialize → disk → restore → resume, including mid-stack edit-then-resume-and-unwind). It stays as the proven substrate until this lands. This plan resolves the open questions in `Documentation/v0.2/app-as-snapshot-proposal.md` — read that for the originating realization; read this for what we build.

## Why

`snapshot.@this` is a parallel tree whose sections are named `Variables`, `CallStack`, `Errors`, `Providers`, `Statics`, `Build`, `Testing` — exactly the App's restorable subsystems, copied into a second tree. It's the Noun+Verb smell one level up: "snapshot" is a noun standing in for "the App, serialized," and `snapshot.resume` is the verb for "run it." Remove the noun and the whole subsystem collapses into App-level verbs the developer already knows:

```
- write %!app% to file 'state.json'    / serialize the App's current state
- read 'state.json' into %app%          / a new App value, detached
- set %app.variable.x% = 2              / its variables, current-actor-implicit
- run %app%, write to %result%          / resume it; the result comes back to me
```

The load-bearing use case under all this is durable execution: capture the exact state at a throw, persist it, re-enter the failing step deterministically with no live LLM. That has to stay cheap on the hot path — most errors are handled and discarded, and we must not pay an App serialization for each one.

The word "snapshot" doesn't vanish — a file holding a frozen App *is* a snapshot. What vanishes is the **type** `snapshot.@this` and the **verb** `snapshot.resume`. Snapshot stops being a thing and becomes a *state the App can be in* (serialized-to-disk).

## Decisions (settled)

1. **The App is the wire-citizen.** It owns its serialized shape via a leaf-serializer, composing its subsystem slices. The slices that serialize are exactly the ones implementing the slice contract today (Variables, CallStack, Errors, Providers, Statics, Build, Testing); the reconstruct-on-build subsystems (Goals, Modules, Channels, Cache, Events, Settings, Navigators, Types, Config) stay invisible. This list is already what `App.Snapshot()` dispatches — it transfers unchanged.

2. **`read … into %app%` builds a new, detached App; `run %app%` resumes it and returns to the caller.** A loaded `%app%` is a full App value with no channels of its own and `Parent` set to the caller. It re-anchors to the same code root so goal hashes match on restore. `run %app%` resumes from the position the value carries and hands its result back to the initiator (`write to %result%`) — goal-call semantics, one level up. Ambient (channels, filesystem) inherits through `Parent`: **channel lookup walks `Parent` before falling back to no-op**, which is what makes inheritance real and removes the need for the App to auto-wire its own console.

3. **Position rides the value, not a `run … from` modifier.** `%!app%` is the App as it is *now*; `%!error.app%` is the App as it was *at the throw*. `run %app%` always means "resume from where this value is positioned" — a throw-positioned value re-enters the failure, a freshly-built state with no position runs from Start, an ask-suspended value re-enters the ask. The developer picks *what to serialize*; `run` does the one obvious thing. No `from last error`, which would breed (`from step N`, `from start`, …) and paper over an inconsistent value.

4. **The error pins its location cheaply at throw; the App serializes only on write.** Every error already carries `Step`/`Goal`/`CallFrames` — its executable position, a few refs and ints, recorded at construction. Handled-and-discarded errors pay nothing beyond that. When `%!error.app%` is actually written, *then* the App serializes — and it builds the CallStack section from the error's **frozen `CallFrames`**, not the live stack. Throw-time variables reconstruct from the diff stream still live in the error scope. Pay-per-error stays a location; serialization is pay-on-write.

5. **Exposure splits state vs capability.** *State* — `%app.variable.x%`, `%app.statics.x%` — is freely read and settable through **curated navigable mounts** (not raw reflection over the App graph). *Capability* — which LLM/cache/db/filesystem provider, the auth root, the channels, the settings store — moves only through `use 'x.dll'` and config, never a reflective write. The protection for an unsophisticated developer running borrowed code isn't "you can't swap the filesystem," it's "swapping it takes a `use` line, which is one readable statement the permission system can gate." A buried `%app...%` write would bypass exactly where the permission check lives. (`Providers` rides the wire so a resumed App uses the same LLM — but it's a capability, so it's swap-only, never `set`. The state/capability line is already real inside one serialized App.)

6. **`%app.variable.x%` resolves through the current actor.** The App knows which actor is current, so `.variable` maps to that actor's variable collection — `app.user.variable.x` when User is current, with the actor implicit. Read *and* set, routing to the collection's existing `Get`/`Set` so events and `Data` minting are preserved. A loaded App value carries *which actor was current* when captured, so `%loadedApp.variable.x%` resolves against the same actor it was frozen in. On the live App, `%x%` and `%app.variable.x%` are the same thing — the `app.`-prefixed form earns its keep when you hold an App *value*.

## The two-consumer finding (read before deleting anything)

There are **two** resume consumers today, both riding `snapshot.@this` + `Data.Snapshot`. Deleting the snapshot type breaks the second one unless it moves too:

- **Error-resume (durable execution).** `%!error.callback%` → `App.Snapshot()` → serialize → disk → `resume %snap%` / `App.ResumeFromWire`.
- **Ask-suspend (interactive).** `output.ask` with no answer returns Data with `Snapshot = action.Snapshot()` attached; `ShouldExit()` short-circuits the step loop; the channel serializes the snapshot and sends it out; the reply path calls `Data.Snapshot.Resume(context)` via `run %callback%`.

Both must land on the same App-value model. Ask-suspend becomes: the App freezes at the ask step (its position is the ask), the channel serializes that App value, and the reply resumes it with the answer bound — `run %app%`, same verb as everything else. The `Data.Snapshot` side-channel then has no remaining consumer and goes.

## Leaf-trace — incumbents and disposition

| Incumbent | Where | Disposition |
|---|---|---|
| `snapshot.@this` tree (write accumulator) | `app/snapshot/this.cs`, `this.Wire.cs` | **Delete.** Write side renders slices straight to the App writer; no intermediate tree. |
| `Io` (read-side JSON cursor) | `app/snapshot/Io.cs` | **Delete.** Read goes through the channel's reader into a generic node; each slice rebuilds from its node. This is the read-side de-JSON-ification (the "one asymmetry" in `snapshot-system.md`) — forced now, not deferred. |
| slice serializer (leaf) | `app/snapshot/serializer/Default.cs` | **Move** → App's leaf-serializer (`<owner>/serializer/`, like `error/serializer`, `channel/serializer`). App owns its wire shape. |
| `snapshot.@this.Resume` | `app/snapshot/this.Resume.cs` | **Move onto App** — the App resumes itself (behavior on the element). The CallStack-chain re-entry (`RunFrom`, unwind to entry goal) transfers verbatim. |
| slice contract `ISnapshot` (Capture/Restore/Read) | `app/snapshot/ISnapshot.cs` | **Reshape + rehome** (e.g. `app/serialize/ISlice.cs`). The *role* — each subsystem owns its slice — is the load-bearing transfer. The *signature* changes: Capture renders to the App writer; Restore reads from a reader-node, not `Io`. |
| the 7 slice implementers | `variable/list`, `callstack`(+`call`), `error/list`(+`trail`), `module/code`, `Statics`, `module/builder`, `tester` `this.Snapshot.cs` | **Keep.** These are the App's serializable slices. Capture/Restore logic transfers; only the contract surface they implement changes. |
| `variable/list/this.SnapshotAt.cs` (throw-time view) | reverse-applies post-throw diffs | **Keep** — it's how `%!error.app%` gets throw-time variables. |
| `App.Snapshot()` / `App.Restore()` | `app/this.Snapshot.cs` | **Becomes** `App.Serialize()` + construct-from-wire. Same per-section dispatch list. |
| `App.SnapshotToWire / FromWire / ResumeFromWire` | `app/this.SnapshotWire.cs` | **Fold** into the wire-citizen (write/read) and `run %app%` (resume). |
| `resume %snap%` verb | `app/module/snapshot/resume.cs` | **Delete** → folded into `run %app%`. |
| `run %callback%` verb | `app/module/callback/run.cs` | **Becomes `run %app%`** — dispatch on the value: an App resumes itself; a goal is called. The branch lives on the value, not as an `if (value is App)` in the handler. |
| `Data.Snapshot` side-channel (+ copy-ctor line at `data/this.cs:1186`) | `app/data/this.Snapshot.cs` | **Delete.** Both consumers move to the App value; nothing sets or reads it after. |
| `Error.Callback` (`%!error.callback%`) | `app/error/Error.cs` | **Rename** → `%!error.app%`. Build the CallStack section from the error's frozen `CallFrames`; materialize (serialize) lazily on write. |
| `action.Snapshot()` (ask-suspend producer) | `channel/message/this.cs`, `module/output/ask.cs` | **Re-home** to the App-value model: ask suspends → App frozen at the ask step → serialized + resumed as an App value. |

## Build order

Not stages, not separate files — one ordered spine. Each step is independently shippable and leaves the tree building.

1. **App as wire-citizen.** Give the App a leaf-serializer (move the slice serializer here) and a construct-from-wire path. Slices render to the writer on capture and rebuild from a reader-node on restore — replacing the `snapshot.@this` accumulator and `Io`. The slice contract reshapes here; the 7 implementers follow it. This is the foundation and the largest single piece; the read-side reader-node abstraction is the sharp edge (own it carefully — it's the half of the Io rework that was deferred).

2. **`%!error.app%` — location-pinned, pay-on-write.** Rename `Error.Callback`; build its CallStack section from the error's frozen `CallFrames` instead of the live stack; serialize only when written. Removes the read-timing inconsistency (variables-say-throw / frames-say-handler) and the per-error cost.

3. **`read … into %app%` → detached App.** Deserialize to a full App value: no channels of its own, `Parent` = caller, re-anchored to the same code root. Make **channel lookup walk `Parent`** before the no-op fallback (this is what inheritance hangs on, and it's what lets the App stop auto-wiring its own console).

4. **`run %app%`.** Dispatch on value type; the App resumes from its carried position (the moved `Resume`) and returns its result to the caller. Re-home **ask-suspend** here in the same step: a suspended ask is an App frozen at the ask step; the reply resumes it with the answer bound. After this, `resume %snap%` and `run %callback%` have no callers.

5. **`%app.variable.x%` read + set.** A curated navigable mount on the App value: `.variable` → current-actor `Variable`, `.x` → `Get`/`Set` (events + minting preserved); `.statics` likewise. No raw reflection; capabilities not exposed. This is the developer-facing payoff and is independent enough to land last.

6. **Delete the incumbents.** `app/snapshot/` (tree, `Io`, serializer, `Resume`, `ISnapshot`'s old home), `app/this.SnapshotWire.cs`, `app/module/snapshot/`, `app/module/callback/run.cs`, `app/data/this.Snapshot.cs` (+ the copy-ctor line). Only after 1–5 prove out against the `type-kind-strict` reference behavior.

## Flow tree — how it will look

**A. Error → save → resume (durable execution)**

```
goal runs
└─ step N throws ──► Errors.Push(error)
                       error pins location: Step / Goal / CallFrames   ← cheap, EVERY error
                       diff-stream on (throw-time variable view)        ← scoped to the error
   └─ error handler runs
      └─ write %!error.app% to file 'crash.json'
            └─ %!error.app% materializes  ← serialize NOW (pay-on-write)
                 App.Serialize ─┬─ Variables  ← SnapshotAt(error)   (throw-time, reverse-applied)
                                ├─ CallStack   ← error.CallFrames    (throw-time, FROZEN — not live stack)
                                └─ Errors · Providers · Statics · Build · Testing
            └─ path.WriteText(json)
   └─ handler returns ──► error discarded, diff-stream off
                          (an error that is NEVER written paid only the pinned location)

─── later / fresh process ───
read 'crash.json' into %app%          ← construct DETACHED App (Parent = caller, no own channels)
run %app%, write to %result%          ← App resumes from carried position:
                                          re-enter step N via RunFrom, unwind to entry goal
                                          result ──► %result% in the caller
```

**B. Ask → suspend → resume (interactive)**

```
- ask "name?", write to %name%
   └─ output.ask, no Answer ──► App freezes at the ask step (position = the ask)
        └─ ShouldExit() → step loop short-circuits
        └─ channel serializes the App value, sends it out
─── user replies ───
run %app%   (answer bound)            ← App resumes at the ask step, %name% = answer, continues
```

**C. Load a state, edit it, run it**

```
read 'appState.json' into %app%       ← detached App value
set %app.variable.x% = 2              ← curated mount → current-actor Variable.Set("x", 2)  (events fire)
run %app%, write to %result%          ← runs (from Start if no carried position),
                                         inherits caller's channels/fs via Parent
... %result% usable in the initiator
```

## Code tree — the end result

`★` new or moved · `=` kept (role unchanged) · `✗` deleted

```
PLang/app/
├─ this.cs                          App root (Parent, CurrentActor, the slice properties)
├─ this.Serialize.cs            ★   App is the wire-citizen: Serialize (the dispatch list) +
│                                     construct-from-wire. (the role of today's this.Snapshot.cs)
├─ this.Resume.cs              ★    App resumes itself — CallStack-chain re-entry + unwind
│                                     (moved from snapshot/this.Resume.cs)
├─ serializer/
│   └─ Default.cs              ★    App's leaf-serializer (moved from snapshot/serializer/)
│
├─ serialize/
│   └─ ISlice.cs               ★    the slice contract: Capture(writer) / Restore(node, ctx)
│                                     (reshaped + rehomed from snapshot/ISnapshot.cs)
│
├─ variable/list/
│   ├─ this.Snapshot.cs        =    variables slice
│   ├─ this.SnapshotAt.cs      =    throw-time variable view (feeds %!error.app%)
│   └─ navigator/ + set-hook   ★    %app.variable.x% read+set → current-actor Get/Set
├─ callstack/this.Snapshot.cs  =    + callstack/call/this.Snapshot.cs   (frame positions)
├─ error/
│   ├─ list/this.Snapshot.cs   =    + error/trail/this.Snapshot.cs
│   └─ Error.cs                ★    %!error.app% (was .Callback): frozen CallFrames, pay-on-write
├─ module/code/this.Snapshot.cs =   Providers slice (capability — swap-only)
├─ Statics/this.Snapshot.cs    =    app key-value slice
├─ module/builder/this.Snapshot.cs = mode bit
├─ tester/this.Snapshot.cs     =    mode bit
│
├─ module/
│   └─ run (or call) ★              run %app% dispatches on value: App resumes, goal calls
│
├─ channel/list/this.cs        ★    named-channel lookup walks Parent before no-op
│
├─ snapshot/                   ✗    this.cs · this.Wire.cs · Io.cs · this.Resume.cs ·
│                                     serializer/Default.cs · ISnapshot.cs   (whole folder)
├─ this.Snapshot.cs            ✗    → this.Serialize.cs
├─ this.SnapshotWire.cs        ✗    folded into wire-citizen + run
├─ module/snapshot/resume.cs   ✗    → run %app%
├─ module/callback/run.cs      ✗    → run %app%
└─ data/this.Snapshot.cs       ✗    side-channel gone (+ copy-ctor line at data/this.cs)
```

## Sharp edges to own

- **The read-side reader-node.** Write is already format-agnostic (IWriter). Read is still JSON-coupled through `Io`. Killing `Io` means each slice's Restore reads from a generic node the channel reader produces. That abstraction is the riskiest single piece — get it right in step 1 before the slices depend on it.
- **Detached-App re-anchoring.** A loaded App must resolve goals against the same code so frame hashes match (`CallbackGoalHashMismatch` / `CallbackGoalNotFound` already enforce this). Decide what the wire carries to re-anchor — app id, root path, version — and how the detached App reaches the live goal registry through `Parent`.
- **Which actor is current in a loaded App.** Decision 6 needs the captured current-actor identity to ride the wire, or `%loadedApp.variable.x%` resolves against the wrong actor.
- **`run %app%` returning a value.** Resume re-enters mid-stack and unwinds; the value handed back to `write to %result%` is the entry goal's result. Confirm that's the intended contract for both the recovery flow and the plain "run a built state" flow.
