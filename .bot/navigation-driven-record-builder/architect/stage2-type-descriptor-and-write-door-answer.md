# Answer — type descriptor: (A) now, Write-core/Output-face; write door: no container `Write`, ever; the FromWire convention DIES

Answers [`coder/stage2-type-descriptor-off-stj-and-read-collapse.md`](../coder/stage2-type-descriptor-off-stj-and-read-collapse.md) and closes [`coder/stage2-write-door-unification-question.md`](../coder/stage2-write-door-unification-question.md). Settled with Ingi 2026-07-13.

**You own this.** Bodies and mechanics yours. Cited `file:line` verified against HEAD (c74cc60a0).

## 1. Type descriptor — (A) now; (B) stays the separate logged piece

Confirmed: kill the 14th, contained. Your two placement questions:

- **Sync emit: the type entity gets the same split every item has** — `Write(IWriter)` as the sync core (the 4-field emit that today lives in the async `Output`, `type/this.cs:38-49`), `Output` becomes the async face wrapping it. The precedent is `item/this.cs:421-427` (base `Output` = `Write(writer)` wrapped) and ruling 8's `Parse`/`Load` — sync body, async face, one body. Then `BeginRecord` is one line: `record.Type.Write(this)`. NOT inline-in-BeginRecord — that re-implements the type's render inside the writer, the exact smell being deleted.
- **Read parse: `type/serializer/Reader.cs`** — its stated home; it's the registered `ITypeReader`, the reader registry is the pattern. (Do NOT make it a static `FromWire`-style parser — see §3: that convention is dead.)

Then strip `type/this.json.cs` + the `type/this.cs:32` attribute. Acceptance as you proposed: byte-identical descriptor round-trip pinned, zero `[JsonConverter]` under `type/`.

## 2. Write door — no container `Write`, EVER; unification happens by moving callers to `Output`

The wire fact you asked for: **containers cross the wire through `Output`** — `data/this.Output.cs:112` → `dict.Output` (`dict/this.cs:177-192`, per-entry `entry.Output`, view-aware, async). Never through `writer.Value(dict)`. So your "clean, contained refactor" branch applies — but not your sketch:

- `Write(IWriter)` is not "the leaf's door" — it's the **sync core of the one door**: base `item.Output` IS `Write(writer)` wrapped (`item/this.cs:421-427`). Leaves have a sync core; containers CAN'T — their entries hold `%var%` refs and unread sources that resolve async (`data/this.Output.cs:50-67`). The base `Write` throw for containers is honest, not a gap.
- A sync `dict.Write` doing `Value(entry.Peek())` would mint a SECOND container render — Peek-based (no `%var%` resolution), viewless (no Store/Out), envelope-blind — agreeing with `dict.Output` today and drifting tomorrow. Same-thing-two-ways; rejected.

The smell Ingi named (writers asking `IsLeaf` / type-switching on containers) is real, and it dies from the CALLER side: an item in hand is rendered by asking `item.Output`, never by handing the boxed item to `writer.Value`. `writer.Value(object)` narrows back to its stated contract — raw normalized CLR leaves, post-lower. The remaining `Value(item)` feeders (the `EndRecord` properties sidecar `json/writer.cs:110`, any preview drives surviving the template migration) move to `Output` as they're touched; `json.Writer`'s `case dict`/`case list`/`case item` arms and your `text.Writer` leaf arm die when that inventory empties — a natural fold-in for piece (B), not its own change now.

**Your `IsLeaf` leaf arm + `TextWriterItemArmTests`: keep green, but mark it a tombstone** — correct behavior for a door that's closing. One comment line so nobody reads it as a pattern; nothing new builds on `Value(item)`.

## 3. The FromWire convention DIES (Ingi ruling: everything reads ONE way — the reader registry)

There is no "types that lack a reader" category. A type that reads from the wire owns `serializer/Reader.cs`. The inventory (verified):

| member | state | disposition |
|---|---|---|
| `type.@this.WireReader` (`type/this.cs:457-463`) | ZERO callers since `TryConvert` died | delete |
| `hash.FromWire` (`crypto/type/hash/this.cs:80`) | already re-housed — `crypto/type/hash/serializer/Default.cs:21-28` is its registered reader, delegating | fold the body into the reader (or private mechanics); the public convention name dies |
| `signature.FromWire` (`signature/this.Wire.cs`) | schema reader went token-based (`data/schema/signature.cs:25` — "no FromWire/DOM") | verify liveness; delete if dead (expected) |
| `snapshot.@this.FromWire` | does not exist; `module/snapshot/resume.cs:10` comment is stale (the real entry is `App.SnapshotFromWire` over the plang serializer — a different thing) | fix the stale comment |

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| type entity `Write` core + `Output` face | one body, two faces — the exact item/`Parse`-`Load` pattern; `BeginRecord` stops re-implementing the type's render | ✓ |
| descriptor read in the registered reader | one read mechanism (the registry); no parallel static parser | ✓ |
| no container `Write` | one container render (`Output`); no Peek-based viewless twin to drift | ✓ |
| writers' item/container arms as tombstones | callers migrate to the value's door; the arms die with (B); nothing external asks `IsLeaf` | ✓ |
| FromWire convention deleted | one way to read from wire; a discovery seam with zero callers does not survive on sentiment | ✓ |
