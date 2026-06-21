# v1 — template stamping at read (born-with-template, the type decides)

Branch `template-stamping-at-read`, off `compare-redesign` (post-IReader-merge).
Design: `.bot/compare-redesign/coder/template-stamping-at-read.md` + this session's
refinement with Ingi.

## The refinement (Ingi)
Don't stamp via a post-parse `Authored()` walk. **The reader hands the type its
template mode at construction; the type decides** (it knows its own holes). A
holeless string drops the mode (so `HasVariableReference => Template != null` stays
correct); a `%ref%` string keeps it. Render reads the stamp later.

## The trust boundary (traced this session)
Goal deserialization is the *only* authored read, reached two ways — `goal.list`
(`Deserialize<goal>`) and `GoalCall` (`file.read → source.Value → goal reader →
App.Type.Convert(text, goal)`). Both end at the plang Wire deserializing a goal.
`_inbound` is **shared** with runtime-ingest (channel messages), so the mode must
NOT ride `_inbound` — a **dedicated authored Wire** (`Template="plang"`) carries it,
used only when deserializing a goal. Runtime ingest (http/as-json → `object.json`;
messages → `_inbound` mode-off) stays literal. A goal is inherently authored code;
a runtime message is never deserialized as a goal.

## Increment 1 (this commit) — born-with-template on the read path
1. `ReadContext` gains `Template` (string?, default null).
2. `text.@this` ctor: `bool canTemplate` → `string? template`; `Template =
   (template != null && HasHoles) ? template : null`. The type owns the holes-decision.
   (`true` was hardcoding `"plang"` mode-blind — the bug.)
3. Callers: `text.Read` + `text/Default.Read` pass `ctx.Template`; `path` keeps
   `"plang"` (no read-mode at its ctor — mode-gating path is deferred, behavior preserved).
4. `Wire` gains a `Template` ctor param; feeds it into the `ReadContext` it builds
   for `ITypeReader.Read`.
5. Plang serializer: a dedicated **authored** options/Wire (`Template="plang"`);
   goal-deserialize routes to it (goal is the inherently-authored type).
6. Keep `StampedForm` + the seams this round (idempotent — read-stamp + seam-stamp
   coexist). Security improves immediately: runtime-ingest text no longer stamps at
   the ctor.

## Later increments
- Container slots: `ReadSlot` carries the mode → a templated slot stores a
  `text{Template}` item, not a raw string.
- Delete `StampedForm`/`Authored`/`RawGraphHasRef`/`StampEntry` + the `goal.list`/
  `GoalCall` seams.
- `FromWire` (risk): rebuilds from already-parsed values — confirm each caller's
  upstream read is mode-on before deleting its seam.
- path mode-gating (thread the mode to path construction).

## Verify
- `./dev.sh full` green vs baseline (the IReader merge — all 6 projects green).
- THE security test: same `%ref%` bytes render read-as-goal, literal read-as-http-body.
- Watch template/variable-resolution tests (Runtime suite).
