# tester v3 — plang-types — reviewing coder v3 (tester v2 fix)

Single v2 finding: `OverflowThrowSettingHonored.test.goal` used `decimal.MaxValue`
(overflows under every policy → `Overflow=Throw` not load-bearing).

## Coder v3 fix
Switched operands to `long.MaxValue` (widens Long→Decimal under Promote) and added a
Promote sibling `OverflowPromoteWidens.test.goal`. The pair: same inputs, opposite
`Overflow`, opposite expected outcome.

## Verification
1. `.pr` carries `Overflow="Throw"` / `Overflow="Promote"` — no builder false-green. ✓
2. plang 248/248 pass; both goals pass. ✓
3. **Mutual-validation:** both goals share identical inputs and differ only in `Overflow`;
   one asserts error, the other no-error. If `Overflow` were ignored they'd resolve to the
   same policy and could not both pass. ✓
4. **Independent mutation:** stripped `Overflow=Throw`, rebuilt cache:false, ran → goal
   **[Fail]** (default policy widens silently, `%err%` unset). Restored via git checkout.
   The param is load-bearing. ✓

Verdict: PASS. False green fixed, mutation-confirmed. Tree clean, no source committed.
