# Architect — prevars-in-pr

## 2026-05-30 — reframed perf/span proposal into build-time value transforms

Pulled the branch and reviewed `proposal.md` (pre-parsed `%variables%` in `.pr`, perf-framed). Verdict: the perf/span half is a no-go — the builder does no `%var%` parsing today (verified against the merged base), so the spans are not free to harvest, storing them creates a second parser plus a sync obligation, and spans are regex-recomputable. Merged latest `runtime2` into the branch (clean, pushed) and re-checked: the builder grew a confidence/build-event pipeline and the reference grammar grew a property suffix (`%x!cost%`), but still zero build-time `%var%` parsing — the verdict held.

The conversation then uncovered the real idea behind the branch: the builder feeds each variable's *type surface* (properties/methods) to the compile LLM, which compiles natural language (`%photo% resized to 200x200`) into navigation expressions (`%photo.Resize(200,200)%`) stored in the parameter `value`. Runtime executes deterministically via the existing navigator, which already does chained method calls. The `.pr` barely changes. Found `os/system/modules/MapVariables.goal` + `MapVariablesSystem.llm` — orphaned, unwired sketches of the same instinct from a while back; left in place pending Ingi's call on deletion.

Deciding principle captured: **store derived data in the `.pr` only when re-deriving it needs the LLM.** Spans fail (regex rederives); LLM-inferred transform mappings pass (the `.pr` is the only home).

Status: design direction captured, nothing built. Open decisions deferred by Ingi: what is navigable (marker/convention), routing tie-break (confidence preferred; value-method over action if static), the collection-query frontier (`where/select/orderby/take`), and purity-boundary enforcement.

Output:
| File | Purpose |
|------|---------|
| [plan.md](plan.md) | Spine — verdict, the real idea, the principle, two-paths rules, open decisions |
| [plan/examples.md](plan/examples.md) | Example ladder with real `.pr` snippets |
