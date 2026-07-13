# Addendum — formal render, post-trace: text.Writer whole (quote rule REMOVED), the Data async accessor, views hand back their native, text.Writer's missing item arm

Extends [`stage2-formal-render-answer.md`](stage2-formal-render-answer.md) after a full code trace of the suggested design. Settled with Ingi 2026-07-13. Cited `file:line` verified against HEAD (a981d523f).

**You own this.** These four findings fold into your plan.md; bodies and mechanics yours.

## 1. The space/comma quote rule is REMOVED — `text.Writer` is the ENTIRE formal value render

Ingi ruled the incumbent quoting (`ui/code/Fluid.cs:158-162` — wrap a value in `"…"` when it contains space or comma) is wrong and dies UNREPLICATED. No thin formal writer, no new writer of any kind: the `| formal` value render is `text.Writer`, whole.

Why it's safe: the formal grammar's parens already delimit — every parameter is `name(…)`, so a new parameter can only begin after `), `; a comma or space INSIDE the parens was never actually ambiguous. The one genuine residual collision — a value containing `)` — is ACCEPTED as a known edge: these lines are LLM-facing previews, nothing machine-parses them back. Revisit only if builder evals show real confusion; don't pre-build an escaping scheme.

`%var%` needs no special arm either: it's a string, `text.Writer` writes it bare at top level.

Goldens: pin the NEW clean output (no quotes anywhere). The builder-prompt change is accepted (no-backward-compat).

## 2. Fluid gets an async `Value` accessor on `data.@this` — the #1 plan addition

Traced: no accessor exists anywhere (`ui/code/Fluid.cs` is the only Fluid site; nothing registers on Data). `{{ user }}` works today only because binding EAGERLY materializes (`:122` — `FluidValue.Create(await kvp.Value.Value(), …)`); a Data reached by reflection mid-render (`p` inside the goal graph) has nobody to open it, so `{{ p.Value | formal }}` binds nil — the template line has never worked (it only ever ran under Scriban → C# fallback).

Fix: register a `ValueTask` member accessor on `data.@this` so `{{ p.Value }}` calls `await d.Value()` lazily (Fluid supports async accessors natively). This makes the goal template's existing grammar real. Follow-up the plan may take or defer: with the accessor in place, the eager materialization loop at `:122` can thin to binding the Data itself.

## 3. The filter's input — the views hand back their native

Fluid's converters dismantle values at binding: `dict.@this` → `NativeDictView`, `list.@this` → `NativeListView` (`:181-194`), entries answer `.Peek()` (`:207,:224,:250,:258`). So `| formal` receives Fluid wrappers/views/raw scalars — never an item — which is why `UnwrapFluid` (`:278-297`) exists. Fix: the views already hold their backing native (`NativeDictView(dict.@this d)`) — expose it (internal is fine) so the filter reaches the value and drives `text.Writer` with it; raw scalars go straight to `w.Value(raw)` (bare at depth 0, `text/writer.cs:43-58`). `UnwrapFluid` and `FormatFormalValue` die.

## 4. `text.Writer.Value()` is missing the item arm — fix it in this piece

`json.Writer.Value` dispatches an item to its own door (`case app.type.item.@this v: v.Write(this)`, `json/writer.cs:184`); `text.Writer.Value` (`text/writer.cs:71-95`) has no item case, so a top-level ITEM falls to `default:` → `Structural()` → json-QUOTED — while the same item through `item.Write(textWriter)` renders bare. The two entrances disagree; the filter drives `Value()`, so add the mirror arm (`case item.@this v: v.Write(this);` before the default). This is a real latent bug independent of this piece — pin it with its own small test.

## Unchanged from the base answer

One `actionFormal` partial + per-host wrappers (the include/render machinery is already wired — `PlangFileProvider`, `:108-110`, AuthGated; note resolution is relative to the CALLING GOAL's dir, `:319-329` — pin the partial's location accordingly); Fluid only, Scriban removed; throw-no-fallback; plan.md is your next task with the strip at the piece's end; error-templates piece after, your judgment.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `text.Writer` as the whole formal render | one writer, zero new members, zero new classes; the grammar's parens carry the delimiting | ✓ |
| quote rule deleted unreplicated | a rule nobody set deliberately dies once its four copies die; known `)` edge accepted, not pre-engineered | ✓ |
| async `Value` accessor on Data | the template reaches the value through the value's own door, lazily — no eager Peek-and-copy pipeline | ✓ |
| views expose their backing native | the wrapper hands over the whole box; the filter never rebuilds a raw copy (`UnwrapFluid` dies) | ✓ |
| `text.Writer.Value` item arm | both entrances (`Value(item)` / `item.Write(w)`) agree — courier consistency | ✓ |
