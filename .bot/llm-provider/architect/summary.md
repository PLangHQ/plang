# llm-provider — architect's log

The plan: `test/plan/llm-provider/start.md`. The decisions before this branch existed (844–852, how a value reaches an llm and the provider design so far) are in `.bot/kind-is-type/architect/summary.md`. New decisions go here, newest first.

| # | what | rule | detail | who | waits on |
|---|---|---|---|---|---|
| 853 | **the branch** (Ingi, 2026-10-17: "create a new branch and start writing up a new plan there, we are not done, just what you have so you dont mix it with kind-as-type") | one branch per change | Cut from llm-writer (feaa3a7cd); first commit reverts llm-writer's old stage 3 (b368e6901), so it starts from OpenAi as 819 left it. llm-writer doesn't move. The plan holds the rulings so far (851, 852) and the open pieces. | — | Ingi: the open pieces |
