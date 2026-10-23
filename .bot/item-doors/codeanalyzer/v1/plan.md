# codeanalyzer v1 — item-doors audit (decision 160)

Brief: `.bot/item-doors/architect/briefs/audit.md`. Read only; the output is `.bot/item-doors/codeanalyzer/audit.md`.

1. Read the plan (`test/plan/item-doors/start.md`). Take the architect's log as claims to check.
2. Group the production diff (base 1adfb9555, tip b91e019b5) by stage: 1/1b born-whole, 2 door and pairs, 3 record door and PLNG006, 4 tokens, 5 members, the 145 families, 149/152 list and dict, the late fixes.
3. Per group, run every CLAUDE.md shape smell by name over the added lines. Grep the demolition list to see which plan items were not done.
4. Trace each finding up to its cause, with file:line at each hop. A finding without a chain is dropped.
5. Check that each code-changing decision in the log matches what was built.
6. Give each stage a verdict: keep, fix at the root, or revert. Write audit.md and verdict.json, then commit and push `.bot/item-doors/codeanalyzer/` only.
