# Learnings — codeanalyzer v3, runtime2-setup-goal

## Learning 1: Trace the actor context, not just the object graph

When a feature registers something on a specific actor's MemoryStack, verify that the execution path actually uses that actor's context. PLang has three actors (System, Service, User), each with their own PLangContext and MemoryStack. `engine.Context` defaults to User.Context (Engine/this.cs:145), and `RunGoalAsync` defaults to `User.Context` when no context is passed (Engine/this.cs:209, 278). A feature registered on System's MemoryStack is invisible to normal PLang step execution.

**Pattern to watch for:** Any code that puts data on `engine.System.Context.MemoryStack` and expects PLang code to read it via `%VarName%`. The LazyParamsGenerator resolves against the context passed through the execution chain, which is User.Context.

## Learning 2: Tests that use the wrong actor mask wiring bugs

When all tests for a MemoryStack-registered feature use `_engine.System.Context.MemoryStack`, they prove the feature works in isolation but don't prove it's reachable. The critical test is: resolve the variable through `engine.Context.MemoryStack` (which is User's). If this test doesn't exist, the wiring gap is untested.

**Rule of thumb:** For any MemoryStack-registered feature, at least one test must resolve through the same path that PLang code uses — `engine.Context.MemoryStack` or `User.Context.MemoryStack`.

## Learning 3: Action handlers vs MemoryStack bridge are different resolution paths

Settings action handlers (`get settings 'key'`) access `Context.Engine.System.DataSource` directly — they navigate the object graph. The `%Settings.Key%` bridge goes through MemoryStack resolution via LazyParamsGenerator. These are two completely different paths. One working doesn't prove the other works.
