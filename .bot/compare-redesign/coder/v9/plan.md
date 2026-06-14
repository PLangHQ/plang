# v9 — Stage 11: lazy read + lazy containers

Implements `architect/stage-11-lazy-read-and-containers.md`. Store raw, type on read.

## Part A — Wire value-slot read (PLang/app/data/Wire.cs)
- Replace the value case: decode the value slot **once** into a **plain CLR value**
  (number → long/double, string → string, bool → bool, object → `Dictionary<string,object?>`,
  array → `List<object?>`) — NOT a JsonElement DOM — and hand it to the existing
  `type.Deserialize` / `Read(object raw, kind, ctx)` reader.
- Demolish: `LiftDataIfShaped`, `HasDataMarker`, `LiftArrayElements`, `IsDeferrableShape`,
  `deferredRaw` + `GetRawText` re-stringify, `_readDepth`/`MaxReadDepth`, the three-branch
  value switch. Re-judge the two `SetValueDirect` courier branches against single-layer.
- Keep type-before-value invariant; reject value-first loud.

## Part B — lazy containers
- `list._items: List<Data>` → `List<object?>` (raw-or-Data). One Data over the whole list.
  Normalize-on-read + cache-back. Untouched → verbatim. `add`/`set` drop value in as-is.
- `dict._entries: List<Data>` + `_index` → `Dictionary<key, object?>`; key moves off `Data.Name`.
- Re-express every op (At/Insert/RemoveAt/Items/SortByValue/etc.) to normalize-on-read.

## Order
A first (build+targeted tests), then B. Both green before commit. `dev.sh` for build/test.

## Non-negotiables (architect)
store raw / type on read; one Data per container (never per element at rest); untouched
verbatim; keep `Read(object raw)`, do NOT build IReader.

## Finding before implementing (2026-06-14)

**Do NOT attempt this piecemeal — it must be Part A + Part B together.** Tried the
smallest isolated slice first: in `Wire.ReadBody`, decode only the *scalar* value
token to plain CLR (`Number→long/double`, `String→string`, `bool`) instead of
`Deserialize<object?>`→`JsonElement`, leaving containers and the ctor/Judge path
untouched. Build was green, but it **regressed** `Variables_SurviveWireRoundTrip`
with a write-side `TargetInvocationException` in `renderer/this.cs:118` /
`json/writer.cs:157`. Reverted.

Lesson: the **`JsonElement` value-slot is load-bearing in multiple downstream
paths** (at least the snapshot render → re-serialize round-trip and the renderer).
Changing the value-slot form for scalars alone breaks consumers that assume a
`JsonElement`. So Part A (the value decode + demolition) and Part B (the
`list`/`dict` raw-or-Data backing that *receives* the decoded plain values) have
to land as one change — the decode produces plain CLR / raw containers, and every
consumer (read normalize-on-read, the renderer, the snapshot render path) must be
moved to that shape in the same pass. Grep the consumers of the value slot before
cutting: `JsonElement`, `LiftArrayElements`, `LiftDataIfShaped`, `FromRaw`,
`renderer`/`Writer.Value`, and `snapshot/serializer/Default.Render`.

Working tree is clean at the milestone; `Variables_Survive` passes; build green.
Implementation not started — needs a fresh full-context pass over Part A + B as one unit.
