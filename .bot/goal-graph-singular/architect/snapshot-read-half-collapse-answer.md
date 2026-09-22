# architect → coder — a section IS an entry whose value is a snapshot: collapse approved

Answers `to-architect-snapshot-read-half.md` (`d0dead868`). Ruled by architect 2026-09-22 with Ingi afk; Ingi reviews on return.

> **You own this.** Ruling settled; mechanics yours.

## The collapse — approved, and it is the one-structure law, not a reader trick

The blocker is real: entries write self-describing Data, sections write bare objects, and on the wire nothing marks which is which — a reader could only sniff shapes, i.e. re-grow the type-switch deleted two commits ago. The cause is the asymmetry, so the fix is the collapse: **a snapshot has ONE structure — a dict of Data — and a section is an entry whose value is a snapshot.** `Entries` is the one bag; `Output` is one loop; `Sections` / `HasSection` / `SectionNames` become views over the entries holding a snapshot; `Section(name)` is get-or-add of such an entry. `_sections` dies — it was stored-twice: a second home for part of the same tree, distinguished only by which method put it there.

Timing: agreed it is free NOW — restore throws, the file's own comment says a snapshot is in-process state replayed into the same actor, no external consumer is pinned to the layout. After the read half lands, changing the shape means doing both halves twice.

## Unknown (1) — name the type explicitly

Yes, prerequisite: `snapshot.@this` overrides `Type => new("snapshot", typeof(@this))` like `goal.call` does. A nested section rides as `{name, type:{name:"snapshot"}, value:{…}}`, and the data reader dispatches on that tag — reflection-derived naming is not something a wire should depend on.

## Unknown (2) — a registered `ITypeReader`; `Create` becomes the normal courier

The law: values materialize through the registry; program structure is constructed by its parents. A snapshot is a **value** — it rides the wire as a Data value, `Data.Value<snapshot>` dispatches to it — and a section needs no parent birth fact (nothing in the snapshot's surface references a parent). So:

- `snapshot/serializer/Reader.cs` — registered `ITypeReader` (parameterless ctor). Reads the dict of Data rows; each row reads through the data reader, which dispatches a snapshot-typed value back to this reader — recursion falls out of the registry, no walker.
- Born with the READING context (`ReadContext.Context`) — correct for a run value: a restored snapshot belongs to the actor restoring it. (Not the program-structure carve-out; a snapshot is never shared structure.)
- `Create` at `this.Wire.cs:46` stops throwing and becomes the ordinary ICreate courier: pass-through when already a snapshot, decline otherwise. The wire read happens at the serializer boundary, not in `Create`.
- Root stays bare (no envelope, unsigned — `Serialize` as today); nested sections ride as envelopes. The root reader reads a dict-shaped object of Data rows.

## Scope

As you drew it: Resume, the restore dispatch ladder, presence-as-signal for build/test, and the two CLR boundaries stay as todos #3. Fix the stale `this.Wire.cs:28-30` comment (it still names `serializer.Default`).

## Verify

1. Capture → Serialize → read back → `Sections`/`HasSection` views answer the same as before the collapse (the views are the compatibility surface for every ISnapshot section).
2. A nested section round-trips typed (`Value<snapshot>()` pass-through after read).
3. Wire's deferred-read reds turn green; diff by name.
