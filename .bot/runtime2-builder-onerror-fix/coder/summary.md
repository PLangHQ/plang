# Coder Summary — runtime2-builder-onerror-fix

## v1
Fixed two codeanalyzer findings: (1) replaced stale `retryOverSeconds` with `retryOverMs` in builder .pr files to align with already-correct source, and (2) restructured ErrorRetryOnly and ErrorGoalFirst PLang tests to actually verify retry behavior by adding attempt counters and moving retry from throw steps to call steps. Tests need LLM rebuild (no API key in env). See [v1/summary.md](v1/summary.md).
