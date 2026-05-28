# Test-designer plan — data-serialize-cleanup v1

## Source
Architect output in `.bot/data-serialize-cleanup/architect/`:
- `plan.md` — spine
- `plan/test-strategy.md` — four integration cuts
- `plan/test-coverage.md` — 60-row coverage matrix + 19-row failure matrix + new-surfaces inventory
- `stage-1-iserializer-data.md` … `stage-5-vocabulary-sweep.md` — deep dives

Reading the strategy/coverage docs top-to-bottom, then walking each matrix row → one test stub. Architect's matrix is the spec; I do not re-design it.

## Layer split (per architect)

- **C# TUnit** (`PLang.Tests/App/...`) — ISerializer surface, wire-converter sign-if-missing, Properties type, crypto canonicalization, Json composition extensions.
- **PLang `.goal`** (`Tests/`) — `%x.field%` vs `%x!key%` navigation, write-then-read round-trips through real channels, multi-step pipelines.
- **Integration cuts** (C# TUnit, channel/serializer boundary) — four cuts, one class each, exercising the full byte pipeline.

## File layout

C# tests:
- `PLang.Tests/App/Serialization/Stage1_ISerializerInputTests.cs` — Stage 1 rows 1.1–1.13
- `PLang.Tests/App/Serialization/Stage2_MergedPlangSerializerTests.cs` — Stage 2 rows 2.1–2.8, 2.11–2.14
- `PLang.Tests/App/Serialization/Stage2_SignInConverterTests.cs` — sign-if-missing + idempotency (subset of Stage 2)
- `PLang.Tests/App/Serialization/Stage2_CanonicalizationTests.cs` — crypto canonicalization fix
- `PLang.Tests/App/Serialization/Stage3_CompressFlattenedTests.cs` — Stage 3 rows 3.1–3.3, 3.5–3.8
- `PLang.Tests/App/Serialization/Stage4_PropertiesWireTests.cs` — Stage 4 rows 4.1–4.8, 4.15–4.18
- `PLang.Tests/App/Serialization/Stage5_VocabularySweepTests.cs` — Stage 5 rows 5.1–5.6
- `PLang.Tests/App/Serialization/FailureMatrixTests.cs` — failure-matrix rows not absorbed above
- `PLang.Tests/App/Serialization/IntegrationCuts/Cut1_PlainRoundTripTests.cs`
- `PLang.Tests/App/Serialization/IntegrationCuts/Cut2_SignThenCompressTests.cs`
- `PLang.Tests/App/Serialization/IntegrationCuts/Cut3_MultiActorForwardingTests.cs`
- `PLang.Tests/App/Serialization/IntegrationCuts/Cut4_PropertiesWireTests.cs`

PLang tests (live in `Tests/Serialization/`, one goal per file):
- `Tests/Serialization/TestCompressRoundTrip.goal` (3.9)
- `Tests/Serialization/TestPropertiesBangNavigation.goal` (4.10)
- `Tests/Serialization/TestValueVsPropertiesDisjoint.goal` (4.11)
- `Tests/Serialization/TestPropertiesBangChainedNavigation.goal` (4.12)
- `Tests/Serialization/TestNegationPrefixStillParses.goal` (4.13)
- `Tests/Serialization/TestVariableRendersValueOnly.goal` (4.14)
- `Tests/Serialization/TestDoubleBangIsParseError.goal` (failure matrix)
- `Tests/Serialization/TestNegationPrefixDistinctFromProperties.goal` (failure matrix)

## Batch breakdown (interactive — one batch at a time, await approval)

| Batch | Scope | Approx count |
|------:|-------|---:|
| 1 | Stage 1 — ISerializer input + OBP renames (C# unit) | 13 |
| 2 | Stage 2 — Merged plang serializer + sign-if-missing + canonicalization (C# unit) | 14 |
| 3 | Integration Cut 1 (plain round-trip) + Cut 3 (multi-actor forwarding) | ~8 |
| 4 | Stage 3 — Flat Compress / Decompress (C# + 1 .goal) + Integration Cut 2 | ~10 |
| 5 | Stage 4 — Properties C# behaviour (C# unit) | 12 |
| 6 | Stage 4 — Properties navigation (PLang .goal) + Integration Cut 4 | ~8 |
| 7 | Failure matrix rows not absorbed by Batches 1–6 | ~10 |
| 8 | Stage 5 — Vocabulary sweep + build/grep tests | ~6 |

Estimated total: ~80 test methods (incl. assertion-rich cut classes), ~8 PLang `.goal` files.

## Style rules (recap from character)

- C#: TUnit `[Test] async Task`, names = `MethodOrBehavior_Scenario_ExpectedResult`, body = `Assert.Fail("Not implemented");`.
- PLang: one goal per file, name starts with `Test`, comment on line 2 describes the spec, body = `- throw "not implemented"`.

## Process

1. Write this plan (this file).
2. Propose Batch 1 to Ingi, wait for approval.
3. Incorporate feedback. Repeat for Batches 2–8.
4. Once **all batches approved**, write the files (commit + push at end).
5. Update `summary.md` and `report.json`.

I'm blocked on nothing — proceeding to Batch 1 proposal.
