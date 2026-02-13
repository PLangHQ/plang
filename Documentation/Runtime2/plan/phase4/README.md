# Phase 4: Builder Self-Hosting

**Goal:** Make the builder run natively on Runtime2.

**Depends on:** Phase 2 (HTTP, template), Phase 3 (settings for API keys), Phase 1 (foreach, error handling, list ops)

## Sections

| # | Title | Status |
|---|-------|--------|
| [4.1](4.1-llm-module.md) | LLM Module (Written in PLang) | NOT STARTED |
| [4.2](4.2-builder-recompilation.md) | Builder Goal Recompilation | NOT STARTED |
| [4.3](4.3-simplify-v1-bridge.md) | Simplify V1 Bridge | NOT STARTED |

## Tests
- LLM module: mock HTTP responses for unit tests, real API for integration
- Builder: recompile and run sample .goal files, compare .pr output
