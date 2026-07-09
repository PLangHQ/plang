# The Snapshot System — file map

Durable execution: capture the exact state at a throw (or suspend), persist it,
and re-enter the failing step deterministically with no live LLM. This is the
high-level map of where everything lives — read it top to bottom to find any
piece.

```
THE SNAPSHOT SYSTEM — where everything lives

┌─ app/snapshot/ ───────────────────────────── the concept's home
│   this.cs            the tree: named Sections + typed Entries (object?)
│   ISnapshot.cs       the contract a subsystem implements: Capture / Restore / Read
│   this.Wire.cs       Serialize (→ channel) · Deserialize (envelope-tolerant) · FromWire
│   this.Resume.cs     Resume(): Restore + walk the CallStack chain, re-enter each goal
│   Io.cs              read-only cursor over a parsed JSON section (Get / GetSection)
│   serializer/
│     Default.cs       ★ the snapshot's LEAF-SERIALIZER — walks itself, emits via IWriter
│                        (format-agnostic; tags errors so they render themselves)
│
├─ app/this.Snapshot.cs        App.Snapshot()  — builds the tree (dispatch to each subsystem)
│  app/this.SnapshotWire.cs    App.SnapshotToWire / FromWire / ResumeFromWire (thin wrappers)
│
├─ each subsystem owns its slice  →  <subsystem>/this.Snapshot.cs
│     variable/list   Capture / Restore / Read   ("variables")
│     callstack       + callstack/call           (frames: position + goal hash)
│     error/list      → error/trail              (the error trail)
│     module/builder, tester                      (the mode bits)
│     module/code (Providers),  Statics
│     variable/list/this.SnapshotAt.cs           throw-time projection of variables
│
├─ app/error/serializer/Default.cs   ★ IError's LEAF-SERIALIZER ($type + content)
│  app/error/IError.Wire.cs          the READ side (polymorphic IError ← JSON)
│  app/error/Error.cs                Error.Callback (throw-time snapshot) + Restore ctor
│
├─ app/module/snapshot/resume.cs     the PLang verb:  - resume %snap%   (Data<snapshot>)
│
└─ app/data/this.Snapshot.cs         Data.Snapshot side-channel (ask-suspend; flagged for removal)
```

## How to read it — the two directions

- **Write (out):** `App.Snapshot()` → tree → `Serialize` sends it through the
  channel → the snapshot's `serializer/Default.cs` walks itself and emits via
  `IWriter` → errors tag themselves so `error/serializer/Default.cs` renders
  them. *Nobody names a format.*
- **Read (in):** `Deserialize` strips the Data envelope → each subsystem's
  `Read` (via the `Io` cursor) rebuilds its own section → `Resume` restores and
  re-enters the suspended step.

## The one asymmetry worth knowing

**Write is format-agnostic** (renderers / `IWriter`, the OBP leaf-serializer of
the "data rides sealed" rule). **Read is still JSON-coupled** (`Io` parses JSON nodes). Making read
format-agnostic too — go through the channel's reader into a generic tree, then
rebuild — is the remaining half of the `Io` rework, tracked in `todos.md`.

## OBP shape

- The snapshot owns its own serialization (its leaf-serializer), not a static
  `Write(section, …)` reaching in from outside — that earlier shape was the
  violation that this rework removed.
- A domain value that can't be rendered structurally (an `IError`, which needs a
  `$type` discriminator and must drop its live `Step`/`Goal`/`Exception`
  back-references) owns *its* shape via its own renderer; the snapshot composes
  it by tagging, never by reaching into the error's fields.
