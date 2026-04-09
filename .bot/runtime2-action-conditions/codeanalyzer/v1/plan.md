# Code Analysis Plan — v1

## Scope
5-pass analysis of coder v1 implementation of action-based conditions.

### Files to analyze
1. `PLang/App/modules/condition/providers/IEvaluator.cs` — interface
2. `PLang/App/modules/condition/providers/DefaultEvaluator.cs` — evaluator implementation
3. `PLang/App/modules/condition/if.cs` — refactored if handler
4. `PLang/App/modules/condition/compare.cs` — new compare action
5. `PLang/App/Goals/Goal/Steps/this.cs` — sub-step logic

### Analysis passes
1. OBP compliance (5 rules)
2. Simplification
3. Readability
4. Behavioral reasoning (what breaks silently?)
5. Deletion test (what's untested?)

### Key concerns to investigate
- Thread safety of `__condition__` Variables signal
- `DefaultEvaluator.Evaluate` throws `NotSupportedException` — does this violate "methods returning Data never throw"?
- Source generator compatibility with IContext-based handlers
- `new DefaultEvaluator()` hardcoding vs engine navigation
- Duplicate `<summary>` blocks on Steps.RunAsync
