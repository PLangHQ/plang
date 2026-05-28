# Test-designer plan — data-normalize v1

## Source
Architect output in `.bot/data-normalize/architect/`:
- `plan.md` — spine, stage index
- `plan/test-strategy.md` — 3 integration cuts + layer mapping
- `plan/test-coverage.md` — ~50-row coverage matrix + failure matrix + new-surfaces inventory
- `plan/wire-out-attributes.md` — 13-type `[Out]` inventory
- `stage-1-out-discipline.md`, `stage-2-normalize-jsonwriter.md`, `stage-3-as-tree-walker.md`

## Approach

Going through the strategy and coverage docs, then folding rows into concept-grouped files (not stage-grouped). The architect explicitly invited reshaping; I'm taking it for readability. Coverage matrix is the input; my judgment selects what's worth a separate test vs. parameterized vs. dropped as a build invariant.

## Layer split (per architect)

- **C# TUnit** (`PLang.Tests/App/...`) — `[Out]` attribute inventory, `Data.Normalize()` mechanics, `IWriter` contract, `JsonWriter` byte shape, `As<T>` reconstruction, cycle/depth detection, debug-mode toggle, failure matrix.
- **PLang `.goal`** (`Tests/`) — developer-facing surfaces: path round-trip through channel, sensitive doesn't leak, masked on wire, sign→serialize→verify, debug-mode payload.
- **Integration cuts** (C# TUnit, channel/serializer boundary) — three cuts per `test-strategy.md`, one class each.

## File layout

### C# tests

```
PLang.Tests/App/Serialization/
  OutAttributeInventoryTests.cs          — Stage 1, [Out] per 13 types
  RawSignatureDeletionTests.cs           — Stage 1, RawSignature gone
  MaskedAttributeTests.cs                — Stage 1, [Masked] exists
  IWriterContractTests.cs                — Stage 2, IWriter surface + JsonWriter bytes
  JsonWriterDomainShapeTests.cs          — Stage 2, wire shape for domain types
  DebugModeBypassTests.cs                — Stage 2, View.Out vs View.Debug
  FailureMatrixNormalizeTests.cs         — failure matrix (typed errors)
  IntegrationCuts/
    Cut1_JsonRoundTripTests.cs
    Cut2_DebugModeTests.cs
    Cut3_SignWireVerifyTests.cs

PLang.Tests/App/Data/
  NormalizeTreeShapeTests.cs             — Stage 2, tree shape per input
  NormalizeFilterTests.cs                — Stage 2, [Out]/[Sensitive]/[Masked] filtering
  NormalizeCycleAndDepthTests.cs         — Stage 2, cycle + depth + getter-throws
  AsTreeWalkerTests.cs                   — Stage 3, As<T> reconstruction
  AsReconstructionHookTests.cs           — Stage 3, per-type hook (path.Resolve)
```

### PLang goal tests

```
Tests/Serialization/
  PathRoundTripAfterNormalize.test.goal
  SensitivePropertyDoesNotLeak.test.goal
  MaskedSettingOnWire.test.goal
  DebugModePayloadIncludesNonOut.test.goal

Tests/Signing/
  RoundTripAfterRawSignatureDeletion.test.goal
```

## Batch breakdown

| Batch | Scope | Files | ~tests |
|------:|-------|-------|---:|
| 1 | Stage 1 — Out / RawSignature / Masked | 3 | ~20 |
| 2 | Stage 2 Normalize core — tree shape / filter / cycle+depth | 3 | ~21 |
| 3 | Stage 2 wire emission — IWriter / JsonWriter / Debug | 3 | ~19 |
| 4 | Stage 3 As&lt;T&gt; — tree walker / hooks | 2 | ~15 |
| 5 | Integration cuts + failure matrix + 5 goal files | 9 | ~25 |

User said "go for all batches" — running straight through without per-batch approval.

## Decisions diverging from the matrix

1. **Grouped by concept, not stage.** Future reader hunting "where is cycle detection tested" shouldn't have to know that's Stage 2 row 13.
2. **Pruned redundancies.** Matrix rows like "Normalize on List<int>" and "Normalize on Dict<string,X>" collapse into one parameterized test in `NormalizeTreeShapeTests`. The coverage row remains covered, just not as a separate method stub.
3. **Reflection over compile-fail.** `Data.RawSignature` deletion is compile-time guaranteed; expressed as `typeof(Data).GetProperty(...)` returning null.
4. **Failure matrix gets its own file** (`FailureMatrixNormalizeTests.cs`) — happy-path Normalize tests stay focused.
5. **No new test for "existing tests pass."** That's a CI invariant, not a test.
6. **One `.goal` per file** — the small-rule from memory says multi-goal files get overwritten by the builder.
7. **`PLang.Tests/App/Data/` folder** (not `DataTests` already exists) — there's already a `DataTests/` folder; I'll use it to avoid namespace fragmentation. **(Update during writing if alias clash bites.)**
