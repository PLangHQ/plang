# Security Review v1 — Action-Based Conditions

## What this is

Security review (blue team + red team) of the action-based conditions implementation: `DefaultEvaluator`, `condition.if`, `condition.compare`, and the sub-step skip logic in `Steps.RunAsync`. Focused on injection risks in operator/value handling, type confusion, `__condition__` signal spoofing, and DoS vectors.

## What was done

### Blue team — attack surface mapped

Five areas analyzed:
1. **Evaluator operator dispatch** — operator string from .pr, switched via `ToLowerInvariant()`. Unknown operators throw `NotSupportedException`, caught by Step.Methods.cs outer catch. Clean.
2. **Type normalization & comparison** — `NormalizeTypes` recursion bounded at depth 2 (safe). `Compare()` silently returns 0 for non-IComparable (logic error, not crash).
3. **`__condition__` signal mechanism** — unprotected MemoryStack key used for inter-step communication. Primary finding.
4. **Sub-step skip logic** — local variables, thread-safe. Indent values from .pr not validated but fail-open.
5. **Collection iteration** — O(n) with normalization per element, no size limit. Standard risk.

### Red team — 5 findings

| ID | Severity | Category | Vector | Status |
|----|----------|----------|--------|--------|
| 1 | **Medium** | code-execution | `__condition__` stale signal from non-condition step bypasses condition check | open |
| 2 | Low | injection | Evaluator exceptions not caught in If.Run/Compare.Run — violates Data-return convention | open |
| 3 | Low | resource-exhaustion | ContainsElement O(n) iteration on attacker-controlled collections | accepted-risk |
| 4 | Low | deserialization | Compare() returns 0 for non-IComparable — silent logic error | open |
| 5 | Low | injection | `is not true` strict bool check is actually fail-secure | accepted-risk |

### Verdict: PASS

No critical or high findings. The medium finding (#1) requires specific preconditions (a non-condition step that writes `__condition__` followed by a step with indented children).

## Key finding detail — `__condition__` signal spoofing (#1)

The attack path: A step that is NOT a condition handler but writes to `__condition__` in MemoryStack (via `variable/set` or a custom handler). If that step has no indented children, `HasIndentedChildren` is false, so the signal is NOT consumed. The next step that DOES have indented children reads the stale signal and acts on it.

```
Step 0 (indent 0): variable/set __condition__ = true   ← no children, signal not consumed
Step 1 (indent 0): condition.if Left=false              ← sets __condition__ = false
Step 2 (indent 1): sensitive-action                     ← skipped (correct, If overwrote)
```

The overwrite by Step 1 makes this safe in the common case. The vulnerable pattern is:

```
Step 0 (indent 0): custom-action that sets __condition__=true  ← stale signal
Step 1 (indent 0): non-condition step WITH indented children   ← reads stale signal!
Step 2 (indent 1): executes because stale signal was true      ← should default to execute anyway
```

Wait — for non-condition steps, `conditionSignal != null` would be true (stale signal exists), and `conditionSignal.Value is not true` would be false (it IS true), so children execute. If the stale signal were `false`, children would be skipped even though Step 1 isn't a condition. This is the real risk: a stale `__condition__=false` from a prior step skips children of a non-condition step.

Proposed fix: clear `__condition__` before each step execution, not just on consumption.

## Files produced
- `.bot/runtime2-action-conditions/security-report.json` — full report
- `.bot/runtime2-action-conditions/security/v1/verdict.json` — PASS
- `.bot/runtime2-action-conditions/security/v1/plan.md`
- `.bot/runtime2-action-conditions/security/v1/summary.md` (this file)
