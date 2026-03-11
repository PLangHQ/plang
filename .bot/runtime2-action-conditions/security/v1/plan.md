# Security Review Plan — Action-Based Conditions (v1)

## Scope

Security review of the action-based conditions implementation on branch `runtime2-action-conditions`. Five production files, focused on the evaluator, condition handler, and sub-step signaling mechanism.

### Target Files
1. `PLang/Runtime2/modules/condition/providers/IEvaluator.cs` — evaluator interface
2. `PLang/Runtime2/modules/condition/providers/DefaultEvaluator.cs` — operator handling, type normalization
3. `PLang/Runtime2/modules/condition/if.cs` — condition handler, `__condition__` signal
4. `PLang/Runtime2/modules/condition/compare.cs` — pure evaluation action
5. `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs` — sub-step skip logic consuming `__condition__`

## Phase 1: Blue Team (Defensive Audit)

Map attack surface for each file:

- **DefaultEvaluator**: operator string from .pr file, `object?` left/right from deserialized JSON, `NormalizeTypes` recursion, `Compare` with incompatible types, `ContainsElement` iteration cost
- **if.cs**: `__condition__` MemoryStack signal — write-protected? spoofable? exception handling for evaluator failures
- **compare.cs**: same evaluator concerns, no `__condition__` signal (good)
- **Steps/this.cs**: `__condition__` consumption — type check (`Value is not true`), cleanup semantics, race conditions

## Phase 2: Red Team (Offensive Analysis)

Attack vectors to investigate:
1. **Operator injection** — crafted .pr with unexpected operator string → `NotSupportedException` (unhandled)
2. **Type confusion in Compare** — `IComparable.CompareTo` with incompatible types → `ArgumentException` (unhandled)
3. **`__condition__` signal spoofing** — user/attacker sets `%__condition__%` to bypass condition checks
4. **DoS via large collections** — `ContainsElement` with O(n) normalization per element
5. **NormalizeTypes recursion** — string→number→renormalize chain (bounded at depth 2, likely safe)
6. **`conditionSignal.Value is not true` type confusion** — non-bool values in `__condition__`

## Phase 3: Report & Verdict

Write `security-report.json`, `verdict.json`, summaries. Commit and push.

## Threat Model Reminder

PLang is user-sovereign. The user IS admin. Key question for each finding: is this exploitable by **untrusted external data** (malicious .pr files, external API responses flowing into conditions), or only by the user themselves? User-self attacks are informational, not vulnerabilities.
