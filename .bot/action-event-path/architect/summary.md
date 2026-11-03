# action-event-path — the architect's log

Plan: `.bot/action-event-path/architect/plan/start.md`.

## Replaced decisions

| replaced | by | what changed |
|---|---|---|
| — | — | none |

## 2026-11-03 — the cause, and the plan

`before action output.write call Mark` builds the step's moment, because the writer copies an `action module.name` example only when the step's text matches it whole. The fix is teaching only: the rule first, examples that vary. Ingi: "yes" (its own branch).

| # | decision | why | who |
|---|---|---|---|
| 1 | Branch `action-event-path` from runtime2 4fb4d5043, after set-add-order's merge | Ingi: "yes"; the decider pins would conflict if cut before | Ingi, architect |
| 2 | Teaching only (`os/system/modules/on/event.{notes,examples}.md`): the rule first in the Event teaching; the action examples vary (called goals, actions, a comma), plus a third. No C# check | the builder's trace (483dbb7f4); matching the step's words is a no (plang is multi-language) | builder, architect |
| 3 | The line: builder → tester → auditor → docs; the codeanalyzer and security have nothing to review (no C#, no permission surface) | the change's size; on-event-examples went the same way | architect |

Stage status:
| Stage | What | Status |
|---|---|---|
| 1 | The rule first, examples that vary | at the builder |
