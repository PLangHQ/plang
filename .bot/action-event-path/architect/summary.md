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

| 4 | **Shipped short of done-when** (builder e54b8dcab, 6f952c362): the action wordings 6/27 → 17/27, Mark 0/5 → 3/5 (binding output.write's start), `after step` 1/3 → 3/3, the app's start 3/3 as before, `before each goal` 0/3 as before (the same cause, older). Naming the wrong forms in the notes gave 14/27 and was dropped. One pin re-pinned (`TheStageOneRequest_IsThePinnedOne`, the on module's e.g. lines). No further round: the wording has reached its ceiling, and the remaining lever is structural (the decider chooses whose moment it is), on the backlog for Ingi. doc/events.md keeps no output (3/5 isn't proof) | a strict improvement, nothing regressed | architect |

Stage status:
| Stage | What | Status |
|---|---|---|
| 1 | The rule first, examples that vary | at the tester |
