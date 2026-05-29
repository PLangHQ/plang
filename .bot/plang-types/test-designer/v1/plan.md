# test-designer v1 — `plang-types` test contract

**Source design:** `.bot/plang-types/architect/plan.md` + `plan/test-strategy.md` + `plan/test-coverage.md` + 7 stage files. All read.

## Layer policy (lifted straight from architect, owned here)

- **C# TUnit** — pins type-internal behavior, build/dispatch machinery, no-regression on registry fold. Lives under `PLang.Tests/App/...`. Runs via `dotnet run --project PLang.Tests` (immune to the stale-binary trap).
- **PLang `.goal`** — pins developer-facing build-and-run surfaces. Lives under `Tests/` (uppercase). Each goal is a single-goal file (memory rule). Runs via `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` after a clean rebuild.
- **Integration cuts** — `.goal` (sometimes paired with a C# harness) that exercise build+runtime+channel together.

## Folder layout I'm targeting

C# (new + reused):

```
PLang.Tests/App/
  Types/                           — new; folder already exists for path/engine tests
    RegistryFoldTests.cs           — Stage 1
    KindFieldTests.cs              — Stage 1
    TypeBuildHookTests.cs          — Stage 1
    TypedPropertyCatalogTests.cs   — Stage 1
    NumberValueTests.cs            — Stage 3 (storage, From, casts)
    NumberParseTests.cs            — Stage 3
    NumberBuildKindTests.cs        — Stage 3
    NumberEqualityTests.cs         — Stage 3 (lenient, exact, NaN, hash)
    NumberOperatorsTests.cs        — Stage 3
    NumberArithmeticTests.cs       — Stage 4 (Add/Sub/Mul; promotion table)
    NumberDivideTests.cs           — Stage 4 (7/2→3.5; intdiv; ÷0)
    NumberPowerTests.cs            — Stage 4
    NumberPolicyResolutionTests.cs — Stage 4 (app.config walk)
    MathHandlerDataReturnTests.cs  — Stage 4 (returns Data, never throws)
    ImageValueTests.cs             — Stage 5 (Bytes/Mime/Path nullable, truthiness)
    ImageParseTests.cs             — Stage 5 (path/data-url/base64)
    ImageBuildKindTests.cs         — Stage 5 (.jpg→jpg)
    CodeValueTests.cs              — Stage 5
    FileReadBuildTests.cs          — Stage 5 (extension→high-level type)
    CleanupBindingsTests.cs        — Stage 6 (datetime→DateTimeOffset; date/time/duration; timespan alias; no DateTime)
    RuntimeTypeLoadingTests.cs     — Stage 7 (RegisterRuntime overwrite precedence, missing-renderer failure)
  Serialization/                   — existing; add:
    TypeSerializersDispatchTests.cs — Stage 2 (lookup specific?? "*")
    TypedValueNodeNormalizeTests.cs — Stage 2 (tag-hook, registered vs reflection)
    IWriterFormatTests.cs           — Stage 2 (Format token per writer)
    PathSerializerMigrationTests.cs — Stage 2 (path renders identically before/after)
    PlngSerializerCoverageTests.cs  — Stage 2 (generator gate)
    NumberSerializerTests.cs        — Stage 3 (Default.cs per Kind)
    ImageSerializerTests.cs         — Stage 5 (text→placeholder, Default→base64, protobuf stub)
    CodeSerializerTests.cs          — Stage 5
    NestedRegisteredTypeRoundTripTests.cs — Stage 2
    IntegrationCuts/                — extends existing folder
      Cut2_ImageTwoChannelsTests.cs — image, two writers, two wire shapes
      Cut3_CompositionNavigationTests.cs — %photo.Path.Exists% (the C# half)
  Generators/                      — existing? if not, create
    PlngSerializerCoverageGenTests.cs — Stage 2 generator gate (negative)
```

PLang goals (new):

```
Tests/
  Types/
    SetDecimalLiteralStampsKind.test.goal           — Cut 1 piece (build-vs-runtime row)
    PolymorphicMathAddHasNoKind.test.goal           — build-vs-runtime row
    ReadPhotoStampsImage.test.goal                  — types.md row + Cut 2 build half
    PhotoPathExistsNavigation.test.goal             — types.md / Cut 3 (present file)
    PhotoPathExistsMissingFile.test.goal            — Cut 3 (missing file)
    Base64ImagePathNull.test.goal                   — Cut 3 (no source, Path null)
  Math/
    DivideSevenByTwoIsThreeHalves.test.goal         — divide footgun
    IntDivSevenByTwoIsThree.test.goal               — math.intdiv
    OverflowThrowSettingHonored.test.goal           — policy via set
    SubGoalInheritsParentPolicy.test.goal           — context walk
    AddRoundTripDecimal.test.goal                   — Cut 1 (0.1+0.2 documented)
  Cleanups/
    TimespanAliasStillResolves.test.goal            — cleanup row
    DurationRoundTrip.test.goal                     — cleanup row
  Cut1_LiteralKindArithmeticOutput.test.goal        — Cut 1 spine goal (set 1 + set 3.5 + add + write out)
  Cut4_RuntimeLoadAndRender/                        — Cut 4
    LoadDllRegistersType.test.goal
    LoadDllOverwritesBuiltIn.test.goal
    (paired test DLL fixture under PLang.Tests/App/Types/Fixtures/)
```

Total estimate: ~30 C# files (~110-130 tests), ~15 PLang goals, 1 test fixture DLL.

## Batches

| # | Surface | Files | Approx tests |
|---|---|---|---|
| 1 | Stage 1 — registry fold, `kind` field, type `Build()`, typed-property catalog | RegistryFoldTests, KindFieldTests, TypeBuildHookTests, TypedPropertyCatalogTests | ~12 C# |
| 2 | Stage 2 — `TypeSerializers` dispatch, `TypedValueNode`, `IWriter.Format`, `path` first-mover, PLNG coverage gate | TypeSerializersDispatchTests, TypedValueNodeNormalizeTests, IWriterFormatTests, PathSerializerMigrationTests, PlngSerializerCoverageTests, NestedRegisteredTypeRoundTripTests | ~12 C# |
| 3 | Stage 3 — `number` value (storage, parse, Build, operators, equality, truthiness, serializer) | NumberValueTests, NumberParseTests, NumberBuildKindTests, NumberEqualityTests, NumberOperatorsTests, NumberSerializerTests | ~15 C# |
| 4 | Stage 4 — `number` arithmetic + policy + math.* retype + intdiv | NumberArithmeticTests, NumberDivideTests, NumberPowerTests, NumberPolicyResolutionTests, MathHandlerDataReturnTests + 5 .goal files (Math/) | ~14 C# + 5 goals |
| 5 | Stage 5 — `image` + `code` + file.read.Build | ImageValueTests, ImageParseTests, ImageBuildKindTests, CodeValueTests, FileReadBuildTests, ImageSerializerTests, CodeSerializerTests | ~13 C# |
| 6 | Stage 6 — primitive cleanups (datetime/date/time/duration + alias + no-DateTime) | CleanupBindingsTests + 2 .goal files | ~8 C# + 2 goals |
| 7 | Stage 7 — runtime type-loading + Cut 4 | RuntimeTypeLoadingTests + Cut 4 .goal files + fixture DLL surface | ~6 C# + 2 goals + 1 fixture |
| 8 | Cuts 1–3 — the spine integration goals + C# harness | Cut1 goal, Cut2_ImageTwoChannelsTests, Cut3_CompositionNavigationTests + 4 supporting goals (set decimal stamp, math add no-kind, read photo, navigation present/missing/base64) | ~6 C# + 6 goals |

After approval flows, write the files; commit + push. Per architect "impossible-by-design" list, I will NOT write tests for: runtime-missing serializer for built-in type, `type:kind` string-split, `number` carrying stale Context.

## Open notes / divergences from the architect matrix I'll own

- **Group by surface, not by coverage-matrix table.** A test reader hunting "where is divide-by-zero pinned" finds it in `NumberDivideTests`, not in three different "negative-path" files. The failure matrix is folded into the surface it tests (overflow lives in NumberArithmetic/NumberDivide; PLNG gate lives in PlngSerializerCoverage; load failure lives in RuntimeTypeLoading; navigation-on-null in Cut3).
- **One file per concept, not per stage.** `NumberValueTests` and `NumberArithmeticTests` are different files because they cover different surfaces, even though both are "number." Reader-facing names.
- **Cuts 1 and 4 ship as integration `.goal` files** (Cut 1 also has C# inspection of the `.pr` via `BuilderSanity`-style fixture if precedent exists; otherwise the goal asserts via PLang `assert.*`). Cut 2/3 ship as C# harnesses driving the channels because two-channel switching is C#-test-shaped.
- **The runtime fixture DLL for Cut 4** is a tiny project under `PLang.Tests/App/Types/Fixtures/TypeLoadingFixture/` (or wherever the existing Generators/fixture pattern lives) — I'll match that pattern when I get there.
- **No tests for `MathHelper.ToDouble`/`PreserveType` deletion as a build assertion** beyond a single grep-style test; the build itself fails if refs remain (matrix says "C# build" for that row, which is accurate).

## Status

- v1 plan written. Awaiting approval on Batch 1 before writing test signatures.
