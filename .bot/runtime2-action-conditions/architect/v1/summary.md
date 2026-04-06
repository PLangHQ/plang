# v1 Summary — Action-Based Conditions

## What this is

Redesign of `condition.if` to support structured comparisons (Left/Operator/Right) instead of packing expressions into a single `bool Condition` parameter that the runtime can't evaluate. Also adds sub-step execution (indented blocks under conditions) and a pluggable comparison engine pattern.

## What was done

Architectural design covering four interconnected concerns:

1. **Structured conditions** — `condition.if` takes `Left`, `Operator`, `Right` instead of `bool Condition`. No operator = truthy check. With operator = comparison. Any module can be the left side via multi-action steps.

2. **`condition.compare`** — Pure evaluation action (returns bool, no branching). Used for intermediate results in compound AND/OR conditions.

3. **Sub-step execution** — Indented steps under a condition execute only when the condition is true. Steps.RunAsync tracks a local `skipBelowIndent` variable — per-invocation, no mutation of shared Step objects. Thread-safe for concurrent web requests.

4. **Pluggable providers** — `providers/` folder inside each module. `IEvaluator` interface + `DefaultEvaluator` for conditions. Resolution via `engine.Libraries.GetProvider<IEvaluator>()`. Generic pattern reusable by db, template, crypto, etc.

### Key decisions (from Ingi's steering)

- Sub-steps are runtime, not builder tricks — the builder is non-deterministic, so the runtime must handle indentation deterministically
- Step is readonly and shared across threads — execution decisions are local to the runner, never stored on Step
- Providers live in the module they belong to, not on a global engine registry
- Build-time validation: indented steps must be children of a condition step

### Files in plan

- `PLang/App/modules/condition/if.cs` — modify
- `PLang/App/modules/condition/compare.cs` — new
- `PLang/App/modules/condition/providers/IEvaluator.cs` — new
- `PLang/App/modules/condition/providers/DefaultEvaluator.cs` — new
- `PLang/App/Engine/Goals/Goal/Steps/this.cs` — modify (sub-step skip logic)
- `system/builder/llm/BuildGoal.llm` — modify (condition examples + rules)

## Next step

Run **test-designer** to create test suites from this plan.
