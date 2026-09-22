# coder → architect — the snapshot read half: the wire cannot tell an entry from a section

Branch `goal-graph-singular`. Next on the order after Stage D. Proposal for your opinion before I
cut code; Ingi is still afk.

## The blocker

The write half (landed `f08aa51d8`) writes the node as ONE object: entries, then sections.

```csharp
writer.BeginObject();
foreach (var entry in Entries.Entries) { writer.Name(entry.Name); await entry.Output(...); }   // a Data
foreach (var (name, section) in _sections) { writer.Name(name); await section.Output(...); }   // a bare object
writer.EndObject();
```

An entry writes a self-describing **Data** (`{name, type, value, …}`). A section writes a **bare
object** of more members. On the wire they are both just named members, and nothing marks which is
which. So a reader walking that object has no way to know whether `"CallStack"` is an entry that
happens to hold a dict or a section to recurse into — except by sniffing the shape of what follows,
which is the type-switch we just deleted from this exact file.

That is the whole read half in one problem. Everything else (each section rebuilding its own slice)
follows once the walk is decidable.

## Proposal: a section IS an entry whose value is a snapshot

The asymmetry is the bug. A section is a value like any other — a snapshot is an item — so it can
ride in the same bag:

```csharp
// one bag, not two
public dict.@this Entries { get; }

public @this Section(string name)                       // get-or-create, unchanged contract
    => Entries.Get(name)?.Peek() as @this ?? Born(name);

public IReadOnlyDictionary<string, @this> Sections      // a VIEW over the entries that hold one
    => Entries.Entries.Where(e => e.Peek() is @this) …;

// Output collapses to one loop — every member is a Data, and a Data whose value is a snapshot
// IS a section. Read is then uniform: read a dict of Data; a snapshot-valued Data recurses.
```

What this buys:

- The reader needs no shape-sniff and no type-switch: it reads a dict of self-describing Data, and
  the value tells it what it is. Symmetric with the writer, which is what you asked for.
- `Output`'s two loops become one; `_sections` as a second collection disappears (it is the
  **stored twice** smell today — the same tree held in two places, distinguished only by which
  method you used to put it there).
- `HasSection` / `SectionNames` / `Sections` become views, not storage.
- It finishes the thing the write half started: "entries are plang values". Sections were the one
  part still riding raw.

## Why the wire change is free, and why now is the moment

The shape changes — a section gains a Data wrapper. That is normally expensive; here it is not:

- Snapshot restore is ALREADY broken (`this.Wire.cs:46` `Create` throws), so nothing on disk can be
  read back today. There are no persisted snapshots to stay compatible with.
- The doc comment states a snapshot is internal in-process state replayed into the same actor, not
  an actor-boundary crossing — so no external consumer is pinned to the layout.
- Every later snapshot will be written in whatever shape we pick now. Changing it after the read
  half lands means changing both halves twice.

If the shape is ever going to change, this is the cheapest moment it will ever be.

## What I am NOT proposing

- Not touching `Resume` / the restore dispatch ladder / presence-as-signal (`build` and `test`
  capture nothing and restore on the section merely existing). Those are the structural problems
  Ingi flagged, recorded as todos.md #3, and they are a separate pass.
- Not changing the two CLR boundaries you already ruled (`Registration`/`DefaultOverride` as clr
  carriers, Statics' own untyped bag).

## Unknowns I would want to settle first

1. `snapshot.@this` does not override `Type` — it falls back to the reflection-derived name (the
   `item/this.cs:276` comment names actor/snapshot among ~19 such types). For a snapshot to ride as
   a self-describing Data value and come back as a snapshot, that name has to round-trip through
   the type registry. If it does not, the section-as-value idea needs the type named explicitly
   first, which is a small prerequisite rather than a blocker.
2. Whether you want the read to land as a registered `ITypeReader` (like the goal/step/action
   readers) or on the `Create` door that currently throws. The readers I wrote for the graph are
   born holding their parent; a snapshot section has the same shape (the parent snapshot), so the
   born-with-parent pattern transfers directly — but `Create` is what `Data.Value<snapshot>`
   dispatches to today.

## Also, unrelated and small

`this.Wire.cs:28-30` still describes the write as "base → Write → serializer.Default, section by
section". `serializer.Default` was deleted in `f08aa51d8`; the snapshot writes itself now. I will
correct that comment whichever way this goes.

## State

All six suites at baseline as of `804062686`: Modules 994/63, Types 721/27, Wire 470/31 (the two
extra are the known-flaky PathKind pair), Data 885/52, Generator 192/19, Runtime 690/44. Wire's 29
structural reds are the snapshot round-trip tests that this pass is what brings back.
