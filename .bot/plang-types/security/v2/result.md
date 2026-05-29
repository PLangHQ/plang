# security v2 — plang-types — VERDICT: PASS

v1 F1 + F2 closed and mutation-confirmed. One new Low finding (F4) on the
math handler family from the `mathhelper-deletion` merge. F3 (image byte
cap) remains a deliberate deferral.

Full details: `.bot/plang-types/security-report.json`.

## v1 fixes verified

### F1 — math.power exponent cap — CLOSED

`PLang/app/types/number/this.Arithmetic.cs`:

- `MaxPowerExponent = 64` const (line 37).
- `EnsureExponentInRange(expL)` called at the top of each CPU-loop branch:
  the negative-exponent decimal branch (line 218), the integer-base
  integer-exponent branch (line 230), and the decimal-base integer-exponent
  branch (line 251).
- Math.Pow branches (fractional exponent line 206; double-base lines 223,
  257) legitimately skip — Math.Pow is constant time and not subject to
  the cap.
- `PowerExponentTooLargeException` surfaces as
  `Data.Fail("PowerExponentTooLarge", 400)` via the existing `Wrap` catch.

Tester v5 mutation-confirmed each CPU-loop site is covered and each
Math.Pow site is uncovered (skip pin). Solid.

### F2 — Loader sealed-names allowlist — CLOSED

`PLang/app/types/Loader.cs`:

- `SealedNames = { "identity", "signature", "signedoperation", "callback",
  "channel" }` (OrdinalIgnoreCase).
- Gate at **all three** sites: explicit `[PlangType(name)]` (line 88),
  inferred `@this`-convention name (line 101), and ITypeRenderer.TypeName
  (line 122).
- Each site returns
  `Result(Success=false, ErrorKey="TypeLoadCollision", ErrorMessage=…)`
  BEFORE touching the registry.

Tester v5 mutation-confirmed each gate site fails independently when its
check is neutralised — three real fixture DLLs
(`IdentityShadow`, `SignatureRendererShadow`, `CallbackInferredShadow`)
exercise each shape.

## New finding (Low × 1)

### F4 — `math.round` Decimals out-of-range escapes `Wrap`

`PLang/app/types/number/this.Unary.cs:84-90` calls `System.Math.Round(decimal,
decimals, …)` and `System.Math.Round(double, decimals, …)`. Framework rule:
`decimals ∈ [0, 28]` for decimal, `[0, 15]` for double. Out of range throws
`ArgumentOutOfRangeException`.

`number.Wrap` (`this.Arithmetic.cs:47-65`) catches `DivideByZeroException`,
`OverflowException`, `ArithmeticException`, `PowerExponentTooLargeException`.
`ArgumentOutOfRangeException : ArgumentException` — not in the catch list.

The handler `math.round Decimals=%var%` accepts any `int`. So
`Decimals=999` or `Decimals=-1` (both easily reachable from LLM-produced
.pr literals or untrusted `%var%` content) throws uncaught from the action,
degrading the typed-failure contract the rest of the math family observes
(MathOverflow / DivideByZero / ArithmeticError / PowerExponentTooLarge).

**Reachability today:** any goal `- round %x% Decimals=%user_decimals%`
where `%user_decimals%` flows from external data (LLM JSON, HTTP form,
file content). Not a crash escalation — the runtime catches uncaught
handler exceptions higher up — but `on error set %err% = …` clauses tied
to the math-family error keys will not bind, and the error message shape
diverges from the documented Wrap envelope.

**Fix posture:** clamp at `DoRound` entry — either validate decimals into
the framework-legal range and surface `Data.Fail("ArithmeticError")` for
out-of-range, or widen `Wrap` to catch `ArgumentException` with a typed
key like `"InvalidArgument"`. The clamp is smaller and preserves the
existing key vocabulary.

## Standing — F3 (image byte cap) still deferred

Coder v4 reasoned the deferral correctly: no shipping handler binds
`Data<image>` from a string parameter today; the `image.ResolveAsync`
HTTP-URL branch is a latent factory. Real fix consolidates
`ReadBytes(maxBytes?)` at the path-verb layer alongside the existing
standing OpenAiProvider `ReadAllBytes` finding — better landed in a
focused branch.

## What I checked and ruled out

- **mathhelper-deletion merge** — every retyped handler (`abs`, `floor`,
  `ceiling`, `sqrt`, `min`, `max`) routes through `number.* → Wrap`.
  Reviewed each: only `round` exposes a bounded framework parameter
  that escapes Wrap. `sqrt` correctly maps negative input to
  `ArithmeticError` via `Wrap`. `abs(long.MinValue)` throws
  OverflowException → typed.
- **SealedNames partial-state on bail** — `Loader.Register` exits early
  on first sealed-name collision; PlangType registrations from earlier
  classes in the same DLL stay in `_runtimeNameToType`. Sub-Low /
  informational: the user is past the Execute trust boundary, so a hostile
  DLL's non-sealed registrations would have succeeded anyway under the
  by-design accepted-risk model. Worth noting; not flagging as a finding.
- **Sealed-names Unicode bypass** — checked OrdinalIgnoreCase semantics
  against Turkish-I and similar Unicode tricks. The sealed gate uses the
  same comparer as the registry (`StringComparer.OrdinalIgnoreCase`), so
  any name that would shadow at lookup-time also matches `SealedNames`.
  No bypass.
- **Wire / Normalize / json.Writer** — unchanged since security v1.
  Builder-ergonomics depth-counter intact (`Wire.cs:113`); fail-closed
  on unknown leaf types in `json.Writer.Value`; `RendererLookupMissed`
  thrown when a tagged value lacks a renderer.
- **semgrep architectural baseline** — 15 findings, unchanged from
  runtime2 baseline.

## Verdict

**PASS.** v1 findings closed; one new Low (F4) on math.round; F3 remains
a tracked deferral. No critical/high. Branch is safe to merge with F3 and
F4 tracked.

## Next bot

```
VERDICT: PASS
Next: run.ps1 auditor plang-types "Review the code on branch plang-types" -b plang-types
```
