# Docs v2 Summary — Action-Based Conditions

## What this is

Documentation for the action-based conditions feature in PLang Runtime2. This feature replaces the old `bool Condition` parameter on `condition.if` with structured `Left/Operator/Right` evaluation, adds `condition.compare` for compound conditions, a pluggable `IEvaluator` interface, and sub-step execution logic (indented steps after conditions). All code reviews passed (auditor, security, tester, codeanalyzer). This documentation pass is the final merge gate.

## What was done

### XML Doc Comments (4 files)
- **`PLang/Runtime2/modules/condition/if.cs`** — Class docs describing goal mode vs sub-step mode, property docs for Left/Operator/Right/GoalIfTrue/GoalIfFalse, Run() method docs including error key "EvaluationError".
- **`PLang/Runtime2/modules/condition/compare.cs`** — Class docs explaining pure bool evaluation role in compound conditions, property docs, Run() method docs.
- **`PLang/Runtime2/modules/condition/providers/IEvaluator.cs`** — Interface docs with pluggability explanation, Evaluate() with supported operators and exception docs, IsTruthy() with truthiness rules.
- **`PLang/Runtime2/modules/condition/providers/DefaultEvaluator.cs`** — Class docs on type normalization and case-insensitivity, `/// <inheritdoc/>` on both methods.

### Architecture Documentation (3 files)
- **`Documentation/Runtime2/modules.md`** — Expanded condition module from one-line entry to full section with `if`/`compare` details, sub-step mode, pluggable evaluator, supported operators.
- **`Documentation/Runtime2/README.md`** — Updated file tree to show `condition/providers/` subtree with IEvaluator.cs and DefaultEvaluator.cs.
- **`Documentation/Runtime2/good_to_know.md`** — Added two new sections: `__condition__` signal pattern (why MemoryStack, thread safety, nesting) and condition type normalization rules.

### CHANGELOG
- Drafted in `v2/result.md` — Added/Changed entries for PLang users.

## Code example

XML doc pattern on the If class:
```csharp
/// <summary>
/// Evaluates a condition and branches execution.
/// When <see cref="Operator"/> is null, performs a truthy check on <see cref="Left"/>.
/// Branches to <see cref="GoalIfTrue"/>/<see cref="GoalIfFalse"/> when set (goal mode),
/// or signals sub-step execution via the <c>__condition__</c> MemoryStack key (sub-step mode).
/// </summary>
[Action("if")]
public partial class If : IContext
```

## Verdict

**PASS** — All documentation gaps filled. One minor finding (missing PLang `.goal` examples) flagged for tester, not blocking since builder prompt hasn't been updated yet.
