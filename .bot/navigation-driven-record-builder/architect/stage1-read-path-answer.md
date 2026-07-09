# Decision — one producer: `ReadText` defers everything; the eager-convert branch dies

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage1-goal-read-path-divergence.md`.

## A — total. Not redirected for host types; deleted for everything.

Your worry ("json/snapshot blast radius → the branch grows a reader-owned-host-type fork, which smells") dissolves against the model: `ReadText`'s eager-materialize branch (`this.Operations.cs:113-119` + the snapshot arm at `:77`) is the **old world** at the read seam — eager, through the `[Obsolete]` TryConvert. The settled model already defines what a file read produces: **a source-backed Data stamped with its declared type, materialized on first touch through the ONE reader** (Everything Is Lazy + the defer-first rule).

- `.pr` → deferred → `.Value()` → the goal reader → `clr<goal>` — your Path 1 becomes the *only* path.
- `.json` → deferred → the json reader → `clr(json)` — not blast radius, **alignment**: "external json stays a clr(json), never a materialized dict" is the settled clr-navigators reader pivot. The eager branch was violating it.
- build-snapshot `.pr` → same deferral, same goal reader.

No host-type fork, because nothing is eager anymore. One producer (the reader registry), every type; the `[Obsolete]` TryConvert dependency at this seam is gone; verbatim passthrough (read → write, zero parse) starts actually holding at file reads. Option B (wrap at Convert) kept two producers alive — dead on never-diverge, as you suspected.

Watch-outs while landing:
- A caller that `Peek()`s a fresh file-read now sees the raw source form (the source contract: sync face = unparsed raw). Callers that need the value use `.Value()` — your migrated consumers already do. Fix stragglers as the tests surface them.
- The type stamp comes from the mime/extension mapping as today (`{goal}`, `{object, json}`, …) — only the *materialization timing* moves, not the typing.

## The `.Count` tail — dispatch on the SEGMENT, not the key string

The distinction you're missing already exists in the path grammar: `Segment.Index` (`[0]`) vs `Segment.Member` (`.Count`). The list kind's `Descend` should not `int.TryParse` a key — it receives the segment kind:

- **Index segment** → the sequence answer (element at position).
- **Member segment** on a CLR list host → the property answer (`Count` is a real public property on the host's class — the host's declaration is the source of truth).

Dispatching on segment kind is not a fork — the grammar carries the distinction; two genuinely different asks (positional vs named) get two answers from the object that owns both faces. This likely means `Descend`'s signature takes the segment (or an `isIndex` fact) rather than a bare string — your shape call.

## Tail #1

Confirmed — the `ReferenceEquals(Peek(), goal)` / `IsTypeOf<goal>` assertions are old-model; update to the Peek→self clr model (`.Clr<goal>()`). Test-authoring fix, no design content.
