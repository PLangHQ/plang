# PLang Project Instructions

## CRITICAL Rules
- **NEVER use System.IO** in PLang project code. Always use `fileSystem.File`, `fileSystem.Directory`, `fileSystem.Path`, `fileSystem.FileInfo.New`, etc. (IPLangFileSystem abstraction)
- **NEVER manually edit .pr files** — only the plang builder generates/modifies .pr files. If a .pr file has wrong data, explain the problem and let the user rebuild it.
- **NEVER delete .pr files** - plang builder should manage everything, so rebuild instead. only last resort to delete.
- **NEVER change strongly-typed parameters to `object`** — PLang is strongly typed. Need explicit permission to weaken types. Diagnose and explain the problem instead.
- **YOU MUST** follow Object-Based Pattern (OBP):
        1. **Behavior belongs to the owner** — Put methods on the object that owns the data. If `Steps` should have the list, `Steps.Run()` does the iteration. Never loop over another object's collection from the outside.
        2. **Navigate, don't pass** — Reach dependencies through the object graph (`Engine.IO`, `Engine.FileSystem`, `context.MemoryStack`). Never decompose an object into separate parameters; pass the root and let the caller navigate.
        3. **Keep object references, not extracted fields** — Store `Step`, not `step.Text`. Store `Goal`, not `goal.Name`. Wrapper DTOs are only allowed at serialization boundaries.
        4. **Per-request state is a parameter, per-object state is a property** — Never cache `PLangContext` on shared objects like `Goal` or `Step`. Pass context through method parameters; store only structural data (like `EntityEvents`) as properties.
        5. **Collections are smart wrappers** — Collection types (`Steps`, `Actions`) inherit `List<T>` and own domain operations (`Load`, `RunAsync`, `Merge`). Parents delegate to these methods — they never iterate directly.
- Use System.Text.Json instead of Newtonsoft. When Newtonsoft is noticed in Runtime2 code, suggest change (don't change automatically).
- Plang is strongly typed. The stronger typed plang and c# can communicate on the more stable everything will become

## Build Commands
- Always run `plang p build` without specifying a goal name - it builds everything
- NEVER delete .build folders.
- Use `PlangConsole/bin/Debug/net10.0/plang.exe` for net10.0 builds

## Bash syntax
-  Dont use Select-String and Select-String, it doesnt work in bash

## PLang Syntax (v0.1 builder limitations)
- Cannot combine two modules in one step (e.g., `if + set` must be separate steps)
- foreach always calls a goal, does not support sub steps. syntax: `foreach %products%, call DoProduct item=%product%`, `item=%variableName%` not `%variableName%=%item%`
- Simple set statements work: `set %step.Name% = %stepResult.method%`

## Runtime2 Architecture

### Object Graph
- **Engine is the root** — all capabilities hang off it: `Engine.IO`, `Engine.Goals`, `Engine.Actions`, `Engine.FileSystem`, `Engine.Serializers`
- **Entity hierarchy**: Goal → Steps → Actions. Each has `.Events` (EntityEvents with Before/After × Load/Runtime phases)
- **Handlers extend `BaseClass<TParams>`** — get Engine/Context via Initialize(), use `MemoryStack` for variables, `Data.Ok()`/`Data.Fail()` for results

### Key Conventions
- Goal properties: use `Path` and `PrPath` (relative), not `FilePath`/`PrFilePath`/`RelativePath`
- Step.Goal property has `[JsonIgnore]` to avoid circular reference in serialization
- v0.2 .pr.json format: single file with all steps
- **Lazy params**: Source generator creates `*__Generated` records resolving `%var%` at property access
- Handler naming: records = action name (`set`, `save`), handlers = `SetHandler`, `SaveHandler` (partial)
- `ICodeGenerated` is REQUIRED on all handlers — Engine has no fallback path
- `Data` is the universal result type: has `Value`, `Properties`, `Error`, `Success`, `Ok()`, `Fail()`, `Merge()`. It can be extended with more properties.
- `Action.Return` is `List<Data>?` — simple list of return variable mappings, no wrapper class
- Simplified modules: `variable.set(name, value, type?)`, `output.write(content)`, `file.save/read/etc`

### Source Generator Notes
- PLang.Generators: netstandard2.0, IIncrementalGenerator
- Filter out `EqualityContract` (protected, not public) when scanning virtual props
- Generated records must be `public sealed record` to match base access level

### Testing
- .NET 10: use `dotnet run --project PLang.Tests` not `dotnet test`
- Source generator only runs on PLang project; test mocks must implement `ICodeGenerated` manually
- In tests: use `System.Type?` (not `Type?`) for CLR type properties to avoid ambiguity with `PLang.Runtime2.Memory.Type`

## Debugging
In Runtime2 you can get debug/callstack information. This is usefull when step fails and more information is needed. It will give you the variable values and step pr details that might not be available in the error information. 
- run "plang p !debug" - enabled debugger on all steps
- run "plang p !debug=Start" - enable debugger on specific goal
- run "plang p !debug=Start:3" - enable debugger on specific step index

## Key Files
- PlangConsole is the executable project (not PLang which is a library)
- system/builder/*.goal - the new PLang builder written in PLang
- PLang/Runtime2/modules/*.cs - action handlers (variable/set, file/read, output/write, etc.)
- PLang/Runtime2/modules/IClass.cs, ICodeGenerated.cs, BaseClass.cs - handler interfaces
- PLang/Runtime2/Memory/Data.cs - universal data container + Type class
- PLang/Runtime2/Utility/TypeMapping.cs - PLang type names + MIME types -> CLR types
- PLang/Runtime2/Mapping/GoalMapper.cs - maps Building.Model -> Runtime2.Core
- PLang.Generators/LazyParamsGenerator.cs - source generator for lazy param resolution

For full OBP details with code examples, see `Documentation/Runtime2/plang_object_based_pattern.md`.