# Code Analysis v1 — UI Module + Clone Fixes

## Scope

Files changed on `runtime2-builder-v2-template` vs `runtime2-builder-v2-cleanup`:

### New UI module files
1. `PLang/App/modules/ui/render.cs` — action handler
2. `PLang/App/modules/ui/providers/ITemplateProvider.cs` — provider interface
3. `PLang/App/modules/ui/providers/FluidProvider.cs` — Fluid implementation (310 lines, main target)

### Clone family fixes
4. `PLang/App/Memory/Data.cs` — Clone() now virtual, Properties.Clone()
5. `PLang/App/Memory/Properties.cs` — Clone() method added
6. `PLang/App/FileSystem/PathData.cs` — Clone() override
7. `PLang/App/modules/identity/types.cs` — IdentityData.Clone() override
8. `PLang/App/Memory/Variables.cs` — clone logic narrowed to SettingsData only

### Other fixes
9. `PLang/App/Memory/Data.Envelope.cs` — InvalidOperationException catch added
10. `PLang/App/modules/condition/providers/DefaultEvaluator.cs` — InvalidCastException added to catch
11. `PLang/App/Providers/this.cs` — ITemplateProvider registration
12. `PLang/App/modules/Attributes.cs` — XML doc comments added (no behavior change)
13. Various `on.cs`, `skipAction.cs`, `remove.cs`, `GoalCall.cs` — XML doc comments only

### Test file
14. `PLang.Tests/App/Modules/ui/RenderTests.cs` — 29 tests

## Analysis Plan

### Pass 1: OBP Compliance
- FluidProvider.Render(): does it navigate or decompose?
- render.cs: does it delegate properly?
- Clone family: are all copy surfaces covered?

### Pass 2: Simplification
- FluidProvider: is anything over-engineered?
- PlangFileProvider: is the double-try pattern needed?
- RegisterTypeIfNeeded: could be simpler?

### Pass 3: Readability
- Naming, method length, flow clarity
- FluidProvider.Render() is ~80 lines — check if it should be split

### Pass 4: Behavioral Reasoning
- callGoal: does the catch(Exception) mask programming errors?
- Variables clone change: does narrowing to SettingsData break DynamicData cloning?
- Data.Clone() virtual: does DataList.Clone() preserve all fields?

### Pass 5: Deletion Test
- Can we remove RegisterTypeIfNeeded and nothing breaks?
- Can we remove the PlangFileProvider fallback try/catch?
- Clone overrides: are they tested?
