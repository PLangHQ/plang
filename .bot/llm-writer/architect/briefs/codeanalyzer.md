# codeanalyzer: llm-writer (as the coder)

**Reassigned (Ingi, 2026-10-15): "let get tester to do it".** The codeanalyzer wasn't reachable, so the tester (plang-f2) builds this branch; read "you" below as the tester.

Traces to: decisions 791 and 793 in `.bot/kind-is-type/architect/summary.md`; the plan is `test/plan/llm-writer/start.md`, its index `start.goal`.

## Why you

Ingi (2026-10-15): "lets give to codeanalyzer, so how he does, ask him to ask coder for tips". You build this branch end to end, as a coder would: tests first, then the code, each stage's shape to the architect before it's built. The kind-is-type coder ("Review goal-graph-singular pull request and trace code") has been through the type system and the writers this month: ask it for tips, especially on how values write themselves and where the llm module renders a message.

## In plang

```plang
Build the llm-writer branch from its plan (decisions 791, 793)
Start
- work in /workspace/llm-writer on branch llm-writer (made from runtime2 cf66e2d01)
- read test/plan/llm-writer/start.md and start.goal, and the standing rules: CLAUDE.md, Documentation/v0.2/obp-smells.md, "a value writes what it is; the writer decides how it looks"
- ask the kind-is-type coder for tips before stage 0: where a value becomes llm message text, how a writer is chosen, the pitfalls it hit
- stage 0: trace where a value becomes LLM message text today, and send the shape of stages 1–4 to the architect
- for each stage: write its tests first (red for their own reason), then the code, then the gate; commit and push at the end of each stage
LIMITS: the writer is the llm module's; nothing added to a locked core surface without the architect (who takes it to Ingi); never hand-edit a .pr; no System.IO or Console in production C#
DONE WHEN: stages 1–4 are in, each green, pushed
REPLY: to the architect ("Review architect module owns action documentation"), Done/HASH/CHECK/OPEN, text first
WHY: a value put into an LLM message can blow the call; the writer decides how much of it the LLM sees
```
