# v14 Result — Law of Names Restructuring

## Objective
Restructure PLang Runtime2 so folder structure, namespaces, and class names align with the OBP ownership hierarchy. When you see `EngineGoals`, you know it belongs to Engine. When you see `GoalSteps`, you know it belongs to Goal.

## Execution

### Phase 1: Namespace Migration + Folder Restructure

**Approach**: Longest-match-first sed replacement across all 668 .cs files, followed by `git mv` for folder moves.

**Namespace replacements** (10 total, in order):
1. `PLang.Runtime2.Memory.Navigators` -> `PLang.Runtime2.Engine.Memory.Navigators`
2. `PLang.Runtime2.Serialization` -> `PLang.Runtime2.Engine.Serializers`
3. `PLang.Runtime2.Context` -> `PLang.Runtime2.Engine.Context`
4. `PLang.Runtime2.Memory` -> `PLang.Runtime2.Engine.Memory`
5. `PLang.Runtime2.Core` -> `PLang.Runtime2.Engine`
6. `PLang.Runtime2.IO` -> `PLang.Runtime2.Engine.Channels`
7. `PLang.Runtime2.Errors` -> `PLang.Runtime2.Engine.Errors`
8. `PLang.Runtime2.Utility` -> `PLang.Runtime2.Engine.Utility`
9. `PLang.Runtime2.Parsing` -> `PLang.Runtime2.Engine.Parsing`
10. `PLang.Runtime2.Mapping` -> `PLang.Runtime2.Engine.Mapping`

**Issues resolved**:
- **Relative namespace references** in modules (`Core.Action` -> `Engine.Action`, etc.)
- **Namespace-type conflict**: `Engine` is both a namespace and a class. Fixed with `using EngineType = PLang.Runtime2.Engine.Engine;` alias in `IClass.cs` and `ICodeGenerated.cs`
- **`Memory.Type` conflict**: `Memory.Type.FromName()` confused with `System.Memory<T>`. Fixed with fully-qualified paths
- **Double replacement**: Second sed pass created `PLang.Runtime2.Engine.PLang.Runtime2.Engine.Memory.Type`. Fixed by replacing the double pattern
- **v1 relative references**: `Executor.cs` and `PlangModule/Program.cs` used `Runtime2.Core.X` (without `PLang.` prefix)
- **Source generator**: Hardcoded namespace strings in `LazyParamsGenerator.cs` updated

**Result**: 212 files changed, 0 build errors, 1167/1167 tests passing.

### Phase 3: Convention Renames

**9 classes renamed** to `{Owner}{Capability}` pattern:

| Old Name | New Name | Files Changed |
|----------|----------|---------------|
| SerializerRegistry | EngineSerializers | 4 |
| TestMode | EngineTesting | 2 |
| DebugMode | EngineDebug | 9 |
| Channels | EngineChannels | 16 |
| Libraries | EngineLibraries | 12 |
| Steps | GoalSteps | 21 |
| Actions | StepActions | 39 |
| Goals | EngineGoals | 58 |
| Property | EngineProperty | 16 |

**Issues resolved**:
- Constructor names not updated by sed (e.g., `Property()` -> `EngineProperty()`)
- Fully-qualified references in test files not caught by targeted sed
- Using aliases in `PlangModule/Program.cs` needed updating

**Result**: 44 files changed, 0 build errors, 1167/1167 tests passing.

## Recommendations

1. **Phase 4 should be a separate session** — converting `EngineDebug`/`EngineTesting` from static to instance is a behavioral change, not just a rename
2. **Phase 2 is cosmetic** — dot-naming for partials and splitting multi-class files can be done anytime
3. **The `EngineType` alias** in `IClass.cs`/`ICodeGenerated.cs` is a known workaround. If the namespace structure changes again, this needs revisiting.
