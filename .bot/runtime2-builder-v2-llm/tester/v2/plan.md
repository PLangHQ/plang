# Tester v2 Plan — LLM Module

## Scope
Test quality analysis for coder v2's LLM module: `query.cs`, `LlmMessage.cs`, `ToolCall.cs`, `ILlmProvider.cs`, `OpenAiProvider.cs` (~847 lines).

## Steps
1. Run full C# test suite — verify 1958 pass
2. Run Cobertura coverage on LLM module files
3. Read all 10 test files + source — hunt false greens
4. Apply deletion test, weak-assertion scan, mock-level check
5. Write `test-report.json` to branch root
6. Write `verdict.json`, `summary.md`, session report
7. Commit + push
