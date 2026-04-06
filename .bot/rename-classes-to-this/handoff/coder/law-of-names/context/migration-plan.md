# Law of Names — Migration Plan

## Blast Radius

| Area | Files | References to update |
|------|-------|---------------------|
| App production | ~165 files | Every namespace declaration |
| LazyParamsGenerator | 1 file | 15 hardcoded namespace strings |
| PlangModule/Program.cs | 1 file | ~32 App references |
| C# test files | 55 files | ~322 App references |
| PLang .goal test files | 0 | No namespace refs in .goal files |

**Total**: ~222 files touched. Zero behavior change in phases 1-3.

---

## Phase 0: Green Baseline

**Goal**: Verify everything compiles and passes before we touch anything.

**Operations**:
1. `dotnet build PLang.sln` — must succeed
2. `dotnet run --project PLang.Tests` — must pass
3. `plang p build` in test directories — must succeed
4. Create a git tag `pre-law-of-names` as a rollback point

**Commit**: None. Just verification.

---

## Phase 1: Folder Restructure + Namespace Migration

**Goal**: Move all files to their Law of Names locations. Update all namespaces. Zero behavior change.

This is one atomic commit. We do NOT split it into sub-phases because:
- Partial moves create intermediate states where some files reference old namespaces and others new
- Temporary `using` aliases or forwarding types add complexity for zero benefit
- It's 100% mechanical search-and-replace — it either compiles or it doesn't
- Any build error is trivially fixable (missed `using` statement)

### Namespace mapping (search-and-replace)

| Old namespace | New namespace |
|---|---|
| `App.Core` | `App.Engine` |
| `App.Context` | `App.Context` |
| `App.Memory.Navigators` | `App.Variables.Navigators` |
| `App.Memory` | `App.Variables` |
| `App.IO` | `App.Channels` |
| `App.Errors` | `App.Errors` |
| `App.Serialization` | `App.Serializers` |
| `App.Utility` | `App.Utility` |
| `App.Parsing` | `App.Parsing` |
| `App.Mapping` | `App.Mapping` |

**Important ordering**: `Memory.Navigators` must be replaced BEFORE `Memory` (longer match first). Same for any nested namespaces.

### modules/ split

The `modules/` folder has two kinds of files:

**Infrastructure** (IClass, ICodeGenerated, Libraries, Library, ActionAttribute, DefaultAttribute, VariableNameAttribute, IContext):
- Move to: `Engine/Libraries/`
- New namespace: `App.Libraries`

**Handlers** (variable/, file/, output/, etc.):
- Move to: `Engine/modules/{module}/`
- New namespace: `App.modules` (or `.modules.{subfolder}` if they already use sub-namespaces)
- Update `Library.Discover("App.modules")` → `Library.Discover("App.modules")`

### File moves

Create this folder structure under `PLang/App/`:
```
Engine/
├── Goals/
│   └── Steps/
│       └── Actions/
├── Channels/
├── Property/
├── Events/
├── Serializers/
├── Cache/
├── Debug/
├── Testing/
├── Context/
├── Memory/
│   └── Navigators/
├── Libraries/
├── modules/
│   ├── assert/
│   ├── condition/
│   ├── convert/
│   ├── error/
│   ├── event/
│   ├── file/
│   ├── goal/
│   ├── library/
│   ├── list/
│   ├── loop/
│   ├── math/
│   ├── mock/
│   ├── output/
│   └── variable/
├── Errors/
├── Utility/
├── Parsing/
└── Mapping/
```

Move files per the tree map in `result.md`. Key moves:
- `Core/*.cs` → `Engine/` (Engine.cs becomes `Engine/this.cs`) and sub-folders
- `Memory/Data.cs` → split into `Engine/Data.cs` and `Engine/Type.cs` (Data/Type to root)
- `Memory/*.cs` (rest) → `Engine/Memory/`
- `Context/EventScope.cs` → `Engine/Events/EventScope.cs`
- `IO/*.cs` → `Engine/Channels/`
- `Serialization/*.cs` → `Engine/Serializers/`
- `modules/` infrastructure → `Engine/Libraries/`
- `modules/` handlers → `Engine/modules/`
- `Errors/*.cs` → `Engine/Errors/`

### External files to update

1. **LazyParamsGenerator.cs**: Replace all 15 hardcoded namespace strings
2. **PlangModule/Program.cs**: Update 4 `using` statements + any inline references
3. **All 55 test files**: Update `using` statements (322 references)

### Verification
- `dotnet build PLang.sln` — must succeed
- `dotnet run --project PLang.Tests` — all tests pass
- `plang p build` — PLang builder still works

**Commit message**: `refactor: Move App to Engine/ folder structure (Law of Names phase 1)`

---

## Phase 2: File Organization

**Goal**: Split multi-class files, apply dot-naming convention. Zero behavior change.

### Operations

1. **Split `EventCollection.cs`** into 3 files:
   - `Engine/Events/EngineEvents.cs` → `Events` class (stays named `Events` for now — rename in Phase 3)
   - `Engine/Events/EventBinding.cs` → `EventBinding` class
   - `Engine/Events/EventType.cs` → `EventType` enum

