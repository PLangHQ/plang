# Phase 1 Implementation Plan: Engine.Types

## Goal

Replace three scattered type-knowledge sources with `Engine.Types` — a single live instance on Engine that owns PLang names, CLR types, file extension kinds, MIME types, and compressibility data. This is the foundation for Phases 2-4 (Type context, Data restructure, envelope pipeline).

## Current State (what exists)

1. **`PLang/Runtime2/Engine/Utility/TypeMapping.cs`** — static class with:
   - `NameToType` dictionary (PLang name → CLR type, 50+ entries)
   - `TypeToName` dictionary (CLR type → PLang name, 15 entries)
   - `GetType(string)`, `GetTypeName(Type)`, `ConvertTo()`, `IsPrimitive()`, `GetMimeType()`, `GetBuilderTypeNames()`, `GetComplexTypeSchemas()`, `GetValidValues()`

2. **`PLang/Modules/FileModule/TypeMapping.cs`** — instance class with:
   - `typeMap` dictionary (extension → kind, ~100 entries)
   - `contentTypeMap` dictionary (extension → MIME, ~60 entries)
   - `GetType()`, `GetContentType()`, `AddMapping()`
   - Note: `.key` appears twice (presentation + certificate)

3. **`PLang/Utils/MimeTypeHelper.cs`** — static class with:
   - Redundant MIME mapping (~50 entries)
   - `GetWebMimeType()`, `GetMimeType()`

### Consumers of static TypeMapping

- `Data.cs` lines 25, 107, 126, 148, 166 — ClrType, constructor, Value setter, GetValue<T>, GetValue(Type)
- `file/types.cs` lines 39, 72 — GetMimeType for file path
- `StepActions` line 48 — GetTypeName for builder schema

### Consumers of MimeTypeHelper

- `PLang/Modules/FileModule/Program.cs`
- `PLang/Modules/HttpModule/Program.cs`

### Consumers of FileModule.TypeMapping

- `PLang/Modules/FileModule/Program.cs` (via ITypeMapping interface)

## What I'll Build

### 1. New file: `PLang/Runtime2/Engine/Types/this.cs`

The `@this` class (following OBP `@this` convention) that owns all type knowledge:

```csharp
namespace PLang.Runtime2.Engine.Types;

public sealed class @this
{
    // Data stores (private)
    private readonly Dictionary<string, System.Type> _nameToClr;     // PLang name → CLR type
    private readonly Dictionary<System.Type, string> _clrToName;     // CLR type → PLang name
    private readonly Dictionary<string, string> _extensionToKind;    // ".jpg" → "image"
    private readonly Dictionary<string, string> _extensionToMime;    // ".jpg" → "image/jpeg"
    private readonly HashSet<string> _notCompressible;               // "image", "video", "audio", "archive"

    // Public API (nouns, no verbs per OBP)
    public System.Type? Clr(string plangName);           // PLang name → CLR type
    public string Name(System.Type clrType);             // CLR type → PLang name
    public string? Kind(string extension);               // Extension → kind
    public string Mime(string extension);                 // Extension → MIME
    public bool Compressible(string kind);               // Kind → compressible?

    // Runtime extensibility
    public void Add(string extension, string kind, string? mime = null);
    public void Remove(string extension);

    // Builder helpers (moved from static TypeMapping)
    public List<string> BuilderNames();
    public Dictionary<string, string> ComplexSchemas();
}
```

**OBP compliance:**
- One-word noun names: `Clr`, `Name`, `Kind`, `Mime`
- Instance on Engine, not static
- No `Get` prefix

### 2. Modify: `PLang/Runtime2/Engine/this.cs`

Add `Types` property to Engine:

```csharp
public Types.@this Types { get; }
// Initialize in constructor: Types = new Types.@this();
```

### 3. Add global using: `PLang/Runtime2/GlobalUsings.cs`

```csharp
global using EngineTypes = PLang.Runtime2.Engine.Types.@this;
```

### 4. Keep: Static helpers on old TypeMapping

`ConvertTo()`, `IsPrimitive()`, and `GetValidValues()` are **pure stateless functions** — they don't depend on dictionaries. They stay as static helpers on the existing TypeMapping class (or a new static utility). No point making them instance methods on Engine.Types since they don't need runtime extensibility.

The old `TypeMapping.GetType()`, `TypeMapping.GetTypeName()`, and `TypeMapping.GetMimeType()` will delegate to `Engine.Types` in Phase 2 (when Data gets context). For now, I'll keep the **old static class working as-is** alongside the new Engine.Types. Phase 1 is additive — new class, no breaking changes.

**Rationale:** The architect said "Pure logic (`ConvertTo`, `IsPrimitive`) is stateless — can stay as static helpers or move, coder's call." Data.cs currently has no access to Engine (no context yet — that's Phase 2). Changing Data's constructor to require Engine/context in Phase 1 would be a massive breaking change. So Phase 1 builds the new Types instance and Engine property, but existing code continues using static TypeMapping. Phase 2 wires them together when Data gets context.

### 5. Fix: `.key` extension conflict

FileModule has `.key` mapped to both "presentation" (line 87) and "certificate" (line 174). The second entry wins in the dictionary. Architect's decision: keep "certificate". I'll ensure Engine.Types only has `.key` → "certificate".

### 6. Phase 1 scope — what is NOT changing

- Data.cs stays exactly as-is (no context yet)
- Type class stays exactly as-is (no lazy Kind yet)
- Static TypeMapping stays as-is (consumed by Data until Phase 2)
- FileModule.TypeMapping stays as-is (consumers keep using ITypeMapping)
- MimeTypeHelper stays as-is (deprecated in Phase 2+)

## File Changes Summary

| File | Action | What |
|------|--------|------|
| `PLang/Runtime2/Engine/Types/this.cs` | **Create** | New Types class with all type knowledge |
| `PLang/Runtime2/Engine/this.cs` | **Modify** | Add `Types` property, initialize in constructor |
| `PLang/Runtime2/GlobalUsings.cs` | **Modify** | Add `EngineTypes` global alias |

## Testing Plan

### C# Tests

- `PLang.Tests/Runtime2/Engine/Types/TypesTests.cs` — test all public methods:
  - `Clr("string")` → `typeof(string)`, generics, nullable
  - `Name(typeof(string))` → `"string"`, arrays, generics
  - `Kind(".jpg")` → `"image"`, unknown extension → null
  - `Mime(".jpg")` → `"image/jpeg"`, unknown → `"application/octet-stream"`
  - `Compressible("text")` → true, `Compressible("image")` → false
  - `Add/Remove` extensibility
  - `.key` → `"certificate"` (conflict resolved)

### PLang Tests (deferred)

PLang .goal tests validate the full pipeline (builder → .pr → runtime). Since Phase 1 is additive with no behavior change to existing code, PLang tests aren't needed yet. They'll be critical in Phase 2 when Data starts using Engine.Types.

## Verification

After implementation, I will:
1. Run `dotnet build` on the solution to verify compilation
2. Run existing tests to verify no regressions
3. Run the new C# tests

## Dependencies

None — Phase 1 is pure addition. No existing behavior changes.
