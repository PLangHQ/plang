# Tester v2 Plan — HTTP Module Test Quality Analysis

## Context
Coder v2 implemented the HTTP module (4 actions, DefaultHttpProvider, types, config) and refactored HttpHelper into the provider. 54 C# tests + 10 PLang goals. All tests pass (1904/1914, 2 pre-existing DLL failures, 8 skipped).

## What I'll Do

1. **Run full test suite** — done, 1904 passed
2. **Run coverage** — done, DefaultHttpProvider at 5.7% line coverage
3. **Analyze test quality** — the core job:
   - Mock pattern analysis: are mocks hiding real behavior?
   - False green hunting: which tests would pass even with broken code?
   - Edge case coverage gaps
   - The deletion test: which production code has no test coverage?
4. **Check PLang test existence** — 10 goals exist but can't build without LLM
5. **Write test-report.json** with findings and verdict

## Key Finding (Preview)
The entire test suite uses MockHttpProvider which replaces DefaultHttpProvider. This means **984 lines of production code (URL resolution, signing, streaming, response parsing, error handling, progress reporting, upload content resolution) have 0% test coverage**. The tests verify action property passthrough, not actual HTTP behavior.