2. **Split `Data.cs`** — `Type` class moves to its own file:
   - `Engine/Data.cs` → keeps `Data`, `Data<T>`, `DynamicData`
   - `Engine/Type.cs` → gets `Type` class

3. **Split `CallStack.cs`** — serializable types to separate file:
   - `Engine/Context/CallStack.cs` → keeps `CallStack`
   - `Engine/Context/SerializableCallStack.cs` → gets `SerializableCallStack`, `SerializableCallFrame`

4. **Dot-rename partial files**:
   - `GoalMethods.cs` → `Goal.Methods.cs`
   - `StepMethods.cs` → `Step.Methods.cs`
   - `ActionMethods.cs` → `Action.Methods.cs`

5. **Split `Lifecycle.cs`** (optional — `Bindings` and `Lifecycle` are closely coupled, could stay together)

### Verification
- `dotnet build PLang.sln`
- `dotnet run --project PLang.Tests`

**Commit message**: `refactor: Split multi-class files and apply dot-naming convention (Law of Names phase 2)`

---

## Phase 3: Convention Renames

**Goal**: Rename the 11 classes to follow `{Owner}{Capability}` convention. Zero behavior change — just renames.

### Renames

| Current | New | Files affected (estimated) |
|---------|-----|---------------------------|
| `Goals` | `EngineGoals` | ~15 |
| `Steps` | `GoalSteps` | ~10 |
| `Actions` | `StepActions` | ~20 |
| `Channels` (IO) | `EngineChannels` | ~10 |
| `Property` | `EngineProperty` | ~5 |
| `Events` | `EngineEvents` | ~15 |
| `SerializerRegistry` | `EngineSerializers` | ~10 |
| `Libraries` | `EngineLibraries` | ~15 |
| `DebugMode` | `EngineDebug` | ~3 |
| `TestMode` | `EngineTesting` | ~3 |

**Also rename `this.cs` files** to match their class name:
- `Engine/Goals/this.cs` → contains `EngineGoals`
- etc.

### Watch out for
- `Actions` is used both as the collection type AND in `using Actions = App.Core.Actions` in PlangModule. Update the alias.
- `Events` is a common word — make sure we only rename `App.Events` class, not the `EventType` enum or event-related types.
- LazyParamsGenerator references: none of the renamed classes appear in the generator (it references namespaces, not class names). But verify.

### Verification
- `dotnet build PLang.sln`
- `dotnet run --project PLang.Tests`

**Commit message**: `refactor: Rename classes to {Owner}{Capability} convention (Law of Names phase 3)`

---

## Phase 4: New Convention Types

**Goal**: Create `EngineCache`, convert `EngineDebug`/`EngineTesting` from static to instance. Small behavioral changes.

### Operations

1. **Create `EngineCache`** (`Engine/Cache/this.cs`):
   ```csharp
   public sealed class EngineCache
   {
       private ICache _implementation = new MemoryStepCache();

       public ICache Implementation
       {
           get => _implementation;
           set => _implementation = value ?? throw new ArgumentNullException(nameof(value));
       }

       public Task<object?> GetAsync(string key, CancellationToken ct = default)
           => _implementation.GetAsync(key, ct);
       public Task SetAsync(string key, object value, CacheSettings settings, CancellationToken ct = default)
           => _implementation.SetAsync(key, value, settings, ct);
       public Task RemoveAsync(string key, CancellationToken ct = default)
           => _implementation.RemoveAsync(key, ct);
   }
   ```

2. **Convert `EngineDebug` from static to instance**:
   - Remove `static` from class
   - `Apply(Engine engine, object debugValue)` → `Enable(object debugValue)` (engine comes from constructor or navigation)
   - Engine stores `EngineDebug Debug { get; }`

3. **Convert `EngineTesting` from static to instance**:
   - Remove `static` from class
   - `RunAsync(Engine engine, ...)` → `RunAsync(...)` (engine comes from constructor)
   - Engine stores `EngineTesting Testing { get; }`

4. **Update Engine**:
   - `ICache Cache { get; set; }` → `EngineCache Cache { get; }`
   - Add `EngineDebug Debug { get; }`
   - Add `EngineTesting Testing { get; }`
   - Update constructor to create instances
   - Update all `engine.Cache.GetAsync(...)` call sites (should work — `EngineCache` delegates)

5. **Update callers**:
   - `DebugMode.Apply(engine, value)` → `engine.Debug.Enable(value)`
   - `TestMode.RunAsync(engine, ct)` → `engine.Testing.RunAsync(ct)`

### Verification
- `dotnet build PLang.sln`
- `dotnet run --project PLang.Tests`
- `plang p !debug` — debug mode still works
- `plang p !test` — test mode still works

**Commit message**: `refactor: Add EngineCache, convert Debug/Testing to instance classes (Law of Names phase 4)`

---

## Phase 5: ConventionWiringGenerator

**Goal**: Write the source generator that discovers `{Owner}{Capability}` classes and auto-wires `Lazy<T>` properties on the owner.

