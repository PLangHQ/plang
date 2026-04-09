# Auditor v1 Summary — LLM Module

## What this is

Cross-cutting integrity audit of the LLM module (Piece 7: query action, OpenAiProvider, GoalCall additions). All three previous reviewers approved. This audit focuses on the spaces between their reviews.

## What was done

### Reviews assessed
- **Codeanalyzer v2**: Agree — thorough file-level analysis, nothing missed
- **Tester v3**: Partial — good coverage work but missed MaxToolCalls behavioral bug and numeric boxing inconsistency
- **Security v1**: Agree — correct threat model, appropriate severity ratings

### Cross-file contracts: CLEAN
- GoalCall's new Description/Parallel properties are immutable (init), no clone/copy family, GoalMapper correctly doesn't map them (old building model), serialization tested
- Provider registration in Engine/Providers/this.cs is correct
- LlmMessage's [Store]/[LlmBuilder] attribute design is intentional — ToolCallId/ToolCalls correctly excluded from builder serialization

### Findings (2 major, 2 minor, 1 nit)

**Major #1 — MaxToolCalls batch overshoot (OpenAiProvider.cs:206)**
When the LLM returns N tool calls in one response, ALL N execute before the limit check. MaxToolCalls=3 + 5 tools in one response = 5 goals executed, but only 3 results appended. Side-effects from extra tools can't be undone.

**Major #2 — Silent empty result on loop exit (OpenAiProvider.cs:345)**
When MaxToolCalls is exhausted, `return Data.Ok()` gives the caller empty data, no error, no metadata. Any content from the last LLM response is discarded. The user has no indication their query partially succeeded.

**Minor #3 — Numeric boxing inconsistency (OpenAiProvider.cs:795 vs 436)**
RestoreFromCache uses TryGetInt32, ParseToolArguments uses TryGetInt64. Low practical impact but inconsistent.

**Minor #4 — MaxToolCalls test too loose (QueryToolTests.cs:298)**
`IsGreaterThanOrEqualTo(2) && IsLessThanOrEqualTo(4)` allows a 3x range. Should be exact.

**Nit #5 — Redundant null ternary (OpenAiProvider.cs:148)**

## Verdict: FAIL

Send back to coder for fixes on findings #1 and #2. These are behavioral correctness issues, not style.
