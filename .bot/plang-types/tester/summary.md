# tester — plang-types — summary

**Latest version:** v2 — **VERDICT: FAIL (one new false green; ~5-min fix)**

## What this is

The `plang-types` branch lands the unified `type + kind` value model: high-level PLang
type + optional `kind` refinement, per-(type,format) renderer dispatch, new
`number`/`image`/`code` value types, arithmetic policy on `number`, temporal cleanups,
and a runtime DLL-loading extension point (`code.load` → `[PlangType]` + `ITypeRenderer`).

## v1 (FAIL) — 10 findings

Green suite (3609 C#, 246 plang) but dishonest: Cut4 runtime-DLL goals were load-only
stubs; literal kind-stamping goals asserted runtime values while the `.pr` carried
`type:object`/no-kind; ~10 deferred tests passed as no-op `Assert.That(true)`; NumberPolicy
`Config.cs` was 0% covered. Full detail in `tester/v1/result.md`.

## v2 (FAIL) — coder addressed all 10; 9 verified real, 1 new false green

Clean rebuild: **C# 3604 pass / 10 skip / 0 fail, plang 247/247.**

**Verified real (mutation-tested where it mattered):**
- #1 CRITICAL — `TypeProviderDllRoundtripTests.cs` loads the real DLL and drives
  Money→"USD 10" / CustomInt→"CUSTOM-INT" through `Renderers.Of`. **Mutation:** reversing
  `Registry.ResolveType` runtime-first precedence fails the CustomInt + RuntimeWins +
  HandlerSlot tests → the coverage bites.
- #2/#6/#9 RuntimeTypeLoading rewrites, #3 BuilderKindStamping (`.pr`-shape), #4 ten
  deferrals now `[Skip]`, #5 Duration both-forms+equals, #8 Mime assertion, #7 C#
  policy-resolution (step/context/app-default/parent-climb) — all genuine.

**New false green (the FAIL):**
- `Tests/Math/OverflowThrowSettingHonored.test.goal` uses `decimal.MaxValue + decimal.MaxValue`,
  which overflows under **every** policy (no Decimal→wider promotion in `this.Arithmetic.cs`).
  So `Overflow=Throw` is not load-bearing. **Empirically confirmed:** the same add without
  `Overflow=Throw` also sets `%err%=true` and passes. The goal's name promises it verifies
  the Throw setting is honored; it doesn't. Behavior itself IS covered in C#, so it's a
  misnamed/non-distinguishing goal, not an untested path.

## Fix for coder

One goal. Change the input so Throw vs Promote diverge:
`math.add A=2147483647 B=2147483647 Overflow=Throw` (int.MaxValue + int.MaxValue) →
Promote widens to Long (no error), Throw → MathOverflow (`%err%` true). Ideally add a
sibling without the override asserting `%err%` is null.

## Files

- `.bot/plang-types/test-report.json` (shared) — v1_findings_resolution table + the v2 finding
- `tester/v1/`, `tester/v2/` — plan/result/verdict per version
