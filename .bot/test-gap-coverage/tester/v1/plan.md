# Test Gap Coverage Plan — Tester v1

## Goal
Add PLang integration tests for untested module actions and engine behaviors.

## Prerequisite
- Fix env var `"OpenApiKey"` → `"OPENAI_API_KEY"` in OpenAiService.cs

## Tests to Create
1. **GoalCall** — `goal.call` with parameters and return values
2. **VariableOps** — `variable.exists`, `variable.remove`, `variable.clear`
3. **ContextVars2** — `%!goal.Name%`, `%!step%`, `%!context%`, `%!fileSystem%`, `%!callStack%`
4. **Convert2** — `convert.toDouble`, `convert.toLong`, `convert.toDateTime`
5. **ListOps2** — `list.range`, `list.set`, `list.flatten`
6. **Math2** — `math.random`

## Dropped
- **ErrorHandling2** — LLM builder unreliably generates `onError` property; can't produce stable .pr files for error handling tests

## Verification
- Build from `Tests/App/` root (critical: cwd = rootDirectory)
- Read .pr files to verify step text matches action/module/parameters
- Run `plang p !test` and verify all pass
