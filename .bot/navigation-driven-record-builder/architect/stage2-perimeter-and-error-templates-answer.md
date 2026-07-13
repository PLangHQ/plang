# Answer — perimeter previews ride the ONE writer (not lower-then-STJ); acceptance flips to clean goldens; error rendering moves to os templates (next piece)

Answers [`coder/stage2-perimeter-converters-dont-fire-finding.md`](../coder/stage2-perimeter-converters-dont-fire-finding.md). Settled with Ingi 2026-07-13.

**You own this.** Bodies, factoring, and per-site mechanics are yours. Cited `file:line` verified against HEAD (7c73c5882).

## 1. Your finding is confirmed — and it changes the FIX, not just the acceptance

Your measurement is right and my premise was inverted: STJ dispatches converters by STATIC type, `Peek()` is `item.@this`-typed, so the 13 converters never fired at the perimeter and the previews already emit reflected garbage. The strip is behavior-neutral there.

But the fix is NOT lower-then-STJ — that supersedes both your proposed step 1 and my own earlier "lower FIRST" ruling. **Ruled: the preview sites produce their JSON through `json.Writer` — the one writer.**

```
REJECTED (lower-then-STJ):   p.Peek() → .Clr<object>() → raw CLR → STJ        ← a SECOND json producer, reborn in miniature
RULED (the one writer):      preview drives json.Writer over a buffer
                               ├─ scaffolding: BeginObject/Name/String          ({module, action, parameters})
                               └─ value slots: w.Value(p.Peek())                writer.cs:130 — dict → {}, list → [], leaf → v.Write(w)
```

Why lower-then-STJ loses, twice:

- **It rebuilds the fork this branch kills.** Six perimeter sites rendering the same dict through STJ while every channel renders it through the writer — and they drift (dates: `writer.DateTime` vs STJ ISO; enums: `writer.Enum` `ToString` vs camelCase; null handling). One value, two JSON faces again.
- **`[Sensitive]` egress into LLM prompts.** Previews go into prompts. A variable holding a clr host: `.Clr<object>()` returns the host itself, STJ reflects it raw — `[Sensitive]` fields land in the prompt. The writer routes hosts through the `*`-kind `Output`, which masks. STJ-after-lowering has no filter at all.

Mechanics: `json.Writer.Value()` is sync (`v.Write(w)` is sync), so the sync preview sites need no async change. Per site, two shapes — your pick: drive writer primitives for the scaffolding with `w.Value(peeked)` for value slots, or compose the preview shape as a native dict/list and render it with a single `w.Value(root)`. An unmaterialized source/wire in a preview: don't force materialization — its own `Write` handles its raw honestly; your call per site.

## 2. Acceptance — confirmed flipped, and in scope

Pin the CLEAN writer-produced output as the new goldens (today's output is garbage; byte-identical was never achievable). The perimeter fix lands in this close-out — same sites, one pass, and the previews improving is the point.

## 3. The strip proceeds

Behavior-neutral at the perimeter per your measurement. Your revised order stands with step 1 amended to writer-driven (above). The rest of the fork answer is unchanged: `writer.cs:88` nativize + the 14th converter per its `Wire`-dependency check; read side stays deferred with the plang asymmetry.

## 4. Error.cs — do NOT polish it; the human error face moves to plang templates (the NEXT directed piece)

Your "safe, nothing to re-home" was right for the strip (no converter fires there), but Ingi caught what it masked: `FormatVerboseValue` (`Error.cs:430-446`) is a pre-item relic. Its input (`kvp.Value.Peek()`, `:396`) is always an ITEM now — the `is string` arm and the `IDictionary`/`IList` STJ arm are dead code; everything already lands on `:444` `ToString()`, which is the right door (each item owns its debug face: dict `dict/this.cs:438`, list `list/this.cs:664`, text `text/this.cs:237`, number `number/this.cs:249`). Leave the method alone in this close-out — the whole rendering dies in the next piece:

### The design (settled with Ingi 2026-07-13)

**Two format families, split by CONSUMER, declared by the channel's MIME. Each serializer owns its own answer — no central `if (presentation)` fork anywhere.**

```
error leaves the actor → the channel's serializer answers
  ├─ SERIALIZATION (machine): application/json, application/plang
  │    → the error VALUE writes itself — one writer, [Out] face (IError.Wire IS this face). Never a template.
  └─ PRESENTATION (person): text/plain, text/html (md later)
       → os-owned template via ui.render (Fluid):  os/system/error/<code>.<ext>
            404.txt / 404.html
            fallback: exact code → century default (404→400, 503→500); 400/500 ship with the os
```

- **The template's data is `%error%` itself** — templates navigate the error's `[Out]` face: `%error.message%`, `%error.fixSuggestion%`, loop `%error.variables%`, recurse `%error.errorChain%`. The verbose variable dump becomes a debug-gated template loop, not a C# method.
- **Failure policy: throw.** Template missing / render failure throws and propagates to the top of the app — whose entry is effectively `Console.WriteLine`, the existing process-boundary last resort. No bootstrap machinery, no quiet fallback layer.
- **The console is included**: CLI error output becomes template-driven (`.txt` for text/plain, `.html` for html) — everything is plang.
- **Dies into templates**: `Error.Format` / `FormatVerbose` / `FormatVerboseValue` / `FormatExtra` (+ `ActionError`'s override) — the C# keeps only the throw-to-top path. The full member-by-member demolition list comes with the piece's plan, not this spec.

Sequencing: land the strip close-out (1-3 above) first; then this as the next directed piece on the branch. Flag back if its size wants a proper plan.md before you start — it likely does (template resolution, ui.render wiring, os template files, the C# formatter demolition).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| previews drive `json.Writer` | one JSON producer everywhere; the value writes itself at every nesting level | ✓ |
| `[Sensitive]` at LLM egress | masked via the `*`-kind `Output`, same rule as every channel | ✓ |
| error faces split by consumer, owned per serializer | variants-with-config: each serializer answers its own "how does an error leave through me"; no central fork | ✓ |
| templates at `os/system/error/<code>.<ext>` | presentation owned by the os layer, not C#; fallback ladder is data (files), not code | ✓ |
| throw-to-top failure policy | one process-boundary last resort (already exists); no second fallback layer | ✓ |
| `FormatVerboseValue` untouched now, deleted whole later | no polishing of code condemned by the next piece | ✓ |
