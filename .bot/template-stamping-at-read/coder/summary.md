# Template stamping at read — summary

**Version:** v1, increment 1 landed.

## What this is
Move template stamping (the authored-vs-literal `%ref%` decision) off the post-parse
`StampedForm` walk and onto the read: **the reader hands the type its template mode
at construction; the type decides.** Trust rides the reader instance, never the
content — so a forged `%secret%` in an http body can't render.

## Increment 1 — born with the mode (landed, green, pushed)
- **`text` ctor**: `bool canTemplate` → `string? template`. `Template = (template !=
  null && HasHoles) ? template : null`. The old `canTemplate:true` hardcoded `"plang"`
  mode-blind — the bug. The type owns the holes-decision, so `HasVariableReference`
  (gates on `Template != null`) stays correct: a holeless string drops the mode.
- **`ReadContext.Template`** carries the mode; **`Wire`** gains a `Template` ctor
  param and feeds it into the `ReadContext` for `ITypeReader.Read`.
- **`_authored`** options on the plang serializer (`template="plang"`) — the single
  trusted construction site. `Deserialize<goal>` routes to it (a goal is the only
  inherently-authored type); `_inbound` (runtime messages) stays mode-off.
- **Trust boundary (traced):** goal deserialization is the only authored read. Path 1
  (`goal.list` → `Deserialize<goal>`) now reads through `_authored`. Path 2 (`GoalCall`
  → catalog options) still seam-stamps for now.
- **Seams stay this round** (`StampTemplates`/`StampedForm`) — idempotent; the read
  stamp + seam stamp coexist. Security already improves: runtime-ingest text no longer
  stamps at the ctor.

### Proof
`dev.sh full` green (counts identical to baseline). Security test
(`TemplateStampOnReadTests`): same `%ref%` bytes → `Template="plang"` under authored
mode, null under runtime mode, null for a holeless string even authored.

### Code example
```csharp
// text.Read — the reader hands the mode; the type decides:
new text.@this(reader.String(), ctx.Template) { Kind = kind }
//   ctx.Template "plang" + has holes  → live template
//   ctx.Template null  (runtime ingest) → literal
//   holeless string                     → literal regardless
```

## Increment 2 — container slots (landed, green, pushed)
`item.serializer.json.ReadSlot` now takes the `ReadContext`; a templated string slot
in an authored container rides as a stamped `text{Template}` item (the "elevated
slot"), a literal slot stays raw, a runtime-ingest slot is always raw. This also
**fixes the live bug** the design flagged (§1.3): the stamp rides the slot, so it
survives the container's fresh-per-read model instead of being re-derived by the
seam's materialize-once hack. `list`/`dict` readers thread `ctx`. Tests: authored
container stamps the `%ref%` slot + leaves the literal; runtime container stamps
neither. Full suite green (Wire 512).
Gap (covered by the still-present seam): a `%ref%` nested inside a *structured* slot
(object/array) goes through `ParseRaw → Parse` which doesn't thread the mode yet —
thread it when removing the seam.

## Remaining increments (to reach the OBP win — deleting `StampedForm`)
Removing the post-parse walk needs **every** authored read to stamp at read first:
2. **Path 2 (GoalCall)** — route the catalog goal-deserialize through the authored
   Wire so a sub-goal's `%ref%` params read-stamp (today they seam-stamp).
3. **Delete** `StampedForm`/`Authored`/`RawGraphHasRef`/`StampEntry` + the
   `goal.list`/`GoalCall` seams.
4. **`FromWire`** (the risk): rebuilds actions from already-parsed values — confirm
   each caller's upstream read is mode-on before deleting its seam; may keep it.
5. **path** mode-gating (thread the mode to path construction; today hardcodes "plang").
