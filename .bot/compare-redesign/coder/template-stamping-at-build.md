# Design: template-stamping moves to build time (persisted on the wire)

> **SUPERSEDED by `template-stamping-at-read.md`.** Architect review rejected build-time + per-node wire marks in favour of stamping at read, gated by the reader's mode (no wire change, no `.pr` migration, no signing question). Read that file. This one is kept only for the rejected-alternatives record.

**Status:** designed + agreed with Ingi (this session). For architect review before implementation.
**Owner:** coder. **Branch:** compare-redesign.
**Related:** `list-dict-raw-slot-model.md` (the O(1) raw-slot model — already landed; this builds on it).

---

## 1. The problem

A builder-authored value that contains `%var%` holes (`output.write content="Hello %name%"`)
must **render** those holes at use-time. A value typed by a user at runtime
(`"%secret%"`) must print **literally** — never resolve. The flag that separates the
two is `item.@this.Template` (`"plang"` = authored template; `null` = literal/runtime).

Today that flag is applied **after** the value is built, by walking it:

- `Action.StampTemplates()` runs at three authored seams (`.pr` load via
  `goal/list.Add`, wire-rebuild via `Action.FromWire`, goal-call injection in
  `GoalCall`).
- It calls `Data.Authored()` → `StampedForm(instance)`, a
  **`switch(instance) { case text / list / dict / source / clr }`** living in
  `app/data/this.cs` that rebuilds the value stamped, recursing into container
  entries.

Three things are wrong with this:

1. **Misplaced behavior (OBP).** `Data` is a courier; it should not `switch` on the
   value's concrete type and reach inside each to stamp it. "How do I become an
   authored template" is a value concern, not a `Data` concern.
2. **Retrofit, not birth.** The authored-ness is *re-derived by walking* on every
   load, even though it is fully known at **build time** (the builder authored it).
3. **Fragility.** The walk assumes container reads return stable instances. After the
   raw-slot model removed cache-back (a read borns a fresh `Data`), the walk's
   "stamp entries in place, then rebuild from the same entries" broke — it re-read
   the container and discarded the stamps. (Patched with a materialize-once fix;
   that patch is the interim this design removes.)

## 2. The model

**The build decides authored-ness, deterministically, and persists it. Load reads it.
Nothing re-derives it.**

- **Authored-ness is decided once, at build**, per parameter, by a deterministic
  rule (below) — **not by the LLM**, and not re-derived per load.
- **The wire (`.pr`) persists the stamp** as part of the value.
- **Load mints the value already stamped** from the wire; the parser materializes the
  stamp, it never re-decides authored-ness.
- `StampTemplates` / `Authored` / `StampedForm` and the `data/this.cs` `switch` are
  **deleted**.

Security boundary, made explicit: runtime input never goes through build, so it
carries no stamp, so it stays raw and **never renders `%var%` by accident**. Today
that boundary is "the walk only runs at authored seams"; after this change it is "the
mark only exists on build output."

## 3. The stamping rule — who gets `template="plang"`

Stamping is **per-parameter, gated by the parameter's role**, generic across every
module. The role is known deterministically at build via the source generator
metadata (`IsVariableNameSlot` / `IRawNameResolvable`).

| Parameter role | Example | Stamp? |
|---|---|---|
| **Variable-name slot** (`Data<Variable>` / `IRawNameResolvable` — write targets, by-name lookups) | `set %x%`= → `name`; `add … to %list%` → `listname` | **Never.** It names a variable, resolved by `Variable.Resolve`, not rendered. |
| **Value param** containing a `%var%` ref (recursively, into list/dict) | `content="Hi %name%"`; `value="%item%"` | **`template="plang"`** |
| Value param, no `%var%` | `value="literal"` | none |
| Fluid `{{ }}` (e.g. `ui.render`) | `render 'Hello {{ name }}!'` | none — orthogonal, Fluid engine, not this mechanism |

