
## architect — 2026-07-24
**Target:** /workspace/plang/CLAUDE.md (Runtime2 Conventions) or Documentation/v0.2/good_to_know.md
**Why:** Two laws settled repeatedly on goal-graph-singular (four+ independent rulings each: goal.call one-structure, typed-value-set-reader, node-list-values, wiring-snag/back-ref). Future work in the graph/value area must respect them or the sediment patterns return.
**Proposed change:**

```markdown
- **One structure per type; parse at the boundary.** A type has ONE structure: `Output` writes it, its Reader reads it back, its LLM-emit schema is it. Raw forms (bytes, JsonElement, the scalar string form) become the typed value exactly once, at the boundary where they enter, through the type's own door (Reader for streams, `Create` for values — the value's kind decides which). Interior code receives the typed value or fails loud. A re-parse at a consumer (dict/JsonElement arms, `To<Type>()` helpers, `FromSlots`-style slot readers) is always a patch over a boundary that dropped the type — fix the boundary, never add the arm.
- **The graph is the program; context belongs to the run.** Program structure (goal/step/action + their node lists) never stores run state: no actor context, no child→parent back-refs (`step.Goal`/`action.Step` are deleted — the run carries parentage via `context.Goal`/`context.Step` and the Call frame captured at push), no setters that run after load, no traversal door (no `Walk`/`ForEachAction` — code meets the graph at the right moment: birth facts stamped by parser/builder and filtered by collections (`IsSetup`/`IsTest`/`Tag`/`Synthetic`/`step.Variable`), execution via the lifecycle, presentation via templates over the value face). The node collections own three recursions — `Run`, `Output`, `Validate` — the node is always the iterator of itself; no public typed-element face exists (`.list`/`.Elements` harvesting is the smell; internal face = Add/IndexOf/positional indexer only).
```
