# Plang Engine Refactoring Plan

## Vision

Transform the plang runtime from a heavy C#-centric architecture to a lean, self-hosting system where plang code bootstraps itself. The goal is:

1. **Minimal C# bootstrap** - Only essential types registered in C#
2. **Plang-driven registration** - Services registered via `system/Run.goal`
3. **Flatter execution** - Simplified goal/step execution without deep call chains
4. **Events as steps** - Events are already steps via EventModule, unify the model
5. **Error handling in plang** - Errors are objects, Error.goal handles display

---

## Current State

### Container.cs (~916 lines)
- Registers 50+ modules via reflection
- Multiple registration methods: `RegisterForPLangConsole`, `RegisterForPLangBuilderConsole`, `RegisterForPLangWebserver`, etc.
- Services: db, llm, caching, settings, encryption, archiver, logger, askuser
- All registered upfront before any plang code runs

### Engine.cs (~1250 lines)
- Deep call chain: `Run()` -> `RunGoal()` -> `RunSteps()` -> `RunStep()` -> `ProcessPrFile()`
- Event handling interleaved at each level
- Error handling with retry logic
- Engine pooling for concurrent execution

### InjectModule (existing)
- Already supports: db, settings, caching, logger, llm, askuser, encryption, archiver
- Calls `Container.RegisterForPLangUserInjections()`
- Can be extended for bootstrap registrations

### EventModule (existing)
- Events can already be steps
- Not limited to Events/ folder

---

## Phase 1: DI Simplification

### Goal
Reduce Container.cs to minimal bootstrap, move registrations to `system/Run.goal`.

### 1.1 Identify Chicken-Egg Dependencies

**Must remain in C# (required to run any plang code):**

| Type | Interface | Why |
|------|-----------|-----|
| File System | `IPLangFileSystem` | Read .goal and .pr files |
| PR Parser | `PrParser` | Parse compiled .pr files |
| Memory Stack | `MemoryStack`, `IMemoryStackAccessor` | Variable storage |
| Context | `IPLangContextAccessor`, `PLangAppContext` | Execution context |
| Minimal Engine | `IEngine` | Execute bootstrap goal |
| Type Helper | `ITypeHelper` | Reflection utilities |
| Base Module Infrastructure | `BaseProgram` | Module execution |

**Move to plang registration:**

| Type | Interface |
|------|-----------|
| Database | `IDbConnection` |
| LLM Service | `ILlmService` |
| Caching | `IAppCache` |
| Settings | `ISettingsRepository` |
| Encryption | `IEncryption` |
| Archiver | `IArchiver` |
| Logger | `ILogger` |
| Ask User | `IAskUserHandler` |
| Identity Service | `IPLangIdentityService` |
| Signing Service | `IPLangSigningService` |
| Output Streams | `IOutputStreamFactory` |

**Not needed:**
- `IErrorHandlerFactory` - Errors are objects, `Error.goal` handles display
- Complex error handler infrastructure

### 1.2 Create MinimalContainer

**New file:** `PLang/Container/MinimalContainer.cs`

```csharp
public static class MinimalContainer
{
    public static void RegisterBootstrap(this IServiceContainer container, string rootPath)
    {
        // File system
        container.Register<IPLangFileSystem, PLangFileSystem>(new PerContainerLifetime());

        // Parser
        container.Register<PrParser>(new PerContainerLifetime());

        // Context
        container.Register<PLangAppContext>(new PerContainerLifetime());
        container.Register<IPLangContextAccessor, ContextAccessor>(new PerContainerLifetime());
        container.Register<IMemoryStackAccessor, MemoryStackAccessor>(new PerContainerLifetime());

        // Type utilities
        container.Register<ITypeHelper, TypeHelper>(new PerContainerLifetime());

        // Minimal engine
        container.Register<IEngine, Engine>(new PerContainerLifetime());

        // Only modules needed for bootstrap
        RegisterBootstrapModules(container);
    }

    private static void RegisterBootstrapModules(IServiceContainer container)
    {
        // InjectModule - for registering services
        // CallGoalModule - for calling goals
        // OutputModule - for basic output (or minimal console write)
    }
}
```

### 1.3 Update system/Run.goal

