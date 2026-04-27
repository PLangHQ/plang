# v14 Result — Law of Names Restructuring

## Objective
Restructure PLang App so folder structure, namespaces, and class names align with the OBP ownership hierarchy. When you see `EngineGoals`, you know it belongs to Engine. When you see `GoalSteps`, you know it belongs to Goal.

## Execution

### Phase 1: Namespace Migration + Folder Restructure

**Approach**: Longest-match-first sed replacement across all 668 .cs files, followed by `git mv` for folder moves.

**Namespace replacements** (10 total, in order):
1. `App.Memory.Navigators` -> `App.Variables.Navigators`
2. `App.Serialization` -> `App.Serializers`
3. `App.Context` -> `App.Context`
4. `App.Memory` -> `App.Variables`
5. `App.Core` -> `App.Engine`
6. `App.IO` -> `App.Channels`
7. `App.Errors` -> `App.Errors`
8. `App.Utility` -> `App.Utility`
9. `App.Parsing` -> `App.Parsing`
10. `App.Mapping` -> `App.Mapping`

**Issues resolved**:
- **Relative namespace references** in modules (`Core.Action` -> `Engine.Action`, etc.)
- **Namespace-type conflict**: `Engine` is both a namespace and a class. Fixed with `using EngineType = App.Engine;` alias in `IClass.cs` and `ICodeGenerated.cs`
- **`Memory.Type` conflict**: `Memory.Type.FromName()` confused with `System.Memory<T>`. Fixed with fully-qualified paths
- **Double replacement**: Second sed pass created `App.App.Variables.Type`. Fixed by replacing the double pattern
- **v1 relative references**: `Executor.cs` and `PlangModule/Program.cs` used `App.Core.X` (without `PLang.` prefix)
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
