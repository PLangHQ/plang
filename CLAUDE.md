# CLAUDE.md - PLang Development Guide

Quick reference for developing on the PLang codebase. For PLang language syntax, see `.claude/skills/SKILL.md`. For runtime internals, see `.claude/skills/plang-runtime.md`.

## Project Overview

PLang is a natural language programming language that compiles `.goal` files into executable JSON instructions (`.pr` files) using LLM-powered code generation, then executes them via reflection-based C# runtime.

```
.goal files → [LLM Builder] → .pr files → [Runtime Engine] → Execution
```

## Quick Commands

```bash
# Build
dotnet build PLang/PLang.csproj

# Run tests
dotnet test PLang.Tests/PLang.Tests.csproj

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName"

# Build PLang app
plang build

# Run PLang app
plang run
```

## Project Structure

```
PLang/                    # Core library (net10.0)
├── Modules/              # 49 built-in modules (the heart of PLang)
│   ├── BaseProgram.cs    # Base class for ALL modules
│   ├── BaseBuilder.cs    # Base class for module builders
│   └── */Program.cs      # Individual modules
├── Runtime/              # Execution engine
│   ├── Engine.cs         # Main execution (runs goals/steps)
│   ├── PseudoRuntime.cs  # Goal invocation helper
│   └── MemoryStack.cs    # Variable storage
├── Building/             # Compiler system
│   ├── Builder.cs        # Main build orchestrator
│   ├── StepBuilder.cs    # Compiles steps to instructions
│   └── Model/            # Build-time models
├── Interfaces/           # Public contracts
│   └── PLangContext.cs   # Per-request context
├── Errors/               # Error types
└── Services/             # LLM, Output, etc.

PLang.Tests/              # TUnit test project
├── Modules/              # Module tests
└── Runtime/              # Engine tests
```

## Creating/Modifying Modules

### Module Structure

Every module has `Program.cs` inheriting from `BaseProgram`:

```csharp
namespace PLang.Modules.MyModule
{
    [Description("What this module does")]
    public class Program : BaseProgram
    {
        public Program(ILogger logger, /* dependencies */) : base() { }

        // All public async Task methods become callable from PLang
        [Description("What this method does")]
        [Example("plang syntax here", @"parameter=value, other=%var%")]
        public async Task<IError?> DoSomething(string input, int count = 10)
        {
            // Implementation
            return null; // null = success
        }
    }
}
```

### Key Rules for Module Methods

1. **Must be `public async Task`** - Returns `Task`, `Task<IError?>`, or `Task<(T, IError?)>`
2. **Use attributes** for LLM guidance:
   - `[Description("...")]` - Explains the method
   - `[Example("plang syntax", "parameter mappings")]` - Shows usage
   - `[HandlesVariable]` - Parameter receives variable name, not value
3. **Access base class fields** (set via `Init()`):
   - `memoryStack` - Variable storage
   - `goal` - Current goal
   - `goalStep` - Current step
   - `engine` - Execution engine
   - `fileSystem` - Safe file operations

### Example Attribute Format

```csharp
[Example("while %count% < 10, call goal Increment",
    @"condition={Kind:Simple, LeftValue:%count%, Operator:""<"", RightValue:10}, goalToCall={Name:""Increment""}")]
```

Format: `[Example("PLang step text", "parameter=value, param2=value2")]`

### Return Patterns

```csharp
// Simple success/error
public async Task<IError?> DoWork()
{
    if (failed) return new ProgramError("message");
    return null;
}

// Return value + error
public async Task<(string?, IError?)> GetData()
{
    return ("result", null);
}

// Return value + error + properties
public async Task<(Table?, IError?, Properties?)> Query()
{
    return (table, null, new Properties { /* metadata */ });
}
```

## Testing

### Framework: TUnit (not NUnit/xUnit)

