# coder v2 — plang-types — tester v1 fixes

**Verdict:** all 10 tester v1 findings addressed. C# 3604 pass / 10 skip, plang 247 pass.

## What was done

### Test-only fixes
- **#4** — Marked ~10 deferred no-op tests with `[Skip("deferred: ...")]`:
  `PlngSerializerCoverageTests` (PLNG003 gate), `IWriterFormatTests`
  (PlangWriter/TextWriter tokens), `MathHandlerDataReturnTests` (MathHelper
  absence), `ImageParseTests` (Http fetch), `PathSerializerMigrationTests`
  (legacy converter deletion). Honest skips replace silent green passes.
- **#6** — `LoadDll_PlangTypeWithoutAnyRenderer_FailsLoad_TypedError`:
  dropped the `if (RegisteredTypes.Contains(...))` guard; the typed-error
  assertion (`Success=false`, `ErrorKey="TypeLoadCoverage"`) is now
  unconditional, pinning the contract directly.
- **#9** — `LoadDll_ExistingName_RuntimeRendererWinsAtTypeSerializersLookup`:
  rewrote to use `path` (which has a generator-emitted serializer); captures
  baseline `Of("path","json")`, registers a runtime override, asserts the
  returned delegate is a different instance AND that the runtime branch fires
  the new closure ("runtime"). Real shadow test.
- **#2** — `LoadDll_AlreadyCompiledHandlerSlot_StillSeesBuiltInType_NoRewrite`:
  rewrote to reflect `math.Add.Run()`'s `Task<Data<number>>` return type
  before AND after overriding `"number"` in the runtime registry. The Run
  signature is baked at compile time, so the generic argument stays
  `app.types.number.@this` even when ResolveType("number") returns Uri.
  Tautology replaced with a load-bearing IL invariant.
