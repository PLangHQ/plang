# Review of v1 Findings

All 3 medium findings from v1 were addressed by the coder:

1. **PrPath backslash** — Fixed with `.AdjustPathToOs()` and `/` default. Tests updated to use AdjustPathToOs on both inputs and expected values.
2. **GoalCall parameter pollution** — Parameters moved after successful file load. Correct fix.
3. **Newtonsoft in CommandLineParser** — Migrated to `System.Text.Json`. `JToken.Parse` → `JsonDocument.Parse`, type switch rewritten.
4. **Bare catches** — All 5 narrowed: `ArgumentException` for regex, `MissingMethodException` for Activator, `JsonException` for JSON x2, `ArgumentException` for enum.
5. **Step.RunAsync catch** — Filter added: `when (ex is not (OutOfMemoryException or StackOverflowException))`. NRE kept in catch — deliberate choice for user-facing error reporting.

The STJ migration in CommandLineParser introduced a behavioral regression in Executor.cs — see v2 findings.