```csharp
using NSubstitute;

namespace PLang.Tests.Modules;

public class MyModuleTests
{
    [Before(Test)]  // Setup - NOT [SetUp]
    public void Setup() { }

    [After(Test)]   // Cleanup - NOT [TearDown]
    public void Cleanup() { }

    [Test]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        var mock = Substitute.For<IEngine>();

        // Act
        var result = await something.DoWork();

        // Assert - MUST await
        await Assert.That(result).IsNull();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Message).Contains("text");
        await Assert.That(list).IsEmpty();
    }
}
```

### Mocking IPseudoRuntime.RunGoal

```csharp
pseudoRuntime.RunGoal(
    Arg.Any<IEngine>(),
    Arg.Any<IPLangContextAccessor>(),
    Arg.Any<string>(),           // appPath
    Arg.Any<GoalToCallInfo>(),
    Arg.Any<Goal>(),
    Arg.Any<int>(),              // indent
    Arg.Any<RuntimeEvent?>())
    .Returns(Task.FromResult<(IEngine, object?, IError?)>((engine, null, null)));
```

## Common Gotchas

### BaseProgram Fields Null Until Init()

Fields like `memoryStack`, `goal`, `contextAccessor` are null until `Init()` is called. Unit tests that don't call `Init()` will hit NullReferenceException.

**Solution**: Add null checks:
```csharp
if (memoryStack == null) return condition; // Handle test scenario
```

### GoalToCallInfo Requires Constructor Parameter

```csharp
// WRONG - compile error
new GoalToCallInfo { Name = "Test" }

// RIGHT
new GoalToCallInfo("Test")
```

### Namespace Locations

| Class | Namespace |
|-------|-----------|
| `RuntimeEvent` | `PLang.Events` |
| `ProgramError` | `PLang.Errors.Runtime` |
| `Condition` | `PLang.Modules.ConditionalModule.ConditionEvaluator` |
| `GoalToCallInfo` | `PLang.Models` |

### PLang Syntax (for tests/examples)

```plang
- call goal GoalName           # Correct (not "call !GoalName")
- while %x% < 10, call goal Y  # While loop syntax
- if %x% is empty, throw error 'msg'  # Inline if/throw
- remove cache 'key'           # Clear cache (not "cache '' ...")
```

## Key Architectural Patterns

### Execution Flow

```
Engine.RunGoal()
  → Load .pr file (JSON instruction)
  → BaseProgram.Run()
    → RunFunctionInternal()
      → methodHelper.GetMethod()
      → method.FastInvoke()
  → Store result in MemoryStack
```

### Variable Resolution

```csharp
// In your module method, variables are already resolved
// But if you need to load variables manually:
var value = memoryStack.LoadVariables(someObject);
var specific = memoryStack.Get("variableName");
memoryStack.Put("variableName", value);
```

### Error Handling

```csharp
// Return errors, don't throw
return new ProgramError("message", goalStep);
return new StepError("message", goalStep, "ErrorKey", 400);

// For ask-user scenarios
return new AskUserError("question", callback);
```

### Copy-on-Write ModuleRegistry

Cloned per-request for isolation. O(1) cloning via shared collections:
```csharp
var cloned = engine.CloneDefaultModuleRegistry();
cloned.Disable("http");  // Only affects this request
```

## File Conventions

| Type | Convention | Example |
|------|------------|---------|
| Goal files | PascalCase.goal | `CreateUser.goal` |
| Module folders | PascalCaseModule | `LoopModule/` |
| Test files | ClassNameTests.cs | `LoopModuleWhileTests.cs` |
| System folders | lowercase | `events/`, `setup/` |

## Critical: Setup vs Start

- **Setup.goal**: One-time initialization, each step tracked in system.sqlite
- **Start.goal**: Entry point when no goal specified. SKIPPED if you run `plang MyGoal`

## Important: PLang Runtime Errors

**When you encounter errors running PLang code, ASK the user before changing any C# code.**

The error might be:
- A bug in the PLang code you wrote
- A bug in the C# runtime that needs investigation
- Expected behavior that needs clarification

Don't assume the fix - consult with the user first to determine if it's a PLang code issue or a C# runtime issue that needs fixing.

## Important: Never Delete .build Folders

**NEVER delete or remove `.build` folders.** The PLang builder handles build artifact management automatically.

