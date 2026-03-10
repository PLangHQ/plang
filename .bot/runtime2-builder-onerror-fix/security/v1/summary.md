# Security Review — v1 Summary

## What this is

Security review of the runtime2-builder-onerror-fix branch, which fixes builder onError dropping, renames RetryOverSeconds→RetryOverMs, strengthens the builder LLM prompt, and adds PLang test suites for error handling.

## What was done

Reviewed all code changes (C# runtime, builder prompt, template, tests, documentation) for security implications. Focused on:

- **Builder prompt injection** — New CRITICAL rules in BuildGoal.llm are the correct defense. LLM prompt injection via step text is inherent to the architecture and accepted in the user-sovereign model.
- **Retry mechanism** — RetryAsync properly handles cancellation and exception chaining. No upper bound on retryCount (low severity, accepted-risk since .pr files are builder output).
- **Error goal execution** — CallErrorGoalAsync properly cleans up error variables in finally block. Exception during error handling is chained, not swallowed.
- **Rename completeness** — Verified zero instances of `RetryOverSeconds` remain in PLang/Runtime2/. End-to-end consistent: ErrorHandler.cs → Methods.cs → GoalMapper.cs → BuildGoal.llm → BuildGoal.goal → template → C# tests → documentation.
- **GoalMapper fix** — Eliminated truncation bug where `RetryDelayInMilliseconds / 1000` lost sub-second precision. Direct passthrough to `RetryOverMs` is correct.

## Key files reviewed

- `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/ErrorHandler.cs` — Clean data class, init-only properties, no deserialization risk
- `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Methods.cs` — RetryAsync, HandleErrorAsync, CallErrorGoalAsync
- `PLang/Runtime2/Engine/Utility/GoalMapper.cs` — MapErrorHandler conversion
- `system/builder/llm/BuildGoal.llm` — Two new CRITICAL rules
- `system/builder/BuildGoal.goal` — Schema rename
- `system/builder/templates/goalFormatForLlm.template` — Template rename

## Verdict

**PASS** — No critical or high severity findings. 3 low-severity items documented as accepted-risk within the user-sovereign threat model. No new attack surface introduced.

## Findings summary

1. **Low: Unbounded retryCount** — No cap on retry iterations. Accepted-risk: requires malicious .pr file, which already implies broader compromise.
2. **Low: Error variable visibility** — Error info available in MemoryStack during error goal. By-design behavior.
3. **Low: Prompt injection via step text** — Inherent to LLM-based builders. New CRITICAL rules are the correct mitigation.
