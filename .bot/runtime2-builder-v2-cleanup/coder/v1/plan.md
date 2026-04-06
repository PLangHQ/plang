# Plan v1: Engine Folder Cleanup — Errors, Types, PrParser

Three tasks from the folder-by-folder Engine review.

## Task 1: DataSourceError → SettingsError

DataSource was renamed to Settings in this branch, but the error class wasn't updated.

**Changes:**
- Rename `Errors/DataSourceError.cs` → `Errors/SettingsError.cs`
- Rename class `DataSourceError` → `SettingsError`
- Update default key from `"DataSourceError"` to `"SettingsError"`
- Update all references (5 files):
  - `PLang/App/Engine/Settings/SqliteSettingsStore.cs` (8 refs)
  - `PLang.Tests/App/Modules/settings/SettingsDataTests.cs` (1 ref)
  - `PLang.Tests/App/Modules/identity/IdentityErrorPathTests.cs` (6 refs)
  - `PLang.Tests/App/Modules/datasource/DataSourceTests.cs` (11 refs — also rename test methods)

## Task 2: TypeMapping / Types/@this Consolidation

Both have nearly identical name↔type dictionaries and methods. Strategy: **Types/@this drops its duplicate dictionaries and delegates to TypeMapping for name↔type**. Types keeps its unique extension/kind/mime/compressibility logic.

**Why TypeMapping stays as the dictionary owner:**
- Source generator emits `TypeMapping.ConvertTo()` — can't access Engine instance
- v1 code (PlangModule/Program.cs) uses `TypeMapping.GetTypeName()`, `GetValidValues()`, `GetBuilderTypeNames()`
- The dictionaries are effectively compile-time constants

**Changes to Types/@this:**
- Remove `_nameToClr` and `_clrToName` dictionaries (100+ lines)
- `Clr()` → delegates to `TypeMapping.GetType()` (keep depth guard by adding it to TypeMapping)
- `Name()` → delegates to `TypeMapping.GetTypeName()`
- `Register()` → delegates to `TypeMapping.Register()`
- `BuilderNames()` → delegates to `TypeMapping.GetBuilderTypeNames()`
- `ComplexSchemas()` → delegates to `TypeMapping.GetComplexTypeSchemas()`
- `ValidValues()` → delegates to `TypeMapping.GetValidValues()`
- Keep all extension/kind/mime/compressibility code (unique to Types)

**Changes to TypeMapping:**
- Add depth guard (MaxGenericDepth=20) to `GetType()` — security improvement from Types
- No other changes needed

**Changes to Engine constructor:**
- Remove dual registration: `Types.Register(...)` AND `TypeMapping.Register(...)` → just `TypeMapping.Register(...)`

**Changes to Path.cs:**
- `MimeType` → use `_engine.Types.Mime(Extension)` for comprehensive MIME resolution (80+ entries vs TypeMapping's 20-entry switch)

## Task 3: Delete App PrParser (Dead Code)

`PLang/App/Engine/Utility/PrParser.cs` has zero references. The v1 PrParser at `PLang/Building/Parsers/PrParser.cs` already uses `IPLangFileSystem` correctly. The App version uses `System.IO` directly AND is unused. Delete it.

## Build verification

After all changes: `dotnet build PLang/PLang.csproj` and `dotnet build PLang.Tests/PLang.Tests.csproj` to verify compilation.
