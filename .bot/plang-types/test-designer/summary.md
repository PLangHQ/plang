# test-designer — plang-types

**Version:** v1

## What this is

The `plang-types` branch lands the architectural shift where PLang's higher-level kinds (`number`, `image`, `code`, `path`, …) graduate from being labels in `app/formats` and CLR entries in `app/types`'s flat `Primitives` dict to first-class typed values that own their leaf behavior — value identity (`@this`), build-time `Build(value)→kind`, runtime `Resolve(...)`, and per-(type, format) serializer files. The runtime is a courier: it never dereferences `Data.Value` between actions; only leaf handlers (`math.add`) and leaf serializers reach in.

Seven stages: (1) registry+kind+`Build` machinery, (2) per-(type, format) dispatch table + `path` first-mover, (3) `number` value, (4) `number` arithmetic+policy, (5) `image` + `code`, (6) cleanups (`datetime`→DateTimeOffset, `duration`, `date`/`time`), (7) runtime DLL loading. Four integration cuts pin end-to-end: literal-kind→arithmetic→output; same image rendered to two channels; `%photo.Path.Exists%` composition navigation; runtime `- load X.dll`.

v1 produces the failing-test contract that pins the architecture before any production code lands.

## What was done

Wrote **24 C# test files** + **14 PLang `.goal` files** under `Tests/`. ~150 test stubs total; every C# body is `=> throw new global::System.NotImplementedException();` and every goal body is `- throw "not implemented"`. `dotnet build PLang.Tests` is green (0 errors, only pre-existing warnings).

### C# under `PLang.Tests/App/Types/` (Stages 1, 3–7)

- `RegistryFoldTests.cs` — Stage 1, registry fold no-regression
- `KindFieldTests.cs` — Stage 1, `.pr` `kind` as separate field
- `TypeBuildHookTests.cs` — Stage 1, type `Build(value)` discovery
- `TypedPropertyCatalogTests.cs` — Stage 1, catalog rendering
- `NumberValueTests.cs` / `NumberParseTests.cs` / `NumberBuildKindTests.cs` / `NumberEqualityTests.cs` / `NumberOperatorsTests.cs` — Stage 3
- `NumberArithmeticTests.cs` / `NumberDivideTests.cs` / `NumberPowerTests.cs` / `NumberPolicyResolutionTests.cs` / `MathHandlerDataReturnTests.cs` — Stage 4
- `ImageValueTests.cs` / `ImageParseTests.cs` / `ImageBuildKindTests.cs` / `CodeValueTests.cs` / `FileReadBuildTests.cs` — Stage 5
- `CleanupBindingsTests.cs` — Stage 6
- `RuntimeTypeLoadingTests.cs` — Stage 7

### C# under `PLang.Tests/App/Serialization/` (Stage 2 + per-type serializers)

- `TypeSerializersDispatchTests.cs` / `TypedValueNodeNormalizeTests.cs` / `IWriterFormatTests.cs` / `PathSerializerMigrationTests.cs` / `PlngSerializerCoverageTests.cs` / `NestedRegisteredTypeRoundTripTests.cs` — Stage 2
- `NumberSerializerTests.cs` / `ImageSerializerTests.cs` / `CodeSerializerTests.cs` — per-type serializer tests
- `IntegrationCuts/PlangTypesCut2_ImageTwoChannelsTests.cs` — Cut 2 harness
- `IntegrationCuts/PlangTypesCut3_CompositionNavigationTests.cs` — Cut 3 harness

### PLang goals under `Tests/`

- `Cut1_LiteralKindArithmeticOutput.test.goal` — Cut 1 spine
- `Types/SetDecimalLiteralStampsKind.test.goal` / `PolymorphicMathAddHasNoKind.test.goal` / `ReadPhotoStampsImage.test.goal` / `PhotoPathExistsNavigation.test.goal` / `PhotoPathExistsMissingFile.test.goal` / `Base64ImagePathNull.test.goal`
- `Math/DivideSevenByTwoIsThreeHalves.test.goal` / `IntDivSevenByTwoIsThree.test.goal` / `OverflowThrowSettingHonored.test.goal` / `SubGoalInheritsParentPolicy.test.goal` / `AddRoundTripDecimal.test.goal`
- `Cleanups/TimespanAliasStillResolves.test.goal` / `DurationRoundTrip.test.goal`
- `Cut4_RuntimeLoadAndRender/LoadDllRegistersType.test.goal` / `LoadDllOverwritesBuiltIn.test.goal`