- **`%var%` detection** = the ref regex `%[^%]+%`. `{{ }}` is explicitly *not* this.
- **Full-match refs** (`value="%item%"`, whole value is one ref) are stamped
  `template="plang"` like partial holes — same mechanism, no separate path. (The
  value door resolves a full-match to the variable's own `Data`.)

### Worked example — nested container

```plang
- set dict %x% = message:%message%, user:%user%
        / variable.set name="%x%"  (NAME slot → never stamped)
        /              value = dict { message:"%message%", user:"%user%" }
```

At **runtime** the value must be stamped at *every* node, because `dict.Value()`
renders an entry only when that entry's own value is a stamped template:

```
dict                       Template="plang"     ← "I have holes; run my render loop"
  message → text "%message%"   Template="plang" ← so the loop resolves this entry
  user    → text "%user%"      Template="plang"
  (mode:"fast" — no %var% — stays a plain literal, rendered as-is)
```

So the container **and** each `%var%` leaf are stamped; non-`%var%` entries stay
literal.

## 4. Wire shape — born exactly as persisted

**Decision (Ingi): the wire carries the fully-stamped tree; load does zero `%var%`
scan.** A stamped node rides as an `@schema:data`-marked `Data` carrying a new
`template` field:

- This reuses the existing `@schema:data` slot mechanism — the parser already
  reconstructs `@schema:data`-marked objects as `Data` inside dict/list slots
  (`item.serializer.json.ObjectLeaf` / `RawSlot` → `IsDataMarked`). A templated leaf
  rides as such a marked `Data`; **plain entries stay bare**, so no collision with a
  user dict and the bare-`{}` / `[]` shape is preserved.
- Marked at **every** templated node: the container (so its render loop runs) and
  each templated leaf (so the loop reaches it).
- Read side extends the existing `IsDataMarked` reconstruction to read `template` and
  mint the item already stamped — **no load-time `%var%` scan**.

Rejected alternative: one `template` flag on the top param + parser re-scans `%var%`
leaves at load. Cleaner wire, but it re-derives the leaf stamps at load; Ingi chose
"born exactly as persisted."

`template` is **render metadata, not value identity** — it is *not* part of the signed
canonical value (signing hashes value/type, not the template flag).

## 5. Implementation plan

**Piece 1 — Wire round-trip.** Add `template` to the `Data` wire shape
(`@schema:data` envelope). Write it when `item.Template != null`; read it back in the
`IsDataMarked` reconstruction so the minted item carries `Template`. A `%var%` string
→ `text.@this { Template="plang" }`; a marked container → born stamped.

**Piece 2 — Build stamping.** A deterministic C# pass over the compiled
`BuildResponse` actions (enrich/validate path), role-gated per the §3 table: skip
name-slots; for value params, walk the value and set `template="plang"` on the
container and every `%var%` leaf. Persisted into the `.pr` via Piece 1.

**Piece 3 — Born-at-load; delete the walk.** `.pr` load / `FromWire` / `GoalCall`
stop calling `StampTemplates()`. **Delete** `Action.StampTemplates`, `Data.Authored`,
`StampedForm`, `StampEntry`, `RawGraphHasRef`, the 3 call sites, and the
`switch(instance)` in `data/this.cs`.

**Piece 4 — Rebuild + tests.** `plang build` regenerates every `.pr` with the
persisted stamped tree (clean cut — an un-rebuilt `.pr` won't render its templates;
no runtime fallback). Update the `AsT_*` / `DeepResolution` tests that call
`.Authored()` directly to build pre-stamped values, and rename them (they exercise the
live `Value<T>()` door, not the long-gone `As<T>`).

Sequence: 1 → 2 → 3 → 4.

## 6. Decisions already settled (with Ingi)

- Authored-ness decided at **build**, deterministically, **not** the LLM. ✔
- Rule is **per-parameter, role-gated**: value-param-with-`%var%` → stamp;
  variable-name slot → never. ✔
- `{{ }}` (Fluid) is unrelated; only `%var%`. ✔
- Full-match refs stamped same as partial. ✔
- Wire carries the **fully-stamped tree** (granular, via `@schema:data` + `template`),
  zero load-time scan. ✔
- `template` lives **on the wire** (sibling/`@schema:data` field), not re-derived. ✔
- `StampedForm` + the `data/this.cs` switch are **deleted**. ✔

## 7. Open questions for architect

1. **`@schema:data` for every templated leaf** — a heavily-interpolated authored dict
   now serializes many small marked `Data` envelopes instead of bare scalars. Is the
   `.pr` size / readability acceptable, or is a lighter encoding wanted (e.g. a
   `%`-prefixed convention the reader recognizes without a full `Data` envelope)?
2. **Build stamping home** — a dedicated C# enrich/validate pass vs. folding into an
   existing one (`enrichResponse` / `validateStepActions`). Where does it sit relative
   to `BuildStep/Validate.goal`?
3. **Migration** — clean cut requires rebuilding all `.pr`. Any `.pr` consumed without
   a rebuild (cached, shipped, third-party) loses template rendering. Acceptable, or is
   a one-release fallback (derive-if-missing) warranted?
4. **Modifiers / defaults** — `StampTemplates` also stamps `Defaults` and recurses into
   `Modifiers`. The build pass must cover the same surface; confirm no authored value
   escapes the pass.
5. **Signing** — confirm `template` stays out of the canonical signed shape (render
   metadata, not value identity) so signatures are unaffected.
