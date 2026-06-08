# Coder — compare-redesign

## Version: v1 (review of architect plan — no implementation)

**What this is.** The architect wrote the settled comparison-redesign plan (one lazy
`ValueTask` value door, value-as-raw-in-Data, rank-owned-by-the-type, sign-free
`Comparison` enum, sync ordering core, six stages). Ingi asked coder to read the plan +
stages, ground them against the real branch code, and write peer comments for the
architect to read before implementation starts.

**What was done.** Read `plan.md`, all six stage files, and both test docs. Verified the
load-bearing claims against code on `compare-redesign`. Wrote ranked comments to
`.bot/compare-redesign/coder/v1/comments.md`. No production code changed. Also repaired
the architect's malformed `report.json` (two stray `]` had pushed three action entries
outside the actions array) and appended the coder session.

**Verdict:** build it — the spine is right. Two concerns would most change the
implementation:

1. **The lazy async I/O source is net-new, not a wiring change.** The door's
   `await _source.ReadAndParse()` references infrastructure that doesn't exist — today's
   lazy path is a *sync* `_valueFactory` + `_raw` + sync `Materialize()`, and
   `ILoadable.LoadAsync` is per-type (image/binary), not Data-level. That's a stage-sized
   piece presented as one Stage-2 bullet; recommend splitting it out.
2. **The "~990 `.Value` reads" figure overcounts and migration is not mechanical.**
   `.Value` is overloaded (`Lazy<T>`, `KeyValuePair`, `Nullable`, and the type-wrapper
   views' *own* `.Value`). A find-replace rewrites the wrong receivers. Tied to this:
   stages 2–4 aren't independently green-able (the old mediator must run over the new raw
   value slot until Stage 6), so the green gate realistically moves to the 2→4 boundary.

Three more pinning asks: GetHashCode/Equals throw must ship per-type with the raw flip
(live dict keying); `contains`/`unique` on an `Incomparable` element errors per the
boundary table rather than returning not-found (likely not intended); `Value` is a
`virtual` property with overrides that the method-door drops. Full detail in
`v1/comments.md`.

**Next.** Ingi forwards the comments to the architect.
