# v1 Summary: Engine Folder Cleanup — Errors, Types, PrParser

## What this is

Three cleanup tasks from the folder-by-folder Engine review: renaming a stale error class, eliminating a major code duplication between TypeMapping and Types, and removing dead code.

## What was done

### 1. DataSourceError → SettingsError
DataSource was renamed to Settings earlier in this branch, but the error class wasn't updated. Renamed `DataSourceError` → `SettingsError` across all files:
- `PLang/Runtime2/Engine/Errors/DataSourceError.cs` → `SettingsError.cs` (class, default keys)
- `PLang/Runtime2/Engine/Settings/SqliteSettingsStore.cs` (8 references)
- `PLang.Tests/Runtime2/Modules/settings/SettingsDataTests.cs` (1 ref)
- `PLang.Tests/Runtime2/Modules/identity/IdentityErrorPathTests.cs` (6 refs)
- `PLang.Tests/Runtime2/Modules/datasource/DataSourceTests.cs` (11 refs)

### 2. TypeMapping / Types/@this Consolidation
Both had nearly identical name↔type dictionaries (~100 lines each) and 6 duplicate methods. Eliminated the duplication:

- **Types/@this** (instance on Engine): removed duplicate `_nameToClr` and `_clrToName` dictionaries. `Clr()`, `Name()`, `Register()`, `BuilderNames()`, `ComplexSchemas()`, `ValidValues()` now delegate to TypeMapping. Types keeps its unique extension→kind, extension→MIME, compressibility logic.
- **TypeMapping** (static): gained depth guard (`MaxGenericDepth=20`) on `GetType()` — security improvement ported from Types. Remains the single source of truth for name↔type resolution, needed by source generator and v1 code.
- **Engine constructor**: removed dual `Types.Register()` + `TypeMapping.Register()` — now just `Types.Register()` which delegates to TypeMapping.
- **Path.cs**: `MimeType` now uses `_engine.Types.Mime()` (80+ entries) instead of `TypeMapping.GetMimeType()` (20-entry switch). Removed unused `using PLang.Runtime2.Engine.Utility`.

### 3. Deleted Runtime2 PrParser
`PLang/Runtime2/Engine/Utility/PrParser.cs` had zero references anywhere. Used `System.IO` directly (violating the filesystem abstraction rule). The v1 PrParser at `PLang/Building/Parsers/PrParser.cs` already uses `IPLangFileSystem` correctly.

## Code example

Types/@this before (duplicate dictionaries):
```csharp
private readonly Dictionary<string, System.Type> _nameToClr = new(...) { ["string"] = typeof(string), ... };
private readonly Dictionary<System.Type, string> _clrToName = new() { [typeof(string)] = "string", ... };
public System.Type? Clr(string plangName) => Clr(plangName, 0);  // own implementation
```

Types/@this after (delegates):
```csharp
public System.Type? Clr(string plangName) => Utility.TypeMapping.GetType(plangName);
public string Name(System.Type clrType) => Utility.TypeMapping.GetTypeName(clrType);
public void Register(string plangName, System.Type clrType) => Utility.TypeMapping.Register(plangName, clrType);
```

## Build verification
- `dotnet build PLang/PLang.csproj` — 0 errors
- `dotnet build PLang.Tests/PLang.Tests.csproj` — 0 errors
