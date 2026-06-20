# v5 Changes Applied

## characters/docs/memory/MEMORY.md

**D1 + D3** — Added inline rule: "Examples are docs — OBP and PLang shapes in every code block" → links to new `feedback_examples_are_docs.md`. Covers decomposition in examples (`FirstOrDefault(f => f.path)`) and C# types leaking into PLang shape docs (`IReadOnlyList<file>` → `list<file>`).

**D2** — Added inline rule: "Collection API is always `<concept>.list`, never `<concept>s`." `goal.step.list.start()` not `goal.steps.start()`.

## characters/docs/memory/feedback_examples_are_docs.md (new)

Created. Covers both D1 and D3 — they share a root cause (pattern-matching on C# code just read instead of writing PLang-shaped examples). Includes the exact Ingi quotes from 57c33c2e.
