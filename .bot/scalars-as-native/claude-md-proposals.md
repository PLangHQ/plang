## builder — v1 — 2026-06-06
**Target:** /workspace/plang/CLAUDE.md
**Why:** Debugging the `assert → error.throw` build regression on this branch, I had
to grep the codebase to discover that PLang's template engine is **Fluid** (a .NET
Liquid implementation) and where its variable-binding lives. That fact is load-bearing
for any builder/`.llm`/template debugging: the builder's whole prompt-construction
layer (`*.llm`, `*.template`) renders through Fluid, and Fluid binds PLang values
differently from PLang's own resolver — it can read real .NET `List<T>`/`Dictionary`/
POCOs but **not** native `dict`/`list`/`JsonNode` (they lack `IEnumerable`/
`IDictionary`). That mismatch silently renders template sections empty and was the
root cause of a branch-wide builder failure. Knowing the engine + this trap up front
would have saved a long investigation; it'll recur for anyone touching templates.
**Proposed change:** add to the "Key Files" section (or a new "Templating" note):

```
## Templating (Fluid)
- The template engine is **Fluid** (a .NET Liquid implementation). All `*.llm` and
  `*.template` files render through it via `ui.render` (`app.module.ui.render`).
  Binding lives in `PLang/app/module/ui/code/Fluid.cs` — variables are loaded with
  `FluidValue.Create(variable.Value, options)` in the render loop.
- **Fluid does NOT use PLang's variable navigator.** It can read real .NET
  `List<T>`/`Dictionary`/POCOs, but NOT PLang native `dict`/`list`
  (`app.type.{dict,list}.@this`) or `System.Text.Json.Nodes.JsonNode` (what
  `set … type=json` produces) — those implement domain interfaces, not
  `IEnumerable`/`IDictionary`, so `{{ x.key }}` / `{% for i in x %}` render EMPTY
  even though `%x.key%` resolves fine in PLang. If a value resolves in `--debug`
  but renders blank in a template, this is why.
- Debugging a wrong `.pr`: see `Documentation/v0.2/debugging-builder-failures.md`.
```
**Footer:** Filed by reviewer/builder bot under the "real incident on the branch"
exception (root-caused a branch-wide builder regression to this gap).