- **#8** — `ReadPhotoStampsImage.test.goal`: added `assert %photo.Mime%
  equals "image/png"` and trimmed the comment to match. The runtime stamp
  is the assertion; build-time .pr shape is covered separately (#3 below).
- **#5** — `DurationRoundTrip.test.goal`: now parses BOTH `"PT5M"` (ISO-8601)
  and `"0.00:05:00"` (dot:colon), asserts both non-null, and asserts
  `%iso% equals %dotcolon%`. assert.equals's ToString comparison collapses
  both forms to canonical `"00:05:00"`, so a regression that mis-parses
  either form fails.

### Real coverage work
- **#1 (CRITICAL)** — Added `TypeProviderDllRoundtripTests.cs` (C#) that
  loads `TypeProvider.dll` from the test fixtures, asserts `Money` and
  `CustomInt` register, constructs instances via reflection, dispatches
  through `Renderers.Of(...)`, and asserts the captured wire output:
  - `Money(10m, "USD")` → `"USD 10"` via the loaded MoneyRenderer.
  - `CustomInt()` → `"CUSTOM-INT"` via the loaded CustomIntRenderer, AND
    `ResolveType("int")` returns `TypeProvider.CustomInt` (runtime wins
    the built-in `System.Int32` seeding). Baseline pre-load is captured.
  - The two Cut4 plang goals now carry honest comments noting that goal
    language has no surface for constructing arbitrary CLR instances; the
    real roundtrip lives in C#.

- **#3** — Added `BuilderKindStampingTests.cs` (C#) that loads existing
  pre-built `.pr` files and asserts param-level shape:
  - `readphotostampsimage.test.pr` step 0 action 0 Path param has
    `type=path`, `kind=file`.
  - `setdecimalliteralstampskind.test.pr` step 0 variable.set Value param
    has `type=object`, no `kind`. This pins the "polymorphic Data<object>
    slot gets no kind" rule that the misleading goal was named for.
  - `cut1_literalkindarithmeticoutput.test.pr` variable.set capturing
    `%b%` (math.add result) has no kind. Pins the polymorphic-result rule.
  - The three runtime-only goals (`SetDecimalLiteralStampsKind`,
    `Cut1_LiteralKindArithmeticOutput`, `PolymorphicMathAddHasNoKind`) had
    their comments trimmed to runtime-smoke language; the .pr-shape claims
    now live where they can be asserted.

- **#7** — `NumberPolicyResolutionTests.Resolve_SubContext_ClimbsParent_InheritsParentSetting`:
  replaced the no-op with a real parent/child context construction.
  Parent sets `number.overflow=Throw`; a `new actor.context.@this(app, parent:
  parent)` resolves the policy and inherits `Throw` via the
  `ConfigScope.Resolve` walk. Plus new plang goal
  `Tests/Math/OverflowThrowSettingHonored.test.goal`:
  `math.add A=79228162514264337593543950335 B=... Overflow=Throw, on error
  set %err% = true` — under default Lenient/Promote the add would silently
  promote, under Throw it surfaces a `MathOverflow` error. The step-level
  Overflow override path reaches the handler and changes outcome.

### Process (#10)
- Wrote `baseline-tests.md` capturing the pre-coder test state.

## Verification

Clean rebuild (PlangConsole + PLang.Tests), then:
- **C#: 3604 / 3614 pass, 0 fail, 10 skip** (the 10 are honest deferrals from #4).
- **plang: 247 / 247 pass, 0 fail, 0 skip** (was 246; new goal added).
- Targeted `--build={"files":[...],"cache":false}` rebuilt the 7 modified
  + 1 new goal; .pr step text and param shape match the action catalog.
- No source mutations remain; tree changes are the deliberate edits below.

## Files touched

### C# tests
- `PLang.Tests/App/Serialization/PlngSerializerCoverageTests.cs` — `[Skip]` ×4
- `PLang.Tests/App/Serialization/IWriterFormatTests.cs` — `[Skip]` ×2
- `PLang.Tests/App/Serialization/PathSerializerMigrationTests.cs` — `[Skip]` ×1
- `PLang.Tests/App/Types/MathHandlerDataReturnTests.cs` — `[Skip]` ×2
- `PLang.Tests/App/Types/ImageParseTests.cs` — `[Skip]` ×1
- `PLang.Tests/App/Types/NumberPolicyResolutionTests.cs` — SubContext real impl
- `PLang.Tests/App/Types/RuntimeTypeLoadingTests.cs` — three tests rewritten
  (FailsLoad guard, RuntimeRendererWins shadow, AlreadyCompiledHandlerSlot)

### New C# tests
- `PLang.Tests/App/Types/TypeProviderDllRoundtripTests.cs`
- `PLang.Tests/App/Types/BuilderKindStampingTests.cs`

### plang goals
- `Tests/Cleanups/DurationRoundTrip.test.goal` — both forms + equals
- `Tests/Types/ReadPhotoStampsImage.test.goal` — Mime assertion
- `Tests/Types/SetDecimalLiteralStampsKind.test.goal` — comment honesty
- `Tests/Types/PolymorphicMathAddHasNoKind.test.goal` — comment honesty
- `Tests/Cut1_LiteralKindArithmeticOutput.test.goal` — comment honesty
- `Tests/Cut4_RuntimeLoadAndRender/LoadDllRegistersType.test.goal` — comment honesty
- `Tests/Cut4_RuntimeLoadAndRender/LoadDllOverwritesBuiltIn.test.goal` — comment honesty
- `Tests/Math/OverflowThrowSettingHonored.test.goal` (new)

No production C# changes — tester v1 verdict was that the branch was honest
where it executed real assertions; the fixes are all in the tests +
test-only comments. Production code passed both codeanalyzer and tester
on the mechanism level.

## For next bot (tester / security / auditor)

- The two new C# test files (`TypeProviderDllRoundtripTests`,
  `BuilderKindStampingTests`) are the headline coverage adds — verify the
  assertions actually fail when the underlying behavior is mutated.
- The `[Skip]`-marked tests now show as skipped (was silently green); that
  is the intended state, not a regression.
- `LoadDllRegistersType` / `LoadDllOverwritesBuiltIn` are now smoke tests
  by design — the full DLL roundtrip lives in `TypeProviderDllRoundtripTests`.
