
## architect — 2026-07-24
**Target:** /workspace/plang/CLAUDE.md (Runtime2 Conventions) or Documentation/v0.2/good_to_know.md
**Why:** Two laws settled repeatedly on goal-graph-singular (four+ independent rulings each: goal.call one-structure, typed-value-set-reader, node-list-values, wiring-snag/back-ref). Future work in the graph/value area must respect them or the sediment patterns return.
**Proposed change:**

```markdown
- **One structure per type; parse at the boundary.** A type has ONE structure: `Output` writes it, its Reader reads it back, its LLM-emit schema is it. Raw forms (bytes, JsonElement, the scalar string form) become the typed value exactly once, at the boundary where they enter, through the type's own door (Reader for streams, `Create` for values — the value's kind decides which). Interior code receives the typed value or fails loud. A re-parse at a consumer (dict/JsonElement arms, `To<Type>()` helpers, `FromSlots`-style slot readers) is always a patch over a boundary that dropped the type — fix the boundary, never add the arm.
- **The graph is the program; context belongs to the run.** Program structure (goal/step/action + their node lists) never stores run state: no actor context, no child→parent back-refs (`step.Goal`/`action.Step` are deleted — the run carries parentage via `context.Goal`/`context.Step` and the Call frame captured at push), no setters that run after load, no traversal door (no `Walk`/`ForEachAction` — code meets the graph at the right moment: birth facts stamped by parser/builder and filtered by collections (`IsSetup`/`IsTest`/`Tag`/`Synthetic`/`step.Variable`), execution via the lifecycle, presentation via templates over the value face). The node collections own three recursions — `Run`, `Output`, `Validate` — the node is always the iterator of itself; no public typed-element face exists (`.list`/`.Elements` harvesting is the smell; internal face = Add/IndexOf/positional indexer only).
```

## architect — 2026-09-22
**Target:** Documentation/v0.2/good_to_know.md
**Why:** Found during the (abandoned) formal-plang grammar attempt; the doc it lived in is deleted with the abandonment. The rule decides how nested actions, `%var%`, event goals and recovery chains are modelled, and it forbids the lazy-construction pattern that produced the last back-ref stamp on the graph.
**Proposed change:**

```markdown
### Every call is constructed at load; only its evaluation is deferred

A `%var%` is the model: built at load as a Data holding a name, evaluated when read. A nested action inside a value may be the same — constructed at load (so it is born holding its step), evaluated when its owner reads it. What may never happen is lazy CONSTRUCTION of program structure at run time: nobody holds the step at birth, and the graph ends up stamped afterwards.

Four relationships between calls, each with its own wire slot:
- **sequence** — runs next; the left result is `%!data%` (`step.action[]`)
- **wrap** — runs around its host (`action.modifier[]`)
- **body** — runs when the owning call decides (`action.child[]` for condition bodies; `action.recovery[]` for `error.handle`)
- **expression** — runs when the owner reads the parameter (a parameter's value — grammar-legal; no evaluator yet)

Plus **reference** — a value neither evaluated on read nor run by its owner, stored for later (an event handler's goal). Rides as the `goal.call` TYPE, never as a nested action.

A body written as an expression (recovery actions stored in a parameter) and a reference written as an expression (`channel.set Goal=goal.call(…)`) are the two known mis-modellings; both are fixed by moving the value into its correct slot/type.
```

## architect — 2026-09-22 (Stage D)
**Target:** /PLang/App/CLAUDE.md (or Documentation/v0.2/good_to_know.md)
**Why:** Coder hit this twice in one change while moving build validation onto the nodes: `await p.Value() is GoalCall` opened every parameter, an authored `%var%` (unset at build time) failed to resolve, and the failure surfaced three layers later in code nobody touched — the courier had marked the binding failed. It is a general build-time rule, not a Validate rule.
**Proposed change:**

```markdown
- **Build-time code judges SHAPE, never evaluates authored values ("judging must not resolve").** Anything that runs at build time — validation, Nest, Normalize, the repair loops — reads an action's declared types, catalog rows and structural slots, and never opens a parameter's VALUE except on a slot whose declared type is the thing being judged (`p.Type?.Name == "goal.call"` first, ask second). Authored values are constructed at load and evaluated on read; evaluating one at build time does the run's job at the wrong moment, and a failed evaluation poisons the binding for the run that follows — the error lands far from its cause. Corollary: `await p.Value() is X` is not a guard, it is the opened box; check the declared type, then ask.
```
