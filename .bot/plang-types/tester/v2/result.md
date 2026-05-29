# tester v2 — plang-types — VERDICT: FAIL (one new false green)

Reviewing coder v2, which addressed all 10 tester v1 findings. Review-driven code is
the highest false-green risk, so I mutation-verified the headline fixes rather than
trusting the green.

## Test runs (clean rebuild)
- **C#: 3604 pass / 10 skip / 0 fail** (3614 total). The 10 skips are the v1 deferred
  no-op tests, now honestly `[Skip]` (finding #4 fixed — confirmed in the run).
- **plang: 247 / 247 pass, 0 fail.**
- Tree clean; mutation reverted; no source committed.

## v1 findings — verification of coder v2 fixes

| # | Fix | Verified |
|---|-----|----------|
| 1 (CRITICAL) | `TypeProviderDllRoundtripTests.cs`: loads real TypeProvider.dll, constructs Money(10m,"USD")→"USD 10" and CustomInt→"CUSTOM-INT" through `Renderers.Of`, asserts wire output + runtime-wins ResolveType. | **REAL — mutation-confirmed** (see below). Source `Money.cs` renderer output matches assertions. |
| 2 | `AlreadyCompiledHandlerSlot`: reflects `math.Add.Run()`'s `Task<Data<number>>` slot before AND after overriding `"number"`→Uri; asserts the IL slot stays `number.@this`. | **REAL** — tautology replaced; mutation-confirmed it depends on the override taking effect. |
| 3 | `BuilderKindStampingTests.cs`: pins `path` param `type=path,kind=file` (positive stamp) and `variable.set` Value `type=object, kind=null` (polymorphic-no-kind) against committed `.pr`. Goal comments trimmed to honest runtime-smoke. | **REAL** — concrete `.pr`-shape assertions; this is the right layer for build-time shape. |
| 4 | ~10 deferred no-op tests marked `[Skip("deferred: …")]`. | **REAL** — 10 skips show in the run; no longer silent green. |
| 5 | `DurationRoundTrip`: sets both `PT5M` and `0.00:05:00`, asserts both non-null AND `%iso% equals %dotcolon%`. | **REAL** — equals across both forms catches a mis-parse of either. |
| 6 | `FailsLoad_TypedError`: dropped the `if` guard; `Success=false` + `ErrorKey="TypeLoadCoverage"` asserted unconditionally. | **REAL.** |
| 8 | `ReadPhotoStampsImage`: added `assert %photo.Mime% equals "image/png"`; comment trimmed. | **REAL.** |
| 9 | `RuntimeRendererWins`: captures baseline `Of("path","json")`, registers runtime override, asserts `after != baseline` AND the runtime closure fires. | **REAL** shadow test. |
| 10 | `baseline-tests.md` written. | **DONE.** |
| 7 (C# half) | `NumberPolicyResolutionTests`: step→context→app-default→record-default→**parent-climb** all exercised through `MathPolicy.Resolve`. Config.cs resolution now genuinely covered. | **REAL & thorough.** |

### Mutation test (confirms the headline coverage bites)
Temporarily reversed `Registry.ResolveType` runtime-first precedence (announced; reverted).
Result: `LoadDll_ExistingName_RuntimeWinsAtResolveType`, `AlreadyCompiledHandlerSlot`, and
`TypeProviderDllRoundtripTests.LoadDll_CustomInt_OverridesBuiltInName_RuntimeRendererWins`
all **FAILED** — the runtime-wins / DLL-roundtrip coverage is load-bearing, not decorative.
(Money roundtrip stayed green, correctly — it tests registration+render, not precedence.)

## NEW finding (v2) — FALSE GREEN

### `Tests/Math/OverflowThrowSettingHonored.test.goal` — `Overflow=Throw` is not load-bearing
```
- math.add A=79228162514264337593543950335 B=79228162514264337593543950335 Overflow=Throw, on error set %err% = true
- assert %err% is true
```
The comment claims "Lenient/Promote would promote silently" — **false.** For
`decimal.MaxValue + decimal.MaxValue` the promoted kind is `Decimal`, and
`DoOp`'s overflow-recovery `catch` clauses only widen `Promote && Int→Long` and
`Promote && Long→Decimal` (`this.Arithmetic.cs:85-97`). There is no `Decimal→Double`
widening, so decimal overflow propagates to `Wrap` and surfaces `MathOverflow` under
**every** policy — Throw, Promote, and Lenient alike.

**Empirically confirmed:** I built (`cache:false`) and ran the identical add WITHOUT
`Overflow=Throw`; `%err%` still became `true` and the goal passed. So this goal would
pass even if the `Overflow` parameter were ignored entirely. Its name
("…SettingHonored") promises it verifies the Throw setting changes outcome; it does not.

**Impact:** narrow. The *behavior* (step-level Overflow override reaching the handler,
policy resolution) IS covered by `NumberPolicyResolutionTests` in C#. This is a misnamed,
non-distinguishing goal — a false green for its stated guarantee, not an untested code path.

**Fix (≈5 min):** pick an input where Throw and Promote diverge, e.g.
`math.add A=2147483647 B=2147483647 Overflow=Throw` (int.MaxValue + int.MaxValue).
Under Promote that widens to Long (no error); under Throw it surfaces `MathOverflow`
(`%err%` true). Then the goal distinguishes the setting — ideally pair it with a sibling
that omits `Overflow=Throw` (or sets `Overflow=Promote`) and asserts `%err%` is null.

## Verdict
**FAIL** on the strict-red rule (one confirmed false green). This is not a regression in
quality — coder v2 is a large, genuine improvement and the critical items are now
mutation-verified honest. But a goal named for a guarantee it doesn't verify is exactly
what the tester exists to stop. One goal to fix.
