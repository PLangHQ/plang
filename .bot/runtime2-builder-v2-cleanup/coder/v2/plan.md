# v2 Plan: GoalCall.Parameters → Dictionary<string, Data>

## What
Change `GoalCall.Parameters` from `Dictionary<string, object?>?` to `Dictionary<string, Data>?` so goal parameters flow as `Data` — consistent with PLang's universal variable type.

## Why
All PLang variables are `Data`. When `call goal DoStuff name=%user.name%, isActive=true` passes parameters across a goal boundary, they should remain `Data` — preserving type info, properties, and error state. Currently `object?` loses all of that.

## Files to change

### 1. `PLang/Runtime2/Engine/Goals/Goal/GoalCall.cs`
- Change `Dictionary<string, object?>?` → `Dictionary<string, Data>?`

### 2. `PLang/Runtime2/Engine/this.cs` (RunGoalAsync, ~line 272-275)
- Currently: `context.MemoryStack.Set(param.Key, param.Value)`
- Change to: create a `Data` with `Name = param.Key` from the dictionary entry, then `Put()` it
- Actually, the Data in the dictionary value may not have its Name matching the parameter key (e.g., `name=%user.name%` — the Data's name is "user.name" but the parameter key is "name"). So we need to set the Data's Name to the parameter key before Put.
- Approach: `var paramData = param.Value with name set to param.Key; context.MemoryStack.Put(paramData)`
- But Data.Name has a setter — so we clone or create a new Data wrapping the value.

Wait — we shouldn't mutate the original Data (it belongs to the caller's scope). We should create a new Data with the parameter name and the resolved value.

- `context.MemoryStack.Put(new Data(param.Key, param.Value.Value, param.Value.Type))`

Hmm, but that loses Properties. Better: use `Data.Clone()` or create from the source Data properly.

Actually, the simplest correct approach: `Set(param.Key, param.Value.Value, param.Value.Type)` — this preserves the existing Set behavior but now we have typed info from the Data. The Value inside Data IS the actual value. Type IS the type.

But wait — if the value is itself a Data subclass (PathData, IdentityData), we want the whole object, not just .Value. Let me reconsider.

**Decision:** Use `MemoryStack.Set(param.Key, param.Value)` where the value parameter is the Data object itself. Then Set wraps it in a new Data with the param key name. But Set takes `object?` and wraps in `new Data(name, value)` — so the Data becomes nested.

**Better decision:** Just call `Put()` with a new Data that has the right name:
```csharp
var paramData = new Data(param.Key, param.Value.Value, param.Value.Type);
context.MemoryStack.Put(paramData);
```
This creates a fresh variable in the called goal's scope with the parameter name and the resolved value+type from the caller.

### 3. `PLang/Runtime2/Engine/Utility/GoalMapper.cs` (~line 133)
- v1 `GoalToCallInfo.Parameters` is `Dictionary<string, object?>`. Need to convert to `Dictionary<string, Data>`.
- Wrap each value: `Parameters = oldInfo.Parameters?.ToDictionary(p => p.Key, p => new Data(p.Key, p.Value))`

### 4. `PLang/Runtime2/Engine/Utility/TypeMapping.cs` (~line 315-320)
- Dictionary conversion path for GoalCall. Values are `object?` from JSON unwrap — wrap in `Data`.
- `new Dictionary<string, Data>(pDict.ToDictionary(k => k.Key, k => new Data(k.Key, k.Value)))`

### 5. `PLang/Runtime2/modules/http/providers/DefaultHttpProvider.cs` (~line 776-779)
- `p.Value is string s` → `p.Value.Value is string s` (Data wraps the actual value)

### 6. Tests
- Any existing tests that construct GoalCall with Parameters need updating
- Check for tests in PLang.Tests that reference GoalCall

## Build verification
- `dotnet build PLang/PLang.csproj`
- `dotnet build PLang.Tests/PLang.Tests.csproj`
