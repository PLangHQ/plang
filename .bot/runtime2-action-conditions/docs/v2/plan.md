# Documentation Plan — Action-Based Conditions (v2)

## Context

The action-based conditions feature replaces `condition.if`'s single `bool Condition` parameter with structured `Left/Operator/Right` evaluation, adds `condition.compare` for compound conditions, a pluggable `IEvaluator` interface with `DefaultEvaluator`, and sub-step execution logic in `Steps.RunAsync`. Coder v2 is final (auditor PASS, tester APPROVED, security PASS, 1595 tests).

## Documentation Gaps Identified

### 1. XML Doc Comments — Missing or Incomplete

| File | Gap |
|------|-----|
| `modules/condition/if.cs` | No XML docs on class, properties, or `Run()` |
| `modules/condition/compare.cs` | No XML docs on class, properties, or `Run()` |
| `modules/condition/providers/IEvaluator.cs` | No XML docs on interface or methods |
| `modules/condition/providers/DefaultEvaluator.cs` | No XML docs on class or `Evaluate()`. Only `ContainsElement` has docs. |

### 2. Architecture Documentation — Updates Needed

| File | Update |
|------|--------|
| `Documentation/Runtime2/modules.md` | Condition module entry says only "Condition evaluation". Needs update to describe `if` and `compare` actions with their parameters. |
| `Documentation/Runtime2/README.md` | File tree shows `condition/` but no detail. Add `providers/` subtree. |
| `Documentation/Runtime2/good_to_know.md` | Add entry about `__condition__` MemoryStack signal pattern and sub-step execution. |

### 3. Error Messages — Already Good

Both `If.Run()` and `Compare.Run()` produce rich `ValidationError` with operator, operand types/values, and `FixSuggestion`. No issues.

### 4. CHANGELOG Entry

Draft a changelog entry for the action-based conditions feature.

### 5. PLang Examples — Flag Only

No `.goal` examples exist for the new condition format. PLang pipeline tests are deferred pending builder prompt update. **Flag this and note it — don't fail the verdict** since the builder prompt hasn't been updated yet and examples can't be validated.

## Plan

1. Add XML doc comments to all 4 production files (if.cs, compare.cs, IEvaluator.cs, DefaultEvaluator.cs)
2. Update `modules.md` condition section with `if` and `compare` action details
3. Update `README.md` file tree to include `providers/` subtree under `condition/`
4. Add `__condition__` signal and sub-step execution entry to `good_to_know.md`
5. Draft CHANGELOG entry in `v2/result.md`
6. Write `docs-report.json`, `verdict.json`, `summary.md`