```plang
Run
/ Bootstrap service registrations
- inject db, path: sqlite, global
- inject llm, path: plang, global
- inject caching, path: memory, global
- inject settings, path: sqlite, global
- inject logger, path: default, global
- inject encryption, path: default, global

/ Run the app
- call goal %goalName% %parameters%
```

### 1.4 Tasks

- [ ] Analyze exact minimum dependencies for bootstrap
- [ ] Create `MinimalContainer.cs`
- [ ] Update `system/Run.goal` with registrations
- [ ] Extend `InjectModule` if needed for additional service types
- [ ] Update entry points to use minimal bootstrap
- [ ] Test all existing functionality
- [ ] Deprecate old registration methods

---

## Phase 2: Flatter Engine

### Goal
Simplify Engine from ~1250 lines to ~200-300 lines with flat execution.

### 2.1 Current Call Chain

```
Run()
  └─> RunGoal()
       └─> RunSteps()
            └─> RunStep()
                 └─> ProcessPrFile()
```

Each level adds:
- Event calls (before/after)
- Error handling
- Logging
- Context management

### 2.2 New Flat Execution

The key insight from `Executor.Run2()`:

```csharp
// Get goal
var (goal, error) = prParser.GetGoal(goalInfo);

// Get step and instruction
step.Instruction = prParser.ParseInstructionFile(step);

// Get module, init, run
var module = container.GetInstance(moduleType) as BaseProgram;
module.Init(container, goal, step, step.Instruction, contextAccessor);
await module.Run();
```

### 2.3 Simplified Engine

```csharp
public async Task<(object?, IError?)> RunGoal(Goal goal, PLangContext context)
{
    context.CallStack.EnterGoal(goal);

    try
    {
        // Events are steps in goal.GoalSteps (via EventModule)
        // Error handlers are steps (via ErrorModule or on-error syntax)

        for (int i = 0; i < goal.GoalSteps.Count; i++)
        {
            var step = goal.GoalSteps[i];
            if (!step.Execute) continue;

            var (result, error) = await ExecuteStep(goal, step, context);

            if (error != null)
            {
                // Set error variable for error handler steps
                context.MemoryStack.Put("!error", error);
                // Error handling steps will check this
            }
        }

        return (context.MemoryStack.GetReturnValue(), null);
    }
    finally
    {
        context.CallStack.ExitGoal();
    }
}

private async Task<(object?, IError?)> ExecuteStep(Goal goal, GoalStep step, PLangContext context)
{
    step.Instruction ??= prParser.ParseInstructionFile(step);

    var moduleType = typeHelper.GetRuntimeType(step.ModuleType);
    var module = container.GetInstance(moduleType) as BaseProgram;
    module.Init(container, goal, step, step.Instruction, contextAccessor);

    return await module.Run();
}
```

### 2.4 What Gets Simplified/Removed

| Current | New |
|---------|-----|
| `Run()` ~90 lines | Entry point only, calls RunGoal |
| `RunGoal()` ~65 lines | Simple loop through steps |
| `RunSteps()` ~70 lines | Merged into RunGoal |
| `RunStep()` ~85 lines | Merged into ExecuteStep |
| `ProcessPrFile()` ~110 lines | Simplified to ExecuteStep |
| Event calls in each method | Events are steps (EventModule) |
| Error handling in each method | Errors are objects, handler steps check `%!error%` |

### 2.5 Tasks

- [ ] Create simplified `RunGoal` with flat loop
- [ ] Create `ExecuteStep` (simplified ProcessPrFile)
- [ ] Ensure events work as steps
- [ ] Ensure error handling works via `%!error%` and handler steps
- [ ] Keep engine pooling for concurrent execution
- [ ] Test: Basic goal execution
- [ ] Test: Nested goal calls (CallGoalModule)
- [ ] Test: Concurrent execution
- [ ] Test: Error propagation
- [ ] Remove deprecated methods

---

## Phase 3: Events as Steps (Unification)

### Goal
Ensure events are fully unified as steps, simplify event loading.

### 3.1 Current State

- EventModule already allows events as steps
- Events can be defined anywhere, not just Events/ folder
- `IEventRuntime` loads and manages events

