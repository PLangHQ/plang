# v27 — Stage 27: utils-empty-out (Tier 5 closer)

Final Tier 5 stage. Two pieces.

## Part 1: TypeConverter → Types/Conversion.cs partial

Mechanical move. The 4 public methods (ConvertTo<T>, ConvertTo, Populate, TryConvertTo) and 4 private helpers (FormatTypeMismatch, TypeMismatchHint, FormatValuePreview, GetListElementType) move into a new `Types/Conversion.cs` partial of `Types.@this`. Public methods stay `public static` — pure logic with no Types-instance state, called from many static contexts. Private helpers stay `private static`. Conversion partial holds its own `private static readonly _caseInsensitiveRead` for the GoalCall + complex-type deserialization paths (was `Json.CaseInsensitiveRead`). The 4 forwarder declarations on `Types/this.cs` (added in stage 26) are removed — bodies are now in the partial.

## Part 2: Json.cs disperses

Each piece moves to its consumer. Per-consumer copies for the simple bags; new sub-class for the genuinely-shared helper.

| Piece | New home |
|-------|----------|
| `CaseInsensitiveRead` | `http/code/Default.cs` instance field `_caseInsensitiveRead`. ApplySignature converted from static to instance to use it. |
| `CamelCaseIndented` | `App.@this` `internal static readonly` (App.Save consumer + tests); `Data.@this.Compare` partial `private static readonly` (Compare consumer). |
| `SnapshotClone` | Per-consumer `private static readonly`: `Data.@this` and `Variables.@this`. |
| `DiagnosticOutput` + `FormatForDiagnostic` | New `App/Diagnostics/this.cs` static class. Three callers (AssertionError, modules/assert, modules/test/report) navigate via `global::App.Diagnostics.@this.Format(value)`. The brief leaned instance + sub-`@this`; static is the pragmatic choice because all three callers are in static contexts (AssertionError.FormatValue, FormatValue helpers in assert + test handlers — no App in scope). The single Options bag is held statically (Rule C exception class for stateless config). |
| `PrWrite` + `StoreOnlyModifier` | `Builder.@this` `internal static readonly` + private static modifier method. One production caller + one test caller. |
| `JsonExtensions.ToJson()` + `FixJsonStringValues` | `App/Data/JsonString.cs` static class. Pure parsing utility — keeps the extension shape. |
| `EmptyStringToNullEnumConverterFactory` + `EmptyStringToNullEnumConverter<T>` | `App/Data/JsonString.cs` (sit alongside JsonString since both are JSON-parsing infrastructure). Reachable from both http/Default and Types/Conversion via their case-insensitive-read options. |

## Test compatibility

`PLang.Tests/Support/TypeMappingTestFacade.cs` extended with `App.Utils.TypeConverter` and `App.Utils.Json` static facades — routes the legacy test ergonomics to the new homes. ~12 test sites unchanged.

## Files deleted
- `App/Utils/TypeConverter.cs`
- `App/Utils/Json.cs`

## Definition of done
- `dotnet build PlangConsole` clean
- C# 2752/2752 + PLang 199/199 green
- `App/Utils/` contains exactly 4 files: CommandLineParser, PathExtension, RegisterStartupParameters, StringDistance
- Zero `App.Utils.TypeConverter\|App.Utils.Json\|JsonExtensions` references in PLang/
