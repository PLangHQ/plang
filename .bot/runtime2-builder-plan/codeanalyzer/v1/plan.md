# v1 Plan: Full 5-Pass Analysis of runtime2-builder-plan

## Scope

This branch is massive — ~200 changed files across the full stack. Key themes:

1. **Data<T> composition pattern** — Goal, Step, Action no longer inherit from Data. They implement IDataWrappable instead. Action handler properties wrapped in Data<T>.
2. **Return removal** — Action.Return removed. Results stored as %__data__%, variable.set replaces return.
3. **Condition orchestration** — condition.if now handles if/elseif/else as multiple actions in a single step.
4. **Foreach as inline body** — foreach no longer calls a goal; it runs remaining actions in the step as loop body.
5. **Builder improvements** — validateResponse, promoteGroups, Levenshtein suggestions, IBuildValidatable, Data<T> unwrapping for LLM type names.
6. **Data.Compare** — New structural JSON comparison for eval suite.
7. **Source generator** — Data<T> property support, IStatic support, field reset per execution, __ResolveData.
8. **New modules** — timer (start/end), list (any/group), IStatic, IDataWrappable.
9. **Variables** — ResolveDeep depth guard, Data event system, bracket indexing in SetValueOnObject.
10. **Debug** — Variable watching with events, LLM trace, resolve trace, verbose mode.

## Analysis Plan

5-pass analysis on all changed runtime code (excluding .pr files and test fixtures):
- Pass 1: OBP compliance
- Pass 2: Simplification
- Pass 3: Readability
- Pass 4: Behavioral reasoning (what breaks silently?)
- Pass 5: Deletion test (if I deleted this, would a test fail?)
