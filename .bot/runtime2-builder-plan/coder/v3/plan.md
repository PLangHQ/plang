# v3 Plan: Convert action handler properties to Data<T>

## Summary
Convert plain-typed properties in action handlers to `Data.@this<T>` wrappers, and update `Run()` methods to use `.Value` access.

## Files & Changes

### 1. `PLang/App/modules/variable/set.cs`
- `Type` (string?) -> `Data.@this<string>?` -- update Run() to use `Type?.Value`
- `AsDefault` (bool, [Default]) -> `Data.@this<bool>` -- update Run() to use `AsDefault.Value`
- Skip: `Name` ([VariableName]), `Value` (already Data.@this)

### 2. `PLang/App/modules/variable/get.cs` -- NO CHANGES (only [VariableName] property)
### 3. `PLang/App/modules/variable/exists.cs` -- NO CHANGES
### 4. `PLang/App/modules/variable/remove.cs` -- NO CHANGES
### 5. `PLang/App/modules/variable/clear.cs` -- NO CHANGES

### 6. `PLang/App/modules/condition/if.cs`
- `Operator` (Operator) -> `Data.@this<Operator>`  -- update Run() usages
- `Negate` (bool, [Default]) -> `Data.@this<bool>` -- update Run() to use `Negate.Value`
- Skip: `Left`, `Right` (already Data.@this?), `Evaluator` ([Provider]), `Step` (IStep)

### 7. `PLang/App/modules/condition/compare.cs`
- `Operator` (Operator) -> `Data.@this<Operator>` -- no Run() changes needed (Evaluator consumes `this`)

### 8. `PLang/App/modules/output/write.cs` -- NO CHANGES (Data already Data.@this, Channels from IChannel)

### 9. `PLang/App/modules/goal/call.cs`
- `GoalName` (GoalCall) -> `Data.@this<GoalCall>` -- update Run() to use `GoalName.Value!`
- `Actor` (Actor.@this?) -> `Data.@this<Actor.@this>?` -- update Run() to use `Actor?.Value`

### 10. `PLang/App/modules/goal/return.cs`
- `Depth` (int, [Default]) -> `Data.@this<int>` -- update Run() to use `Depth.Value`
- Skip: `Data` (already Data.@this?)

## Build verification
- `dotnet build PLang/PLang.csproj` after all changes
- Fix any compile errors

## Downstream: Check if Evaluator reads Operator from If/Compare
Need to check DefaultEvaluator to see if it accesses `.Operator` directly and needs `.Value`.
