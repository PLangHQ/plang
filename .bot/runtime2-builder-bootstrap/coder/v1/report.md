# Builder Bootstrap — Coder Handover

## Goal

Make the PLang v2 builder self-hosting. The builder is written in PLang (`.goal` files in `/system/builder/`). These files are proven working — they were built and tested with the v1 builder. Now we need Runtime2 to be able to run them.

There are **3 gaps** in the runtime that block this. Once fixed, we can hand-craft `.pr` files for the builder, and it will be able to build itself and all other PLang code.

---

## Gap 1: `variable.set` needs `AsDefault` property

**File:** `PLang/Runtime2/modules/variable/set.cs`

**Problem:** PLang has `set default %path% = "."` which means "set this variable only if it hasn't been set already" (e.g., by goal parameters from the caller). The current `variable.set` action has no way to express this.

**Solution:** Add an optional `AsDefault` property (bool, default false). When true, check `Context.MemoryStack.Get(Name)` first — if it returns a non-null Data with `IsInitialized == true`, skip the set and return the existing value.

```csharp
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial object? Value { get; init; }
    public partial string? Type { get; init; }
    [Default(false)]
    public partial bool AsDefault { get; init; }

    public Task<Data> Run()
    {
        if (AsDefault)
        {
            var existing = Context.MemoryStack.Get(Name);
            if (existing != null && existing.IsInitialized)
                return Task.FromResult(existing);
        }

        Context.MemoryStack.Set(Name, Value,
            Type != null ? PLang.Runtime2.Engine.Memory.Type.FromName(Type) : null);
        return Task.FromResult(Context.MemoryStack.Get(Name) ?? Data.Ok());
    }
}
```

**Tests:**
- Set default when variable is unset → variable gets the value
- Set default when variable is already set → existing value preserved
- Normal set (AsDefault=false) always overwrites

---

## Gap 2: `file.read` needs `ResolveVariables` property

**File:** `PLang/Runtime2/modules/file/read.cs`

**Problem:** PLang has `read file "llm/BuildGoal.llm", load vars, write to %buildGoalPrompt%`. The `load vars` modifier means: after reading the file content, resolve any `%variable%` references in it using the current memory stack. The current `file.read` action just returns raw content.

**Solution:** Add an optional `ResolveVariables` property (bool, default false). When true, call `Context.MemoryStack.Resolve(content)` on the string result before returning.

`MemoryStack.Resolve(string)` already exists at `PLang/Runtime2/Engine/Memory/MemoryStack.cs:143` — it does regex replacement of `%var%` patterns.

```csharp
[Action("read")]
public partial class Read : IContext
{
    public partial PLangPath Path { get; init; }

    [Default(false)]
    public partial bool ResolveVariables { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data> Run()
    {
        var result = Files.Read(this);
        if (ResolveVariables && result.Success && result.Value is string content)
        {
            var resolved = Context.MemoryStack.Resolve(content);
            return Task.FromResult(new Data(result.Name, resolved, result.Type));
        }
        return Task.FromResult(result);
    }
}
```

**Tests:**
- Read file without ResolveVariables → raw content returned
- Read file with ResolveVariables=true, content contains `%name%` → resolved value returned
- Read file with ResolveVariables=true, no variables in content → content unchanged

---

## Gap 3: `TypeMapping.ConvertTo` — auto-wrap single value into `List<T>`

**File:** `PLang/Runtime2/Engine/Utility/TypeMapping.cs`

**Problem:** In PLang, a single object is just a list of 1. When an action parameter expects `List<T>` (e.g., `goals.save` expects `List<Goal>`) but receives a single `T`, the runtime should auto-wrap it. Currently `ConvertTo` doesn't handle this case, so passing a single Goal to `goals.save` would fail.

**Solution:** In `TypeMapping.ConvertTo(object? value, Type targetType)`, add a check: if `targetType` is `List<T>` (or implements `IList<T>`) and `value` is assignable to `T` (not already a list), wrap it in a new `List<T> { value }`.

Add this logic after the existing `targetType.IsAssignableFrom(sourceType)` check but before the other conversions:

```csharp
// Auto-wrap single value into List<T>
if (targetType.IsGenericType)
{
    var generic = targetType.GetGenericTypeDefinition();
    if (generic == typeof(List<>))
    {
        var elementType = targetType.GetGenericArguments()[0];
        if (elementType.IsAssignableFrom(sourceType))
        {
            var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
            list.Add(value);
            return list;
        }
        // Also try converting the value to the element type
        var converted = ConvertTo(value, elementType);
        if (converted != null && elementType.IsAssignableFrom(converted.GetType()))
        {
            var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
            list.Add(converted);
            return list;
        }
    }
}
```

**Tests:**
- `ConvertTo(singleGoal, typeof(List<Goal>))` → list with 1 goal
- `ConvertTo(listOfGoals, typeof(List<Goal>))` → same list (no double-wrap)
- `ConvertTo("hello", typeof(List<string>))` → list with 1 string
- `ConvertTo(null, typeof(List<Goal>))` → null (don't create empty list)

---

## Files to modify

| # | File | Change |
|---|------|--------|
| 1 | `PLang/Runtime2/modules/variable/set.cs` | Add `AsDefault` property + conditional logic |
| 2 | `PLang/Runtime2/modules/file/read.cs` | Add `ResolveVariables` property + resolve after read |
| 3 | `PLang/Runtime2/Engine/Utility/TypeMapping.cs` | Add single→list auto-wrap in `ConvertTo` |

## Files for reference (read-only)

- `PLang/Runtime2/Engine/Memory/MemoryStack.cs` — has `Resolve(string)` method (line 143)
- `PLang.Generators/LazyParamsGenerator.cs` — source generator, generates `__Resolve<T>` which uses `TypeMapping.ConvertTo`
- `PLang/Runtime2/modules/builder/goalsSave.cs` — example of `List<Goal>` parameter that needs auto-wrap

## What comes after

Once these 3 gaps are fixed, we hand-craft the `.pr` files for the builder goals (`/system/Build.goal`, `/system/builder/Build.goal`, `BuildGoal.goal`, `BuildStep.goal`, `ApplyStep.goal`). Then the builder can run on Runtime2 and build itself + all other PLang code.

## Branch

`runtime2-builder-bootstrap` — branched from `runtime2-builder-v2`
