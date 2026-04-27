# Plan: Convert action handler properties to Data<T> in list/, loop/, math/, event/

## Summary
Mechanical conversion of all action handler properties from plain types to `Data.@this<T>` (or plain `Data.@this` for object/object?). Update all Run() method usages to access `.Value` or `.Value!`.

## Conversion rules
- `object` / `object?` -> `Data.@this` (plain Data wraps any value)
- `int` -> `Data.@this<int>`, usage `.Value`
- `bool` -> `Data.@this<bool>`, usage `.Value`
- `string` -> `Data.@this<string>`, usage `.Value!`
- `string?` -> `Data.@this<string>?`, usage `?.Value`
- `GoalCall` -> `Data.@this<GoalCall>`, usage `.Value!`
- `condition.Operator` -> `Data.@this<condition.Operator>`, usage `.Value!`
- Skip: `[VariableName]`, `[Provider]`, `Actor.@this?`, IContext/IStep interface props

## Files (34 action files across 4 modules)

### list/ (17 files, 12 have convertible props)
### loop/ (1 file)
### math/ (13 action files + MathHelper unchanged)
### event/ (3 files)

## Build verification
After all edits, run `dotnet build PLang/PLang.csproj` to verify compilation.
