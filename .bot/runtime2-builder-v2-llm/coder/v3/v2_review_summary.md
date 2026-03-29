# Tester v2 Review of Coder v2

## Source
Tester v2 (`tester/v2/summary.md`, `test-report.json`). Coverage: OpenAiProvider 82.8% line / 61.5% branch.

## Verdict: FAIL — 4 major, 4 minor

### Major
1. **ProviderNotRegistered tests opposite** — Verifies provider IS registered, never tests missing provider.
2. **MaxToolCalls unverified** — Only asserts `result.IsNotNull()`, loop could be infinite.
3. **API error assertions weak** — Only `Success==false`, no Error.Key or message checks.
4. **OnToolCall callback unverified** — No proof callback fires, only checks final result.

### Minor
5. **ParseToolArguments** — True/False/Null/fallback branches untested (only String).
6. **ResolveImage file path** — 0% coverage on file read + mime type detection.
7. **RestoreFromCache** — Both deserialization branches at 0%.
8. **Parallel execution** — No assertion on actual concurrency vs sequential.
