# Code Analyzer Plan — Identity Module v1

## Scope

All production code changed on `runtime2-builder-v2-identity` vs `runtime2`:

**Identity module (8 handlers + 3 support files):**
- `PLang/Runtime2/modules/identity/create.cs`
- `PLang/Runtime2/modules/identity/get.cs`
- `PLang/Runtime2/modules/identity/getAll.cs`
- `PLang/Runtime2/modules/identity/archive.cs`
- `PLang/Runtime2/modules/identity/unarchive.cs`
- `PLang/Runtime2/modules/identity/rename.cs`
- `PLang/Runtime2/modules/identity/setDefault.cs`
- `PLang/Runtime2/modules/identity/export.cs`
- `PLang/Runtime2/modules/identity/types.cs` (IdentityVariable)
- `PLang/Runtime2/modules/identity/IdentityData.cs`
- `PLang/Runtime2/modules/identity/KeyGenerator.cs`

**Infrastructure changes:**
- `PLang/Runtime2/Engine/View.cs` (SensitiveAttribute)
- `PLang/Runtime2/Engine/Channels/Serializers/SensitivePropertyFilter.cs`
- `PLang/Runtime2/Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs`
- `PLang/Runtime2/Engine/Context/Actor.cs`

## 5-Pass Analysis

1. **OBP Compliance** — Check all 5 OBP rules against every file. Focus on: behavior ownership (persistence on IdentityVariable), navigate-don't-pass (handlers accessing Engine.System.Identity), object references vs extracted fields.

2. **Simplification** — Dead abstractions, over-parameterized methods, redundant patterns, copy-paste across handlers.

3. **Readability** — Naming, method length, flow clarity, consistency with existing module patterns.

4. **Behavioral Reasoning** — Trace data flows: sync-over-async in IdentityData.ResolveDefault(), dictionary Deserialize key casing, race conditions in lazy Identity resolution, clone/copy family audit for Actor.

5. **Deletion Tests** — For each code path: "if I deleted lines X-Y, would any test fail?" Focus on fix-introduced code paths.
