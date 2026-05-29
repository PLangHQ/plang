# tester v3 — plang-types — VERDICT: PASS

Reviewing coder v3, which fixed the single tester v2 finding (the non-distinguishing
`OverflowThrowSettingHonored.test.goal`).

## Test runs
- **plang: 248 / 248 pass, 0 fail, 0 skip** (was 247; +1 sibling goal).
- **C#: 3604 pass / 10 skip / 0 fail** (unchanged — no C# changes in v3).
- Tree clean; mutation reverted; no source committed.

## The fix and how I verified it

The v2 goal used `decimal.MaxValue + decimal.MaxValue`, which overflows under every
policy (no Decimal→wider promotion), so `Overflow=Throw` was not load-bearing. Coder v3
switched both operands to `long.MaxValue` — which Promote *can* widen (Long→Decimal) —
and added a sibling:

- `OverflowThrowSettingHonored.test.goal`: `Overflow=Throw` → MathOverflow → `%err% is true`.
- `OverflowPromoteWidens.test.goal` (new): `Overflow=Promote` → silent Long→Decimal →
  `%err% is null` AND `%sum% is not null`.

**Verification (mine, not the coder's report):**
1. `.pr` shape — both carry the correct `Overflow` param (`"Throw"` / `"Promote"`, type
   `overflowmode`); step text matches `math.add`. No builder false-green.
2. **Mutual-validation** — the two goals share identical operands and differ *only* in
   `Overflow`, asserting opposite outcomes. The default policy is one value, so if the
   `Overflow` parameter were ignored both goals would resolve identically and could not
   both pass. Both passing therefore *proves* the Overflow axis changes the outcome.
3. **Independent mutation** — I stripped `Overflow=Throw`, rebuilt `cache:false`, and ran:
   the goal went **[Fail]** (under the default Lenient/Promote, `long.MaxValue+long.MaxValue`
   widens silently to Decimal, `%err%` stays unset, `assert %err% is true` fails). Restored
   via `git checkout`. Confirms the parameter is load-bearing.

## Branch status

All v1 + v2 findings are now resolved and (where they mattered) mutation-verified:
- The runtime-DLL roundtrip and runtime-wins precedence (v1 #1/#2/#9) bite — confirmed by
  reversing `Registry.ResolveType` precedence in v2.
- Build-time kind-stamping is pinned at the right layer (`.pr`-shape C# tests).
- Deferrals are honest `[Skip]`s, not silent greens.
- NumberPolicy resolution is thoroughly covered (step/context/app-default/parent-climb),
  and now the step-level Overflow override is distinguished end-to-end in plang too.

No remaining false greens. **PASS.**
