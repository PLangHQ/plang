# item-doors — architect's log

The plan: `test/plan/item-doors/start.md`. The branch-A items it gathers came from `.bot/kind-is-type/architect/backlog.md` and the decisions in `.bot/kind-is-type/architect/summary.md` and `.bot/llm-provider/architect/summary.md`. Newest first.

| # | what | rule | detail | who | waits on |
|---|---|---|---|---|---|
| 868 | **#2: one door for writing** (Ingi: "yes, I like that IsLeaf check goes away, agree") | a value writes what it is; the writer decides | `Output` only; the sync `Write` (61) and the `IsLeaf` branch go; the writer carries its output kind (no `string Format`); `Owns` (5 checks) goes; the byte door (844) and the pair registry (846) land here, as core; the json writer's `Value(object)` keeps only primitives. | — | #3 |
| 867 | **the branch, and #1: a type is born whole** (Ingi: "will you create a new branch for this, maybe name it something other than branch A"; "ok, I agree with #1") | the type comes from the list | Branch `item-doors` from runtime2 c47265322. Scope: groups 1–3; groups 4–5 later. #1: the value holds the type that made it; no kind made from a name (`type/this.cs:146–149`); the 29 `Type` overrides, the image's `_mime`, the floor, F3.v3 and F8.1 go; F-SC-1 waits on it. | — | — |
