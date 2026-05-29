# tester v2 — plang-types — reviewing coder v2 (tester v1 fixes)

Coder v2 claims all 10 tester v1 findings addressed. Review-driven code is the
highest false-green risk, so I verify each fix is load-bearing — not a green that
re-states the bug.

## Method
1. Clean rebuild (stale-binary trap). C# 3604 pass / 10 skip / 0 fail; plang 247/247. ✓
2. Read the two headline new C# files (TypeProviderDllRoundtripTests, BuilderKindStampingTests)
   and the three rewritten RuntimeTypeLoadingTests (#2,#6,#9).
3. Read updated goals (#5 Duration, #7 Overflow, #8 ReadPhoto, Cut4 comments).
4. **Mutation-verify** the headline coverage actually fails when behavior breaks
   (coder explicitly requested this).
5. Scrutinize the NEW goal `OverflowThrowSettingHonored` — does `Overflow=Throw`
   actually change outcome, or would it pass under the default too?

## Findings
- 9/10 v1 fixes verified real. Runtime-wins precedence mutation-confirmed (reversing
  ResolveType precedence fails LoadDll_ExistingName_RuntimeWinsAtResolveType,
  AlreadyCompiledHandlerSlot, and TypeProviderDllRoundtrip.CustomInt).
- **NEW false green (#7 plang goal):** `OverflowThrowSettingHonored.test.goal` uses
  `decimal.MaxValue + decimal.MaxValue`, which overflows under EVERY policy (no
  Decimal→wider promotion path; DoOp's overflow catch only widens Int→Long / Long→Decimal).
  Empirically: the same add WITHOUT `Overflow=Throw` also sets `%err%=true` and passes.
  So `Overflow=Throw` is not load-bearing; the goal does not verify the setting is honored.
  Behavior itself IS covered in C# NumberPolicyResolutionTests (step/context/parent walk),
  so this is a misnamed/non-distinguishing goal, not an untested behavior.

Verdict: FAIL on strict-red (one confirmed false green) — narrow, single-goal fix.
