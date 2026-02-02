---
name: plang-csharp
description: Expert guidance for working on the PLang C# codebase. Use when modifying the compiler/builder, runtime engine, modules, or any C# code in the PLang project. For writing PLang code (.goal files), use the plang-language skill instead.
---

# PLang C# Development

Use this skill for **C# development** on the PLang compiler, runtime, and modules.

For **writing PLang code** (.goal files), use the `plang-language` skill instead.

## Skill Files

- **compiler.md** - Build process, LLM-based compilation, StepBuilder, InstructionBuilder
- **runtime.md** - Engine, ModuleRegistry, PLangContext, PseudoRuntime, MemoryStack
- **references/runtime-internals.md** - Detailed runtime documentation

## Quick Reference

See `CLAUDE.md` in the project root for:
- Quick commands (build, test)
- Module creation patterns
- Testing with TUnit
- Common gotchas
- Adding new functionality checklist

## Key Concepts

### Architecture

```
.goal file → [Builder/LLM] → .pr file (JSON) → [Engine] → Execution
```

### Module Development

All modules inherit from `BaseProgram`. Key attributes:
- `[Description("...")]` - Method documentation
- `[Example("plang syntax", "parameter=value")]` - LLM guidance
- `[HandlesVariable]` - Parameter receives variable name

### Testing

Uses **TUnit** (not NUnit):
- `[Test]` for tests
- `[Before(Test)]` for setup
- `await Assert.That(x).IsEqualTo(y)` - async assertions

### Module History

When adding/modifying module methods, append to `module_history.json`:
```json
{
  "file": "PLang/Modules/XModule/Program.cs",
  "method": "MethodName",
  "datetime": "2025-01-15T14:30:00Z",
  "plan": ".claude/plans/PlanName.md",
  "description": "What was done"
}
```
