# snapshot

## Why

Two needs meet in the snapshot. **Durable execution:** when an error happens, the run can be saved and resumed later at goal X, step Y, in a fresh process. **Fast apps:** a web request, a test, or PlangOS starting an app needs a new app quickly, and today every `new app.@this(...)` builds the whole world again (a full reflection scan for the type registry and for the module catalog, every `.pr` read again; `app/this.cs:281–361`). Children should start from a frozen parent, change what they need, and be reset when they're dirty.

Today's snapshot serves only the first need, and its shape is wrong: the app keeps a fixed list of who takes part (`app/this.Snapshot.cs:7–9`), each owner has a second serialization door beside the wire it already has, and the restore path is split across the call stack, the snapshot and the app (the OBP audit below, 25 findings). This plan makes every item serialize itself through its own wire, lets each item say whether it takes part, and adds the second kind of snapshot: the app's.

## Decisions (Ingi, 2026-10-02)

- **One door:** an item serializes itself through the wire it already has (`Output(writer, View)`, read back by its type's reader). A snapshot is one more view, as `[Store]` and `[Out]` are; an item marks the members that belong in it. `ISnapshot`'s `Capture`/`Restore` go.
- **Each item decides:** the snapshot walks the app tree, and every item answers with its own snapshot view. No list on the app.
- **Child apps share the parent's frozen parts in memory**, and copy a part only when they change it. "Dirty" is a child holding a copy; a reset drops its copies. Serialization is for disk: durable execution, a restart, another process.
- **Two kinds of snapshot, one type:** a **resume** snapshot (R) knows where to resume and can generate a callback; an **app** snapshot (A) is the app's world, with no callback.
- **What takes part** (the R/A table, Ingi: "both R and A look logical"):

| part | R | A |
|---|---|---|
| the context's memory (variables) | ✓ | — |
| the call chain's position (goal, step, action per frame) | ✓ | — |
| the error being handled | ✓ | — |
| settings, this run's layer | ✓ | ✓ |
| code providers added at run (`module.add` DLLs, default overrides) | ✓ | ✓ |
| types and modules added at run | — | ✓ |
| loaded goals, shortcuts | — | ✓ in memory (on disk they are already files) |
| event bindings at app scope | — | ✓ in memory |
| mode | ✓ | ✓ |
| statics (until typed) | ✓ | ✓ |
| store, cache, FileSystem, Debug, Build, test, channels, Services, KeepAlive | — | — |

## Open (to Ingi)

- **Which variables a callback carries when it leaves the app.** The 2026-05 ruling (`Documentation/Runtime2/todos.md:196–216`): a callback carries only the variables the step declares (`vars: %orderId%`), signed with the goal's hash, step and action, and encrypted, because a 1000-row list can't ride a hidden form. Today's snapshot captures every user variable. Proposed: a local durable resume (to disk, the same app) carries all the user variables; a callback handed outside the app carries only the declared ones, signed and encrypted.

## The shape (the coder owns the code; this is the intent)

- **The view.** A member that takes part is marked for the kind it takes part in (R, A, or both); writing a snapshot is writing the item through its own `Output` with that view, and reading it back is its type's reader. An item with no marked member doesn't take part, so the walk passes it. The walk goes down the app tree as navigation does (`%!app…%`), so a section's name is the item's own path, never a hand-written string.
- **The value answers whether it is captured.** An always-fresh value (`computed`, `Now`, `GUID`) and a `!` name answer no, in one place (today the rule is written twice, `variable/list/this.Snapshot.cs:25–30` and `this.SnapshotAt.cs:39`).
- **A frame captures and restores itself.** Its position (goal by name, step, action, checked against the goal's hash) is the frame's own, read back as a position that resumes itself. No second chain on the live stack (`_restoredChain`, `BottomFrame`'s fork), no static recursion (`snapshot/this.Resume.cs`).
- **The error owns its throw-time state:** the chain it carried and the variables as they stood at the throw. The diff history that projects variables back (`callstack/this.Snapshot.cs:9–89`, `:251–287`; `variable/list/this.SnapshotAt.cs`) becomes its own type, not part of the call stack's snapshot file.
- **The resume point belongs to the value that exits** (an ask, a failure), not to a settable slot on every Data (`data/this.Snapshot.cs:19`).
- **The mode restores as a value;** what a mode starts (build, the test session) is the mode's own change, not the restore's branch (`app/this.Snapshot.cs:53–61`).
- **Code providers** are named by the code kind's plang name, not `AssemblyQualifiedName`; a provider's name comes from its class, not by building one (`InstanceName`); their DLLs load through the async path verb, never `.GetAwaiter().GetResult()`.
- **Frozen parts and children.** The parts the A column marks are frozen in the parent and shared by reference; a child's first change to one copies it into the child (copy on write), so the parent and its siblings never see it. `new app.@this(parent)` (`app/this.cs:274–279`) is the door, and the test runner's per-test app (`test/list/this.cs:151`) its first user.

## Stages

0. **Measure** (coder): a C# benchmark timing each part of `new app.@this(...)`, the first type lookup, the first goal load, and a child app. The baseline for stage 3.
1. **One door** (coder): the snapshot view and its marks; the walk down the app tree; the snapshot's own wire (`Output` + its reader, no `Serialize` through goal's kind); each of today's five owners moves to marked members; `ISnapshot` and the app's list go. The existing resume tests still pass.
2. **The resume snapshot** (coder): the frame's own position and resume; the error's throw-time state; the diff history as its own type; the value's captured answer; the resume point on the exiting value; the mode as a value; code providers by kind name and async load. A run saved at an error, written to disk, and resumed in a fresh app re-enters the failing step.
3. **The app snapshot and children** (coder): frozen parts shared by reference, copy on write, dirty and reset; the test runner's child apps use it; measured against stage 0.
4. **The callback** (after Ingi's open point): the resume snapshot generating a callback, with what it carries.

## Demolition

- `PLang/app/this.SnapshotWire.cs` (whole file): `SnapshotToWire` (a middleman over `snapshot.Serialize`), `SnapshotFromWire` (the snapshot's birth from text, on the app), `ResumeFromWire`.
- `app/this.Snapshot.cs`: the list `Snapshotted` (`:7–9`), the private `Capture` (`:28–33`), the app's own `ISnapshot` (`:47–61`); `Snapshot(context)` and `Snapshot(error, context)` move to where their owners are (the snapshot's birth; the error).
- `snapshot/ISnapshot.cs` (whole file) and its five implementations: `module/code/this.Snapshot.cs`, `type/item/variable/list/this.Snapshot.cs`, `Statics/this.Snapshot.cs`, `callstack/this.Snapshot.cs`'s `Capture`/`Restore`/`Thrown`/`CaptureFrames`, the app's.
- `snapshot/this.cs`: `Context`'s `internal set` (`:22`); two of `HasSection`/`SectionNames`/`Sections` (`:55–64`), one stays; `Write<T>` taking any CLR value (`:71–73`).
- `snapshot/this.Wire.cs`: `Serialize` through goal's format kind (`:24–33`).
- `snapshot/this.Resume.cs`: `ResumeChain`'s static recursion; `callstack`'s `_restoredChain`, `RestoredChain`, `BottomFrame`.
- `callstack/this.Snapshot.cs:178–242`'s Restore with the PrPath fallback "for snapshots captured before names were carried" (no compatibility shims) and the path string trimming.
- `variable/list/this.SnapshotAt.cs`'s second skip rule.
- `module/code/this.Snapshot.cs`: `InstanceName` (`:175–182`), `.GetAwaiter().GetResult()` (`:109`), `Rows<T>` if the reader makes it unneeded.
- `module/IContext.Snapshot.cs` (whole file): the static extension class.
- `data/this.Snapshot.cs`: `Data.Snapshot { get; set; }`.
- The coder traces every caller of each (Error.cs:155, the callback module, the ask path, tests) and gives each one's disposition before building.

## Stays

- `snapshot/this.cs` as the container: a dict of plang values that writes itself, its reader (`snapshot/serializer/Reader.cs`), and its navigation (`snapshot/this.Variables.cs`: `%snap.variables.x%`, `set %snap.variables.x% = 2`).
- The referent-integrity errors (`CallbackGoalNotFound`, `CallbackGoalHashMismatch`, `CallbackActionMismatch`): a resume that can't find its goal, or finds it changed, fails hard.
- The diff history's behaviour (the throw-time projection), in its own type.
- Code providers' rule: built-ins rebuild, runtime registrations and default overrides are captured.

## How it's proven

- [start.goal](start.goal) runs the plan's tests; each lives under this folder at the path of what it tests.
- At each stage's end its tests are built and run. A stage is accepted when its tests are green, the checks hold, and the architect's OBP review is clean.
- **Every test must fail without its change.**
- The plan's plang says what is wanted; the coder rewrites each test in real plang that compiles, keeping what it proves.

## What tests can't show (checks the coder runs and reports)

- **Stage 1:** no `ISnapshot`, no hand-written section name, no snapshot list on the app (grep); the existing resume and snapshot tests pass.
- **Stage 3:** the benchmark against stage 0, for `new app.@this(parent)` and the test runner's run of every plang test.
- **Every stage:** the gate by name; PLNG001/002 at 0.

## OBP validation

| surface | plang path | C# path | file path | checks |
|---|---|---|---|---|
| a snapshot | `%snap%`, `%snap.variables.x%` | `snapshot.@this` | `PLang/app/snapshot/this.cs` | writes itself (`Output`), read by its reader; one door to sections |
| what takes part | (the item's own members) | a member mark, read by the item's `Output` with the snapshot view | the item's own file | no central list; the item decides |
| a frame's position | `%snap.callstack…%` | `callstack.call.Position` | `PLang/app/callstack/call/…` | captures and restores itself; resumes itself |
| the diff history | — | its own type | coder places it | not in the call stack's snapshot file |
| a child app | — | `new app.@this(parent)` | `PLang/app/this.cs` | frozen parts by reference, copy on write; dirty is "holds a copy" |
| the resume point | (the exiting value's member) | the exiting value | its type's file | not a slot on every Data |
