## architect — v1 — 2026-10-15
**Target:** `CLAUDE.md`, the "Action `Start()` returns are typed via the signature" bullet (Runtime2 Conventions)
**Why:** it says "`Task<Data<object>>` for genuinely polymorphic returns (catalog: `→ returns data`)" and "for owned-construction actions, use `data.@this<object>.Ok(value)`". That can't compile: `data.@this<T>` is constrained `where T : global::app.type.item.@this, global::app.type.item.ICreate<T>` (`PLang/app/data/this.cs:764–765`), and no action in `PLang/app/module/**` returns `Data<object>`. On kind-is-type (decision 821) I told the coder to use it for render's polymorphic return; the coder found it won't compile, and render now forwards the `Data` its format's birth answers (`kind.Decode`), bare `Task<Data>`, the `llm.query` pattern. The rule as written sends the next reader the same wrong way.
**Proposed change:** replace the two `object` clauses:

```
`Task<Data<T>>` for concrete T (catalog: `→ returns T`); bare `Task<Data>` for a return whose type isn't one T: a forwarder that returns a `Data` produced elsewhere (`goal.call`, `llm.query`, `output.ask`, condition evaluators, `ui.render` forwarding its format's birth); the catalog reads it as `→ returns item` (`goal/step/action/this.Schema.cs:61`). `data.@this<T>` requires T to be an item (`data/this.cs:764–765`), so there is no `Data<object>`. **Footgun:** never `return innerDataInstance;` wrapped as another Data: a bare `Task<Data>` returns the inner Data as it is.
```
