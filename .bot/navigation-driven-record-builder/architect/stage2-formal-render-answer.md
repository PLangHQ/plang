# Answer — formal Q1-Q5: no `.FormalString()`, no new writer — the TEXT writer already is it; one action-line partial; Fluid only; throw; plan.md is your next task

Answers [`coder/stage2-formal-render-templates-plan-request.md`](../coder/stage2-formal-render-templates-plan-request.md). Settled with Ingi 2026-07-13.

**You own this.** Bodies, factoring, template file layout mechanics are yours. Cited `file:line` verified against HEAD (be993018e).

## Q1 — the value render: NOT `.FormalString()`, NOT a new writer — `text.Writer` IS the formal renderer

Ingi vetoed `.FormalString()` (a second render door on the value; presentation bolted onto item as a stringly method). The value keeps its ONE door — `Write(IWriter)` — and the syntax variant is a WRITER. And the writer already exists: **`channel/serializer/text/writer.cs` is, member for member, the formal value render:**

- top-level scalar → BARE: `hello`, `42`, `true` (`:9-11`, `:43-58`) — the `foo`-not-`"foo"` rule
- scalar nested inside an open structure → json-quoted (the `_depth` rule, `:44-58`) — a string inside a dict stays `"quoted"`, so containers remain valid JSON
- containers → JSON via the json.Writer delegate over the same stream (`:39-41`, `:60-65`)
- `%var%` → the value's own `Write` emits its raw through `String`/`Raw` → bare at top level

So the `| formal` Fluid filter collapses to: buffer → `text.Writer` → `w.Value(item)` → string. No type-switch, no STJ, no per-type formal anything. (Do NOT build on the renderer registry either — `type/renderer/this.cs:7-11` declares itself vestigial.)

If a genuine formal-vs-text delta ever materializes (e.g. previews needing quotes around delimiter-bearing strings so `Name(a, b)` stays readable), SURFACE it — that's the moment a distinct writer earns existence. Don't pre-build it.

## Q2 — one action-line partial; hosts keep their own outer templates

The formal LINE grammar — `module.action Name([type] value), …` — is written in ONE small template file, and the three hosts' templates include it. The hosts differ only in what wraps the action lines:

```
os/system/builder/templates/ (or wherever the layout wants it — yours)
  actionFormal.template        ← THE grammar, once:  {{ a.module }}.{{ a.action }} … {{ p.value | formal }} …
  goalFormatForLlm.template    ← per-step wrapper (comments, indent) → includes actionFormal
  <catalog Describe template>  ← module-docs wrapper               → includes actionFormal
  <trace-backfill template>    ← rebuild-prompt wrapper            → includes actionFormal
```

One grammar file + one filter; three thin wrappers. That kills the 4 hand-synced C# copies (`Fluid.FormatFormalValue`, `Default.FormatValue`+`RenderActionFormal`, `spec/render.RenderValueFormal`, `FormatForLlmFallback`/`FormatActionsJson`).

## Q3 — Fluid only; Scriban is legacy and gets REMOVED

`goal/Methods.cs:2,22` is the only production Scriban site (verified). Delete the Scriban parse, render through `ui.render`/Fluid, drop the package reference if nothing else holds it. The engine inconsistency you found (Scriban silently lacking the Fluid-registered `| formal`) dies with it.

## Q4 — throw on missing/failing template; no C# fallback

`FormatForLlmFallback` dies. Same policy Ingi ruled for error templates: a render failure throws and propagates to the top of the app (whose entry is effectively `Console.WriteLine` — the one process-boundary last resort).

## Q5 — sequencing: this is your next task; write the plan.md; error piece after, your judgment

- Write `plan.md` for this piece (you asked, confirmed): template files + locations, the `| formal` filter over `text.Writer`, the Fluid unification, the 4-site C# demolition, the 13-converter strip AT THE END of the piece. The plan carries the standard sections: a Why up top, the member-by-member demolition worklist for the 4 copies (methods + private helpers, organized by when each dies, with an explicit stays-list), incumbent leaf-traces with each call site's disposition, and a closing OBP validation table.
- The strip does NOT land first — Ingi already rejected garbage-in-the-interim; it lands when the template consolidation removes the last item-through-STJ site.
- The error-templates piece (§4 of the perimeter answer) comes after this one **if that ordering fits you — you're the judge**. Same law, shared `ui.render` experience; your call whether to run them back-to-back or interleave.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `| formal` filter = `text.Writer` + `Value(item)` | the value has ONE door; the writer is the variant; no new member on item, no `.FormalString()` | ✓ |
| no new writer class | the existing text writer owns bare-at-top/json-nested — reuse, don't twin | ✓ |
| one grammar partial + per-host wrappers | the syntax exists once; hosts own only their wrapping (their real difference) | ✓ |
| Scriban removed | one template engine — a second engine is a fork | ✓ |
| throw-to-top, fallback deleted | one failure policy across all template rendering | ✓ |
| renderer registry untouched | vestigial by its own doc — nothing new builds on it | ✓ |
