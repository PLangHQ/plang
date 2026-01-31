# Bootstrap Dependencies Analysis

## Minimum Required for Bootstrap

These are needed to run `system/Run.goal` which registers other services.

### Layer 1: File System (no dependencies)
```
IPLangFileSystem / PLangFileSystem
IPLangFileSystemFactory / PlangFileSystemFactory
```

### Layer 2: Logging (depends on Layer 1)
```
ILogger / Logger
```

### Layer 3: Context & Memory (depends on Layer 1, 2)
```
PLangAppContext
IPLangContextAccessor / ContextAccessor
IMemoryStackAccessor / MemoryStackAccessor
MemoryStack
```

### Layer 4: Parsing (depends on Layer 1, 2, 3)
```
PrParser
DependancyHelper (assembly loading)
```

### Layer 5: Type Utilities (depends on Layer 1, 2)
```
ITypeHelper / TypeHelper
```

### Layer 6: Security (depends on Layer 1, 2, 3)
```
IFileAccessHandler / FileAccessHandler
```

### Layer 7: Engine (depends on all above)
```
IEngine / Engine
IPseudoRuntime / PseudoRuntime
```

### Layer 8: Bootstrap Modules
```
InjectModule.Program - for registering services
CallGoalModule.Program - for calling goals (depends on IPseudoRuntime)
```

---

## Services to Move to Plang Registration

These will be registered via `system/Run.goal` using InjectModule:

| Service | Interface | Default Implementation |
|---------|-----------|----------------------|
| Database | `IDbConnection` | `SqliteConnection` |
| LLM | `ILlmService` | `PLangLlmService` |
| Caching | `IAppCache` | `InMemoryCaching` |
| Settings Repository | `ISettingsRepository` | `SqliteSettingsRepository` |
| Archiver | `IArchiver` | `Zip` |
| Encryption | `IEncryption` | `Encryption` |
| Identity | `IPLangIdentityService` | `PLangIdentityService` |
| Signing | `IPLangSigningService` | `PLangSigningService` |
| Events | `IEventRuntime` | `EventRuntime` |
| Builder | `IBuilder` | `Builder` |
| Error Handler | `IErrorHandlerFactory` | `ConsoleErrorHandler` |

---

## Dependency Chain Issue: ISettings

`ISettings` depends on `ISettingsRepository`:

```csharp
container.RegisterSingleton<ISettings, Settings>();
// Settings constructor needs ISettingsRepository
```

**Options:**
1. Register ISettings in bootstrap with a minimal/null settings repository
2. Register ISettingsRepository in bootstrap (simple in-memory version)
3. Delay ISettings usage until after plang registration

**Recommendation:** Option 2 - Have a minimal bootstrap settings repository

---

## Current Registration Flow

```
RegisterForPLangConsole
  ├── RegisterBaseForPLang (PLangAppContext, FileSystem, Logger, PrParser)
  ├── RegisterModules (all 50+ modules via reflection)
  ├── RegisterForPLang (Engine, Memory, Services, etc.)
  ├── Set output sinks
  ├── RegisterErrorHandlerFactory
  └── RegisterEventRuntime
```

## New Bootstrap Flow

```
MinimalContainer.RegisterBootstrap
  ├── FileSystem
  ├── Logger (minimal)
  ├── Context & Memory
  ├── PrParser
  ├── TypeHelper
  ├── FileAccessHandler
  ├── Engine (minimal)
  ├── PseudoRuntime
  └── Bootstrap Modules (InjectModule, CallGoalModule)

Then: Run system/Run.goal
  ├── inject db, ...
  ├── inject llm, ...
  ├── inject caching, ...
  ├── inject settings, ...
  ├── ... other services
  └── call %goalName% %parameters%
```

---

## Module Dependencies

### InjectModule.Program
- Only uses `BaseProgram.RegisterForPLangUserInjections`
- Minimal dependencies

### CallGoalModule.Program
Constructor needs:
- `IPseudoRuntime`
- `IEngine`
- `PrParser`
- `IPLangContextAccessor`

### PseudoRuntime
Constructor needs:
- `IPLangFileSystem`
- `PrParser`
- `ILogger`

---

## Open Questions

1. **GoalParser vs PrParser**: Do we need GoalParser for bootstrap?
   - PrParser reads compiled .pr files
   - GoalParser reads .goal files
   - For runtime bootstrap, only PrParser needed (assuming system/Run.goal is pre-compiled)

2. **Output during bootstrap**: If something fails during bootstrap, how do we show errors?
   - Option: Use Console.WriteLine directly
   - Option: Include minimal OutputModule

3. **Settings during bootstrap**: Some services need settings
   - Option: In-memory settings for bootstrap
   - Option: Delay settings-dependent services

4. **Module auto-discovery**: Currently uses reflection to find all modules
   - Keep for now (simplicity)
   - Later: Lazy load based on PR file ModuleType
