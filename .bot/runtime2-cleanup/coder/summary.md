# coder — runtime2-cleanup

## Version
v27 — Stage 27 (Tier 5 closer): Utils/ empty-out — TypeConverter + Json disperse.

## What this is
Final Tier 5 stage. Two pieces:

1. **TypeConverter → Types/Conversion.cs partial.** Mechanical (Types is already a partial from stage 26). The 4 public methods (ConvertTo<T>, ConvertTo, Populate, TryConvertTo) and 4 private helpers (FormatTypeMismatch, TypeMismatchHint, FormatValuePreview, GetListElementType) move into the new partial. Public methods stay `public static` (pure logic, called from many static contexts); private helpers stay `private static`. The Conversion partial holds its own `_caseInsensitiveRead` for GoalCall + complex-type deserialization.
2. **Utils/Json.cs disperses.** Five JsonSerializerOptions bags + 2 helpers + 1 extension + 2 internal converters. Each piece moves to where its consumer lives.

After: `App/Utils/` contains exactly 4 files — `CommandLineParser.cs`, `PathExtension.cs`, `RegisterStartupParameters.cs`, `StringDistance.cs`. The destination tree from `plan/post-cleanup-tree.md` matches reality. Tier 5 closes; runtime2-cleanup branch ready for review/merge to runtime2.

## What was done

### New shape
- `App/Types/Conversion.cs` — NEW partial. Absorbs TypeConverter body.
- `App/Data/JsonString.cs` — NEW. Holds `JsonString.ToJson()` extension + `FixJsonStringValues` + `EmptyStringToNullEnumConverterFactory` + `EmptyStringToNullEnumConverter<T>`.
- `App/Diagnostics/this.cs` — NEW static class. Holds `Format(value)` + `Options` (the masked JsonSerializerOptions).
- `App/Builder/this.cs` — gains `internal static readonly PrWrite` + private static `StoreOnlyModifier`.
- `App/this.cs` — gains `internal static readonly CamelCaseIndented`.
- `App/Data/this.Compare.cs` — gains `private static readonly _camelCaseIndented`.
- `App/Data/this.cs` — gains `private static readonly _snapshotClone`.
- `App/Variables/this.cs` — gains `private static readonly _snapshotClone`.
- `App/modules/http/code/Default.cs` — gains `private readonly _caseInsensitiveRead` instance field; `ApplySignature` converted from static to instance.

### Files deleted
- `App/Utils/TypeConverter.cs`
- `App/Utils/Json.cs`

### Caller sweeps
- `App/Errors/AssertionError.cs` + `App/modules/assert/code/Default.cs` + `App/modules/test/report.cs` — `FormatForDiagnostic` calls now route through `global::App.Diagnostics.@this.Format`/`.Options`.
- `App/modules/builder/code/Default.cs` — `Json.PrWrite` → `global::App.Builder.@this.PrWrite`.
- All Json.X / TypeConverter call sites in production updated to per-consumer/dispersed homes.

### Test compatibility
`PLang.Tests/Support/TypeMappingTestFacade.cs` extended with two more facades:
- `App.Utils.TypeConverter` static class — routes to `global::App.Types.@this.X`.
- `App.Utils.Json` static class — exposes `CaseInsensitiveRead` (locally constructed), `CamelCaseIndented` (→ `App.@this.CamelCaseIndented`), `PrWrite` (→ `Builder.@this.PrWrite`), `DiagnosticOutput` (→ `Diagnostics.@this.Options`).

~12 test sites unchanged. Their `using App.Utils;` keeps resolving via the facade.

## Brief deviations

- **DiagnosticOutput home**: brief leaned new `App/Diagnostics/this.cs` sub-`@this` mounted as `app.Diagnostics`. **Went with static class** instead — all three callers (AssertionError.FormatValue, FormatValue helpers in assert + test handlers) are in static contexts with no App in scope. Per the brief's own escape ("Architect's leans toward instance but flags this as a coder/Ingi judgment call during implementation"), static is the pragmatic answer. The single Options bag is held statically — Rule C exception class for stateless config.
- **CamelCaseIndented + SnapshotClone homes**: per-consumer `static readonly` rather than instance fields, matching the brief's noted exception for frequently-allocated types (Data) and stateless config bags.
- **EmptyStringToNullEnum converters**: bundled into `App/Data/JsonString.cs` rather than a separate file under http/. They're used by both `http/Default._caseInsensitiveRead` and `Types/Conversion._caseInsensitiveRead`, so a shared home is correct.

## Stage closure
- C# tests green: 2752/2752
- PLang tests green: 199/199
- `App/Utils/` end state: exactly 4 files (CommandLineParser, PathExtension, RegisterStartupParameters, StringDistance) ✓
- Zero `App.Utils.TypeConverter` / `App.Utils.Json` / `JsonExtensions` references in production
- Behaviour change: none. Pure shape change.

**Tier 5 closes here.** runtime2-cleanup branch is ready for review and merge to runtime2.