### 3.2 Simplification

Since events are already steps:
1. Event definitions in Events/ are parsed into steps
2. These steps are injected into goals at appropriate points
3. EventModule checks conditions and decides execution

### 3.3 Event Step Injection

When goal is loaded, event steps can be prepended/appended:

```plang
/ Original goal
MyGoal
- do something
- do another thing

/ With events injected (conceptually)
MyGoal
- [event] run before-goal events      <- from Events.goal "before each goal"
- do something
- do another thing
- [event] run after-goal events       <- from Events.goal "after each goal"
```

The event steps use EventModule to check if they should actually run.

### 3.4 Tasks

- [ ] Review EventModule capabilities
- [ ] Determine if event injection happens at parse time or runtime
- [ ] Simplify/remove IEventRuntime if possible
- [ ] Test: Before/after goal events
- [ ] Test: Before/after step events
- [ ] Test: Conditional events
- [ ] Test: Error events (on error call Goal)

---

## Phase 4: Error Handling Simplification

### Goal
Errors are objects, `Error.goal` displays them, handler steps manage flow.

### 4.1 Current State

Complex error handling in Engine:
- `HandleStepError()` ~70 lines
- `HandleGoalError()` ~10 lines
- Retry logic
- AskUser error handling
- FileAccess error handling
- Multiple error types and interfaces

### 4.2 New Approach

1. **Errors are objects** - Just data with message, code, etc.
2. **`%!error%` variable** - Set when error occurs
3. **Error handler steps** - Check `%!error%`, take action
4. **`Error.goal`** - System goal to display errors

```plang
/ In system/Error.goal
Error
- if %!error% is empty then end goal
- write out "Error: %!error.Message%"
- if %!error.Step% is set, write out "  at step: %!error.Step.Text%"
```

### 4.3 On Error Syntax

Current `on error` syntax becomes a step:

```plang
/ User writes
- call http://api.example.com, on error 'timeout' call !HandleTimeout

/ Becomes (conceptually)
- call http://api.example.com
- [error-check] if %!error.Message% contains 'timeout' then call !HandleTimeout
```

### 4.4 Tasks

- [ ] Ensure errors are simple objects
- [ ] Create/update `system/Error.goal`
- [ ] Ensure `%!error%` is set on step failure
- [ ] Ensure `on error` syntax works as steps
- [ ] Remove complex error handling from Engine
- [ ] Test: Basic error display
- [ ] Test: On error call goal
- [ ] Test: Retry logic (if kept)

---

## Phase 5: Future Optimization - Lazy Module Loading

### Goal
Only load modules when needed, based on PR file ModuleType.

### 5.1 Concept

- PR files specify `ModuleType` for each step
- Load module only when first step using it executes
- Validates ModuleType exists

### 5.2 Benefits

- Faster startup
- Lower memory usage
- Validation of module references

### 5.3 Tasks (Future)

- [ ] Create lazy module loader
- [ ] Modify container for on-demand loading
- [ ] Add ModuleType validation
- [ ] Benchmark improvements

---

## Implementation Order

```
Phase 1: DI Simplification
    1.1 Analyze dependencies
    1.2 Create MinimalContainer
    1.3 Update system/Run.goal
    1.4 Test and migrate

Phase 2: Flatter Engine
    2.1 Simplify RunGoal
    2.2 Create ExecuteStep
    2.3 Remove nested methods
    2.4 Test and migrate

Phase 3: Events as Steps
    3.1 Review EventModule
    3.2 Simplify event loading
    3.3 Test

Phase 4: Error Handling
    4.1 Simplify to objects
    4.2 Create Error.goal
    4.3 Test

Phase 5: Lazy Loading (future)
```

---

## Success Metrics

1. **Container.cs** ~916 lines → ~200 lines
2. **Engine.cs** ~1250 lines → ~300 lines
3. **system/Run.goal** contains service registrations
4. **Events** work as steps via EventModule
5. **Errors** are simple objects, Error.goal displays
6. **All existing tests pass**
7. **No breaking changes** to user plang code

---

## Notes

- Keep backward compatibility during transition
- Each phase independently deployable
- Test thoroughly before removing old code
- Document any changes needed in user code