### What the generator does

1. **Scans for convention classes**: Any class named `{X}{Y}` where `{X}` is a known owner type (Engine, Goal, Step, etc.)
2. **Generates a partial class** on the owner with a `Lazy<{X}{Y}>` property named `{Y}`
3. **Wires construction**: The lazy factory creates the instance, passing the owner if the convention class has a constructor that accepts it

### Input → Output example

The generator sees:
```csharp
// In Engine/Goals/this.cs
namespace App.Goals;
public sealed class EngineGoals { ... }
```

It generates:
```csharp
// Engine.Goals.g.cs
namespace App.Engine;
partial class Engine
{
    private Lazy<Goals.EngineGoals> _goals = new(() => new Goals.EngineGoals());
    public Goals.EngineGoals Goals => _goals.Value;
}
```

### Operations

1. **Engine becomes `partial`** (currently `sealed` — change to `sealed partial`)
2. **Write `ConventionWiringGenerator.cs`** in PLang.Generators project
3. **Remove manual property declarations** from Engine:
   - Remove `_libraries`, `_serializers`, `_goals` fields
   - Remove `Libraries`, `Serializers`, `Goals`, `Channels`, `Events`, `Property`, `Cache` properties
   - Remove their construction from the constructor
   - The generator now creates them
4. **Handle constructor parameters**: Some convention types need Engine reference (e.g., `EngineProperty(engine)`, `EngineChannels(engine)`). The generator must detect constructors that take the owner type and pass `this`.
5. **Handle external dependencies**: `FileSystem` is NOT convention-wired (it's an interface injected from outside). Keep it as a manual property.
6. **Update LazyParamsGenerator**: If it references any of the removed properties by type name, update.

### Design questions for the generator
- **Discovery rule**: Scan by naming convention (`{Owner}{Capability}`) or by attribute (`[ConventionWired]`)? Naming convention is the Law of Names — the name IS the declaration. But attributes are more explicit. **Recommendation**: Naming convention. That's the whole point.
- **Owner registry**: The generator needs to know which types are owners. Start with: `Engine`, `Goal`, `Step`. Could be attribute-based: `[ConventionOwner]`.

### Verification
- `dotnet build PLang.sln` — generator runs, properties are generated
- `dotnet run --project PLang.Tests` — all tests pass
- Verify generated code: check `obj/` for `*.g.cs` files

**Commit message**: `feat: ConventionWiringGenerator — auto-wire {Owner}{Capability} properties (Law of Names phase 5)`

---

## Phase 6: MethodRun Dispatch

**Goal**: Wire the convention into the runtime dispatch path. When PLang code does `%engine.Goals%`, it navigates the convention-wired object graph.

### What this means

Currently, `Variables.Get("engine")` returns the Engine object, and dot-navigation (`engine.Goals`) uses reflection/navigators to find the `Goals` property. This already works because the properties exist on Engine.

After Phase 5, the properties are source-generated. They still exist on Engine, so dot-navigation still works. **Phase 6 may be a no-op** if the generated properties are public and visible to the navigators.

### Verify
- `plang p build` then `plang p` with a .goal file that accesses `%engine.Goals%`, `%engine.Channels%`, etc.
- If navigation works → Phase 6 is done
- If not → the ObjectNavigator needs to see generated properties (they should be public, so this should work)

### If MethodRun dispatch is needed
If we want explicit dispatch (not reflection), we'd generate a `MethodRun` method on Engine that does a switch:
```csharp
public object? MethodRun(string path)
{
    return path switch
    {
        "Goals" => Goals,
        "Channels" => Channels,
        "Property" => Property,
        // ...
        _ => null
    };
}
```
This is an optimization, not a requirement. The object graph + reflection works today.

**Commit message**: `feat: Verify convention-wired properties work with PLang navigation (Law of Names phase 6)`

---

## Phase Summary

| Phase | What | Risk | Files touched | Behavior change |
|-------|------|------|---------------|-----------------|
| 0 | Green baseline | None | 0 | No |
| 1 | Folder moves + namespaces | Medium (big diff, but mechanical) | ~222 | No |
| 2 | File splits + dot-naming | Low | ~10 | No |
| 3 | Convention renames | Low-Medium (many refs) | ~80 | No |
| 4 | EngineCache + static→instance | Low | ~15 | Minor |
| 5 | ConventionWiringGenerator | Medium (new generator) | ~10 | Yes (generated props) |
| 6 | MethodRun dispatch | Low (likely no-op) | ~2 | No |

**Total estimated effort**: Phases 1-3 are mechanical and can be done in a single focused session. Phase 4 is small. Phase 5 is the most interesting work. Phase 6 is verification.

---

## Handoff to Coder

Phases 1-4 are pure Coder work — mechanical refactoring, no design decisions left. Phase 5 is Coder + Architect (generator design is settled, but implementation may surface edge cases). Phase 6 is verification.

**Recommended approach**: Hand off Phases 1-3 as a single task ("restructure App per Law of Names tree map"). Phase 4 as a follow-up. Phase 5 as a separate task.
