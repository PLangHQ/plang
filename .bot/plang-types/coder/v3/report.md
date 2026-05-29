# coder v3 — plang-types — tester v2 fix

**Verdict:** the single tester v2 finding (non-distinguishing
`OverflowThrowSettingHonored.test.goal`) is fixed and mutation-confirmed.

## The finding

`decimal.MaxValue + decimal.MaxValue` overflows under every policy because
`DoOp`'s recovery `catch` clauses only widen `Promote && Int→Long` and
`Promote && Long→Decimal` — there is no `Decimal→Double` widening. So Throw
vs Promote didn't distinguish: the goal passed even when `Overflow=Throw`
was stripped.

## The fix

Switched both operands to `long.MaxValue` (9223372036854775807):

- **Throw path** (`OverflowThrowSettingHonored.test.goal`): Long+Long
  overflows; under Throw the exception propagates → `%err% is true`.
- **Promote path** (`OverflowPromoteWidens.test.goal`, new sibling):
  identical operands with `Overflow=Promote` widens Long→Decimal silently
  → `%err% is null` AND `%sum%` populated.

Together they pin that the Overflow axis distinguishes outcome — the
fingerprint the test name promised.

### Mutation test (confirms the distinction is load-bearing)

Temporarily removed `Overflow=Throw` from the Throw goal:
```
- math.add A=9223372036854775807 B=9223372036854775807, on error set %err% = true
- assert %err% is true
```
Rebuilt (`cache:false`), ran `--test`: **[Fail] OverflowThrowSettingHonored**
(under default Lenient/Promote, Long+Long widens silently to Decimal; `%err%`
is unset; the `is true` assertion fails). Reverted; test passes again.
Distinction confirmed.

## Verification

- **plang: 248 / 248 pass, 0 fail, 0 skip** (was 247; new sibling goal added).
- C# unchanged from v2 (3604 pass / 10 skip).
- Tree clean after revert; the mutation never reached `git add`.

## Files touched

- `Tests/Math/OverflowThrowSettingHonored.test.goal` — operands changed to
  long.MaxValue; comment rewritten.
- `Tests/Math/OverflowPromoteWidens.test.goal` (new) — Promote sibling that
  asserts no error AND sum populated.

No production C# changes.
