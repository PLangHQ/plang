# coder v4 — plang-types — security v1 minor fixes

**Scope:** F1 (math.power CPU-DoS cap) and F2 (Loader sealed-name allowlist).
F3 (image byte-intake size cap) deferred — latent and architectural, fix is
"consolidate `ReadBytes(maxBytes?)` at the path-verb layer" which touches the
verb surface + `file.read` + `image.ResolveAsync` and extends a pre-existing
standing finding.

## F1 — `math.power` exponent cap

`PLang/app/types/number/this.Arithmetic.cs`:
- Added `public const long MaxPowerExponent = 64`.
- `DoPower` checks `|expL| > MaxPowerExponent` immediately after parsing the
  exponent (fractional branch already routes through `Math.Pow`, which is
  constant-time and not in scope of the cap).
- Throws a private `PowerExponentTooLargeException`; `Wrap` adds a catch
  branch that surfaces `Data.Fail("PowerExponentTooLarge")` via
  `ServiceError`, sitting alongside the existing `MathOverflow` /
  `DivideByZero` / `ArithmeticError` keys.

Tests added (`NumberPowerTests.cs`):
- `Power_ExponentAtCap_SmallBase_StillSucceeds` — boundary: `|exp|==64` allowed.
- `Power_ExponentJustOverCap_TypedFailure_PowerExponentTooLarge`.
- `Power_NegativeExponentBeyondCap_TypedFailure_PowerExponentTooLarge`.
- `Power_FractionalExponent_NotSubjectToCap` — fractional exponent of 100.5
  succeeds via `Math.Pow`; pins that the cap is integer-loop-only.

## F2 — Loader sealed-name allowlist

`PLang/app/types/Loader.cs`:
- Added `public static readonly IReadOnlySet<string> SealedNames` —
  `{ "identity", "signature", "signedoperation", "callback", "channel" }`
  (OrdinalIgnoreCase). Primitives like `int`/`string`/`path` deliberately
  stay overridable — their body is constrained by the type itself.
- Both passes (`[PlangType]` registration + `ITypeRenderer` registration)
  now refuse a sealed name and return a `Result(Success: false, ErrorKey:
  "TypeLoadCollision", ErrorMessage: …)` BEFORE touching the registry.
- The inferred-name branch (the `@this`-convention fallback) gets the same
  check so a fixture that omits the explicit attribute name can't slip past.

New fixture: `TestFixtures/IdentityShadow/` — a one-file DLL declaring
`[PlangType("identity")]`. Built and committed to
`PLang.Tests/App/Fixtures/dlls/IdentityShadow.dll`, mirroring the
TypeProvider fixture pattern.

Tests added (`RuntimeTypeLoadingTests.cs`):
- `LoadDll_AttemptToShadowSealedName_FailsWith_TypeLoadCollision` — loads
  the IdentityShadow DLL through `Loader.Register`; asserts
  `Success=false`, `ErrorKey="TypeLoadCollision"`, error message names
  `identity`.
- `SealedNames_AreCaseInsensitive_AndCoverCoreSigningTypes` — structural
  test: contains the five signing-load-bearing names (case-insensitive)
  and explicitly does NOT contain `int`/`string`/`path`.

## F3 — deferred

Latent (no shipping handler exposes `Data<image>` from a string parameter
today; `ResolveAsync.http` is the only network surface and is a factory
not a wire-binding). Fix would consolidate `ReadBytes(maxBytes?)` at the
path-verb layer with an `ImageTooLarge` typed failure — touches the verb
surface, `file.read`, and `image.ResolveAsync`. Better landed in a focused
"path-verb size guards" branch alongside the existing standing OpenAI
provider `ReadAllBytes` finding.

## Verification

- Clean build: `0 Error(s)` on PlangConsole + PLang.Tests.
- **C#: 3610 / 3620 pass, 0 fail, 10 skip** (was 3604 — five new tests, all
  green; the 10 deferred-skip tests unchanged).
- **plang: 248 / 248 pass.**
- IdentityShadow.dll is committed as a binary fixture (4.6 KB), matching
  the TypeProvider.dll committed-binary pattern.

## Files touched

### Production
- `PLang/app/types/number/this.Arithmetic.cs` — `MaxPowerExponent` const,
  cap check in `DoPower`, `PowerExponentTooLargeException` + `Wrap` branch.
- `PLang/app/types/Loader.cs` — `SealedNames` set, gate checks in both
  passes (PlangType registration + ITypeRenderer registration), inferred
  name branch.

### Tests
- `PLang.Tests/App/Types/NumberPowerTests.cs` — +4 tests.
- `PLang.Tests/App/Types/RuntimeTypeLoadingTests.cs` — +2 tests.

### Fixture
- `TestFixtures/IdentityShadow/IdentityShadow.csproj` (new)
- `TestFixtures/IdentityShadow/Shadow.cs` (new)
- `PLang.Tests/App/Fixtures/dlls/IdentityShadow.dll` (new, 4608 bytes)
