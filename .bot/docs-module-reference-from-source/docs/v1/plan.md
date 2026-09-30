# v1 — Learner module reference generated from source

## Branch
`docs/module-reference-from-source`, based on `app-systems`.

## Problem
Learner-facing module docs (`docs/modules/*.md`) are hand-written and drift from
the code — parameter tables already disagree with handlers (condition `if`,
error `handle`, output `write`). Ingi's direction: the property data should live
in the `os/system/modules/<module>/<action>.{description,notes,examples}.md`
source (some already does on `app-systems`), and the learner pages should be
**generated** from that source + the handler attributes, so nothing drifts.

## Decision (Ingi, this session)
Source shape = **one `notes.md` per action, tagged**. One line per property:

```
Pattern — which files come back, as a glob. · say: matching '<glob>' · builder: only when the step names one
```

- The prose before the first `·` = the learner description ("what it changes").
- `· say:` = how you type it in a step (the "How you say it" column).
- `· builder:` = the compile-only directive (when the planner should emit it).

One file feeds both audiences; the render strips the tag the audience doesn't need.

## Fact ownership (drift-proof: one home per fact)
| Fact | Source |
|---|---|
| Action summary line | `<action>.description.md` |
| Example steps | `<action>.examples.md` (`Step text:` lines) |
| Property name, "what it changes", "how you say it" | `<action>.notes.md` (prose + `· say:`) |
| Type / Required / Default | handler C# attributes (`data.@this<T>`, `[Default]`, nullability) |
| Returns | handler `Start()`/`Run()` return type (`Data<T>` → T) |

## Scope split
- **Docs (this branch, me):** the tagged-`notes.md` format; enrich the prose;
  the generator **spec**; a golden output sample for the `file` module.
- **Coder (hand-off):** the render pass itself (reuse `app.goal.step.action.@this`
  `.Description/.Notes/.Examples` + property shape), and teach the compile
  teaching to strip `· say:`/`· builder:` so tags never reach build prompts.

## Deliverables v1
1. `Documentation/v0.2/module-reference-generation.md` — the spec + golden sample.
2. Enriched `file/*.notes.md` samples (in the spec, not yet applied live — applying
   live is coupled to the loader tag-stripping, which lands with the generator).
3. Hand-off note for coder + architect.

## Status
Blocked on nothing for the spec. Live enrichment + generator are a coordinated
coder feature; not committed to live `notes.md` until tag-stripping exists.
