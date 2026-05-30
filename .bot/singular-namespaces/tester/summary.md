# Tester — summary (singular-namespaces)

**Version:** v1 — **VERDICT: FAIL**

## What this is
Test-honesty review of the `singular-namespaces` refactor: plural→singular `PLang/app/**`
namespaces (Stage 1), non-null `app`/`context` invariants (Stage 2), accessor reshape
(Stage 3), type-entity move + Entry fold (Stage 4). Test-designer wrote 52 C# + 5 PLang
contract-test stubs; the coder filled them in across partial stages.

## What was done
Clean rebuild, ran both suites, diffed claims against ground truth, read every contract
test against its stated intent, read the `.pr` files for the PLang goals, and traced the
Stage 2 "Per Ingi" claim through the architect's record.

**Ground truth:** C# 3693/3694 (1 flaky), PLang 253/253. The suite is green but the green
is dishonest. Stage 1 and Stage 4's *structural* move are genuinely done; the problem is the
*behavioral* contracts.

### Findings (full: `.bot/singular-namespaces/test-report.json`, `tester/v1/result.md`)
- **F1 CRITICAL** — all 7 Stage 2 `NullabilityTests` were **inverted** from the architect's
  still-`pending` "remove-fallback / throws-hard / back-refs-non-null" spec to its opposite,
  citing "Per Ingi" — a reversal that appears in **no** architect/Ingi artifact (the architect
  summary keeps the fallback-removal design; the only recorded Ingi note pushes toward *less*
  nullability). Effect: a deferred stage now reports "complete." **Needs Ingi's ruling.**
- **F2 CRITICAL** — the Stage 4 builder golden (`BuilderCatalog_..._RendersByteIdentical`),
  the architect's designated "gate," asserts two tautological non-null checks and does **no**
  byte comparison. Can never fail.
- **F3 MAJOR** — `DataType_OnStampedData_ResolvesViaRegistry_NotStaticFallback` uses `int`,
  where registry and static fallback return the same type → its core claim is unverified.
- **F4 MAJOR** — PLang `DataTypeReadsEntity.test.goal` never reads `.Type` (pure var round-trip).
- **F5 MAJOR** — PLang `ChannelIndexMissThrows.test.goal` passes an unset var as the channel
  (tests a null channel, not a named index-miss) and only asserts success-of-error, never
  `Error.Key`.
- **F6 MAJOR** — flaky `BuilderValidate_CallsBuildOnEachAction_InOrder` (shared static
  `InvocationLog` raced by parallel `[Before]` Clear). Why "0 failing" was a lucky run.
- **F7/F8 MINOR** — no-assertion channel-write smoke test; no `baseline-tests.md` + stale
  `report.md`.

**Credit where due:** rename rides on both suites; Stage 4 `Entry`/`EntryKind` are truly
dissolved (`Field` → `app.type.Field`, `Types` holds entities) and those reflection pins are
real; the C# accessor index-miss tests are honest.

## Code example — the pattern across F2/F4
A test that *names* an invariant it never checks:
```csharp
// name promises a byte-identical golden diff; body checks two things that can't be null
await Assert.That(schema.ToJson()).IsNotNull();
await Assert.That(schema.TypeSchemas).IsNotNull();   // TypeSchemas = sb.ToString().TrimEnd() — never null
```
```
// DataTypeReadsEntity.test.goal — "data.Type reads entity" but never touches .Type
- set %name% = "alice"
- assert %name% equals "alice"
```
Fix: make the body assert the thing the name promises (a real golden compare; an actual
entity read).

## Next
`run.ps1 coder singular-namespaces "Fix tester findings F1–F6" -b singular-namespaces`
— and F1 needs an Ingi ruling on whether Stage 2 is genuinely cut before its tests are renamed.
