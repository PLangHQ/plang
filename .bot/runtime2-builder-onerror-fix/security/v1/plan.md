# Security Review Plan — v1

## Scope

Security review of runtime2-builder-onerror-fix branch vs runtime2. Changes include:

1. **RetryOverSeconds → RetryOverMs rename** — ErrorHandler.cs, Methods.cs, GoalMapper.cs, C# tests, BuildGoal.llm, BuildGoal.goal, template, pr-file-format.md
2. **Builder LLM prompt strengthening** — Two new CRITICAL rules in BuildGoal.llm (onError preservation, literal value preservation)
3. **New PLang test suites** — ErrorRetryOnly, ErrorGoalFirst, ErrorMixed, OnErrorMultilingual, plus test file updates

## Analysis Areas

1. **Builder prompt (BuildGoal.llm)** — Prompt injection vectors, consistency of onError schema, potential for LLM to misinterpret new rules
2. **ErrorHandler.cs** — Deserialization safety, type correctness of renamed property
3. **GoalMapper.cs** — Conversion correctness (eliminated /1000 truncation bug), null safety
4. **Methods.cs (RetryAsync, HandleErrorAsync, CallErrorGoalAsync)** — Resource exhaustion via retryCount, error variable lifecycle, exception handling completeness
5. **Template (goalFormatForLlm.template)** — Consistent rename in template output
6. **End-to-end rename completeness** — No stale RetryOverSeconds references remaining

## Approach

- Blue team: Map attack surface of error handling flow
- Red team: Attempt to find exploitable vectors in retry mechanism, error goal execution, and builder prompt
- Verify rename is complete across all layers