## Decisions diverging from the architect's matrix

The test matrix is suggestion; the test surface is mine to own. Specific moves:

- **Grouped by surface, not by stage row.** A reader looking for "where is divide-by-zero pinned" finds `NumberDivideTests`, not three different "negative-path" files. The failure matrix is folded into the surface it tests (overflow in `NumberArithmetic`/`NumberDivide`; PLNG gate in `PlngSerializerCoverage`; load failure in `RuntimeTypeLoading`; navigation-on-null in Cut 3).
- **Cuts 1 and 4 ship as `.goal` files**; Cut 1 also has the supporting one-line `.pr` inspection goals (`SetDecimalLiteralStampsKind`, `PolymorphicMathAddHasNoKind`) split out so the spine cut stays readable. Cuts 2 and 3 ship as C# harnesses because two-channel switching + reflection-on-catalog are C#-test-shaped.
- **Numbered C# integration cuts use a `PlangTypes` prefix** (`PlangTypesCut2_…`) to avoid colliding with the existing `data-normalize` `Cut1/2/3/4` files in the same folder.
- **Stub body is `throw new global::System.NotImplementedException();`** (not `Assert.Fail`). TUnit's `Assert.Fail` returns void, which doesn't compose with `Task.FromResult` in the expression-body shape; `throw` works, fails the test cleanly, and compiles end-to-end.
- **Per architect's "impossible-by-design" list — NO tests** for: runtime-missing serializer for a built-in type (build gate forbids it); a `type:kind` string needing a runtime split (separate fields by construction); `number` carrying stale per-request Context.
- **No fixture DLL binary committed for Cut 4.** The runtime-loading goals reference a fixture DLL; the coder builds it during Stage 7 implementation (matches the `PLang.Tests/App/Fixtures/dlls/` pattern, where existing fixture DLLs are pre-built binaries).
- **No build-gate-only test for `MathHelper.ToDouble`/`PreserveType` deletion** beyond a reflection-style "not present in assembly" pair — matrix calls these "C# (build)" which a compile failure already covers.

## Code example

```csharp
// PLang.Tests/App/Types/NumberDivideTests.cs

// Divide leaves the integer track: 7/2 → 3.5 (lenient). Truncating is math.intdiv.
// Divide-by-zero surfaces as Data.Fail("DivideByZero") at the handler; internals throw.

[Test] public async Task Divide_SevenByTwo_ReturnsThreeAndHalf_NotThree()
    => throw new global::System.NotImplementedException();

[Test] public async Task IntDiv_SevenByTwo_ReturnsThree()
    => throw new global::System.NotImplementedException();

[Test] public async Task Divide_ByZero_Decimal_DataFailDivideByZero()
    => throw new global::System.NotImplementedException();
```

```plang
/ Tests/Cut1_LiteralKindArithmeticOutput.test.goal
Start
/ Integration cut 1 — the spine: literal kind → arithmetic → output.
/ Builds a .pr where %x% has type=number kind=int and %z% has type=number kind=decimal
/ (separate fields, never "number:decimal"). The math.add step result %b% has type=number
/ with NO kind (runtime-decided). Runtime executes; output is 4.5 (and 0.1+0.2 round-trips
/ as documented for decimal precision).
- throw "not implemented"
```

## What's next

`coder` runs next. Suggested order matches the architect's stage dependencies:

1. Stage 1 + Stage 2 together — the machinery (`path` as first mover means Stage 2 closes the loop without needing `number`).
2. Stage 3 (`number` value), then Stage 4 (arithmetic + policy + `math.*` retype + `intdiv`).
3. Stage 5 (`image` + `code` + `file.read.Build`).
4. Stage 6 (cleanups — parallelizable with 3–5).
5. Stage 7 (runtime loading — additive; needs a fixture DLL built under `PLang.Tests/App/Fixtures/dlls/`).