If something isn't in the `.build` folder that you expected:
- ASK the user before taking any action
- The issue might be a build configuration problem
- The issue might require rebuilding with specific flags
- The user can guide you on the correct approach

Do not use `rm -rf .build` or similar commands.

## Adding New Functionality Checklist

1. [ ] Add method to appropriate `Modules/*/Program.cs`
2. [ ] Add `[Description]` attribute
3. [ ] Add `[Example]` attributes with PLang syntax and parameter mappings
4. [ ] Handle null `memoryStack`/`goal` for testability
5. [ ] Return `IError?` or tuple with error
6. [ ] Add unit tests in `PLang.Tests/Modules/`
7. [ ] Run `dotnet test` to verify
8. [ ] **Append to `module_history.json`** (see below)

## Module History Tracking

When you modify or add a method to any module, append an entry to `module_history.json` in the project root:

```json
[
  {
    "file": "PLang/Modules/LoopModule/Program.cs",
    "method": "While",
    "datetime": "2024-01-15T14:30:00Z",
    "plan": ".claude/plans/NostrAskChannelHandler.md",
    "description": "Added while loop support for condition-based iteration"
  }
]
```

Fields:
- `file` - Path to the modified module file
- `method` - Name of the method added/modified
- `datetime` - ISO 8601 timestamp
- `plan` - Relative path to the plan file that requested this change
- `description` - Short description of what was done (for another CLI to pick up)

This file is consumed by another Claude Code CLI instance for tracking and coordination.

## Plan Documentation

After executing a plan, create a markdown file at `.claude/plans/{PlanName}.md` documenting:

1. **What was done** - Summary of implemented changes
2. **Files modified/created** - List of affected files
3. **Key decisions** - Important choices made during implementation
4. **Lessons learned** - Issues discovered, user corrections, and insights gained

Example structure:
```markdown
# Plan: {PlanName}

## Summary
Brief description of what was implemented.

## Files Changed
- `path/to/file.cs` - Added X method
- `path/to/test.cs` - Created unit tests

## Key Decisions
- Decision 1 and why
- Decision 2 and why

## Lessons Learned
- Issue discovered and how it was resolved
- User correction: what was wrong and the fix
- Pattern that worked well
```

This builds institutional knowledge for future development sessions.

## Useful Base Class Members

```csharp
// From BaseProgram (available in all modules)
protected MemoryStack memoryStack;      // Variable storage
protected Goal goal;                     // Current goal
protected GoalStep goalStep;            // Current step
protected IEngine engine;               // Execution engine
protected IPLangFileSystem fileSystem;  // Safe file access
protected ISettings settings;           // App settings
protected ILogger logger;               // Logging
protected PLangContext context;         // Request context

// Helper methods
protected string GetPath(string? path)      // Resolve relative paths
protected (T?, IError?) Module<T>()         // Get another module
```

## Documentation References

### PLang Language (writing .goal files)
- `.claude/skills/plang-language/SKILL.md` - Main PLang syntax guide
- `.claude/skills/plang-language/references/syntax.md` - Complete syntax reference
- `.claude/skills/plang-language/references/database.md` - Database patterns
- `.claude/skills/plang-language/references/patterns-and-conventions.md` - Best practices
- `.claude/skills/plang-language/references/error-handling.md` - Error handling
- `.claude/skills/plang-language/references/user-interface.md` - UI development
- `.claude/skills/plang-language/references/security.md` - Security patterns
- `.claude/skills/plang-language/references/project-structure.md` - Project organization
- `.claude/skills/plang-language/references/modules.md` - Built-in modules

### PLang C# Development (compiler/runtime)
- `.claude/skills/plang-csharp/SKILL.md` - C# development overview
- `.claude/skills/plang-csharp/compiler.md` - Build process, LLM compilation
- `.claude/skills/plang-csharp/runtime.md` - Engine, ModuleRegistry, MemoryStack
- `.claude/skills/plang-csharp/references/runtime-internals.md` - Detailed runtime docs

### Other
- `Documentation/` - User-facing docs and tutorials
