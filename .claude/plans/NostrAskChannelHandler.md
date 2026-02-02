# Plan: Nostr-based Ask Channel Handler Test

## Summary
Implemented `while` loop support in PLang's LoopModule and created PLang test files for a Nostr-based ask channel handler that intercepts `ask` requests and routes them through Nostr messaging.

## Files Changed

### C# Implementation
- `PLang/Modules/LoopModule/Program.cs` - Added `While` method and `ReloadConditionVariables` helper
- `PLang.Tests/Modules/LoopModuleWhileTests.cs` - Created unit tests for While loop

### PLang Test Files
- `Tests/Channels/NostrAsk/Start.goal` - Main test entry point
- `Tests/Channels/NostrAsk/events/Events.goal` - Channel handler setup on app start
- `Tests/Channels/NostrAsk/NostrAskHandler.goal` - Nostr ask handler with timeout logic
- `Tests/Channels/NostrAsk/nostr.config` - JSON config with npub target

### Documentation
- `CLAUDE.md` - Created comprehensive development guide
- `.claude/skills/SKILL.md` - Added while loop documentation
- `.claude/skills/references/syntax.md` - Added while loop syntax reference
- `module_history.json` - Created to track module changes

## Key Decisions

1. **While loop over Repeat**: Chose to implement `While` instead of extending `Repeat` because:
   - `Repeat` takes a fixed count, doesn't support early exit based on condition
   - While supports conditional exit needed for "wait until response OR timeout"
   - Using errors for flow control would be hacky

2. **Variable reloading**: The While method reloads variables from `memoryStack` each iteration, allowing the called goal to update condition variables and exit the loop.

3. **Max iterations protection**: Added `maxIterations` parameter (default 10,000) to prevent infinite loops.

4. **Null-safe for testing**: Added null checks for `memoryStack` and `goal` so the method can be unit tested without full DI initialization.

## Lessons Learned

### User Corrections

1. **Example attribute, not Description**: Initially put PLang syntax examples in the `[Description]` attribute. User corrected that `[Example]` attribute should be used instead, which properly maps PLang syntax to parameter values for LLM guidance.

2. **Events.goal only contains Events goal**: Initially put multiple goals (SetupNostrAsk, StartNostrListener, OnNostrMessage) in Events.goal. User corrected that Events.goal should ONLY contain the Events goal with event bindings - any goals it calls must be in separate files.

   Wrong:
   ```csharp
   [Description("Examples: - while %count% < 10...")]
   ```

   Right:
   ```csharp
   [Example("while %count% < 10, call goal Increment",
       @"condition={Kind:Simple, ...}, goalToCall={Name:""Increment""}")]
   ```

2. **CLAUDE.md scope**: Initial CLAUDE.md only covered what was learned in the session. User wanted a comprehensive guide based on exploring the entire project, including Documentation folder and existing skills.

### Technical Discoveries

1. **BaseProgram fields null until Init()**: Fields like `memoryStack`, `goal`, `contextAccessor` are null until `Init()` is called. Unit tests that don't call `Init()` will hit NullReferenceException. Solution: add null checks.

2. **GoalToCallInfo constructor**: Requires name parameter - can't use `new GoalToCallInfo { Name = "X" }`, must use `new GoalToCallInfo("X")`.

3. **TUnit not NUnit**: Test project uses TUnit framework with async assertions (`await Assert.That(x).IsEqualTo(y)`) and `[Before(Test)]` instead of `[SetUp]`.

4. **RuntimeEvent namespace**: Located in `PLang.Events`, not `PLang.Runtime`.

5. **IPseudoRuntime.RunGoal signature**: Has 7 parameters including optional `RuntimeEvent?` - important for mocking in tests.

### Patterns That Worked Well

1. **Record with-expression** for creating modified immutable copies of Condition:
   ```csharp
   return condition with {
       LeftValue = memoryStack.LoadVariables(condition.LeftValue),
       RightValue = memoryStack.LoadVariables(condition.RightValue)
   };
   ```

2. **Recursive helper** for processing compound conditions that may contain nested conditions.

3. **Exploring project first** before writing CLAUDE.md to understand existing patterns and conventions.
