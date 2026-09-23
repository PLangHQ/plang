# Restore remainder (todos #3) — plan

One structural pass over snapshot capture/restore. Facts from a full read of the machinery (file:line
in the map below); nothing coded yet.

## What is wrong today (measured)

1. **Split lifecycle.** `ISnapshot` (`snapshot/ISnapshot.cs:14`) is an instance `Capture(s)` plus a
   `static abstract Restore(s, context)`. The owner writes itself, then a static rebuilds somewhere else.
2. **Section names live in App, not the owner.** `App.Snapshot` writes `"Variables" "Providers" "Statics"
   "Build" "Test" "CallStack"` (`app/this.Snapshot.cs:19-24`, again for the throw-time overload `:40-45`);
   `App.Restore` reads the same six strings (`:61-66`) as a dispatch ladder
   `if (s.HasSection("X")) await X.Restore(s.Section("X"), ctx)` — a registry written as a switch, the
   strings matched by hand at both ends.
3. **Presence-as-signal.** `build.Capture` and `test.list.Capture` are `{ }` (`build/this.Snapshot.cs:10`,
   `test/list/this.Snapshot.cs:10`); the section exists only because `Section()` creates it
   (`snapshot/this.cs:49-55`). Restore creates `new Build/Test` when the section merely exists
   (`build/this.Snapshot.cs:17`, `test/list/this.Snapshot.cs:17`); absent → nothing (a live Build/Test on
   the destination is not cleared).
4. **Statics restore wipes before it looks** (`Statics/this.Snapshot.cs:30` clears, `:37` then reads) and its
   storage is `ConcurrentDictionary<string, ConcurrentDictionary<string, object?>>` (`Statics/this.cs:17`).
5. **callstack frame restore** reads with `Entries.Get(...)!` (no guard, `callstack/this.Snapshot.cs:181-186`);
   `actionModule`/`actionName` are written and never read (`call/this.Snapshot.cs:34-35`).
6. Stale doc: `resume.cs:9-11`, `this.SnapshotWire.cs:11-12`, `ISnapshot.cs:31-33`, `snapshot/this.cs:10-11`
   (`Read<T>` no longer exists; `Create` no longer throws).

## Shape

**A. The owner owns its section, both halves, as instance members.**
```csharp
interface ISnapshot
{
    string Section { get; }                              // the owner names its own section: "variables"
    void Capture(snapshot.@this s);                      // writes into its section
    Task Restore(snapshot.@this s, actor.context.@this context);   // reads its section into THIS instance
}
```
`static abstract Restore` dies. Restore is into the destination App's own live instance (the one that owns
the data), not a static that finds it.

**B. App keeps ONE list of its snapshot owners** (its snapshotted properties) and walks it both ways:
```csharp
IEnumerable<ISnapshot> Snapshotted => [Code, Variable, Statics, Mode, CallStack];   // restore order
Snapshot(ctx):  foreach (var o in Snapshotted) o.Capture(s.Section(o.Section));
Restore(s,ctx): foreach (var o in Snapshotted) if (s.HasSection(o.Section)) await o.Restore(s.Section(o.Section), ctx);
```
No section string in App; adding an owner is adding it to the list. Restore order stays Providers-first.
The throw-time capture keeps its two differences (variables at the error, frames from the error) as the
two owners' overloads, called by the throw-time Snapshot.

**C. Presence-as-signal → a value.** Build/Test are nullable App properties created when the app builds or
tests; the fact "this app was building / testing" is the APP's, not Build's. A small owner — the App's mode —
captures `{build: bool, test: bool}` and restores by setting/clearing `App.Build` / `App.Test` from the values.
An absent value is "not captured" (leave as is); `false` clears. Build/Test stop implementing ISnapshot.

**D. Statics** reads before it clears, and restores only when its section has its `bags` entry. Its CLR
storage stays (Statics' own backlog, scoped out as before).

**E. callstack** reads frames through typed doors with a decline per missing key (named error, not `!`);
the unread `actionModule`/`actionName` either become the resolve key (they name the action the frame was
in) or stop being written — **Q3**.

## The 8 tests — what each needs

| test | stops at | after this pass |
|---|---|---|
| `Testing_RoundTrip_PreservesIsEnabled` | `:47` — `TestApp.Create` already sets `app.Test` | fixture uses an app without Test (or asserts restore CLEARS/SETS from the value) → green with C |
| `App_Snapshot_WalksISnapshottedProperties_AndAggregatesIntoTree` | `:15` asserts an `"Errors"` section never captured | stale assertion (no Errors owner exists) — update to the owner list |
| `SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess` | `snapshot.Create(text(json))` → null (`Create` is pass-through `this.Wire.cs:44`) | the test uses the retired door; read through the type's reader (`App.SnapshotFromWire` / `Type["snapshot"]`) — test fix |
| `MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal` | untraced (untyped Data read-back, then Rows edit, Resume) | trace after A–E |
| `PlangPath_AsSnapshotConvert_EditSurvivesResume` | `snap.variables.x` set → `ReplayDisabled` | **Q1** |
| `ThrowTimeSnapshot_EditSurvivesResume` | same | **Q1** |
| `TypedSnapshotString_NavigateEditResume_PersistsEdit` | same | **Q1** |
| `NavigateAndEditCapturedVariable_ThenResumeToSuccess` | `snap.variables.x` get → `ReplayDisabled` | **Q1** |

## Questions

**Q1 — edit-and-resume.** Four tests edit a captured variable through navigation (`%snap.variables.x%`).
Those doors were disabled on purpose (2026-07-09): they embedded variable knowledge in the snapshot. With a
section being a plain `dict` entry (`Entries`), navigation can come back WITHOUT that knowledge — the snapshot
navigates its own entries generically (`snap.Variables.variables.x` is dict navigation), and variable.list's
Restore reads whatever the dict holds. Bring it back that way (the tests' path spellings change to the
section/key shape), or keep it disabled and retire the four tests?

**Q2 — the mode owner (C).** An owner for "the app was building/testing" as `{build, test}` values: its own
small type (`app.mode`?) on App, or two bool entries App writes into its own section? Name is yours.

**Q3 — callstack's unread keys.** `actionModule` / `actionName` are captured and never read; restore resolves
the frame by goal + indices. Stop writing them, or make restore verify them (the action at that index is
still that action — a stronger integrity check than the goal hash alone)?

## Order

1. A + B (interface + owner list; section names move to owners) — pure restructure, same wire.
2. C (mode values; Build/Test out of ISnapshot) + `Testing_RoundTrip` fixture.
3. D + E (Statics order, callstack guarded reads).
4. Stale docs (item 6); the `Errors` and `Create(text)` test fixes.
5. Q1 per ruling; then trace `MidStackChain`.
Each its own commit; the six suites by name after each.
