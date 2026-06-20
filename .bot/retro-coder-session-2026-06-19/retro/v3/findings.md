# v3 Findings — Coder sessions 2026-06-16

## Kept (new — not in SC1–SC11)

### F1 — Verify the condition before acting on a procedural memory note
- **Category:** self-correction + wrong-doc
- **Session:** d9ecbe3a, 2026-06-16T17:59
- **Quote:** "My `rm -rf` was stale habit from an out-of-date memory note, not a real need. That habit is now deleted."
- **Trigger:** Bot followed a memory note to delete `Fixtures/pr/.db` — but `TestApp.Create` migration had already fixed the underlying bug in a prior session. The note was stale.
- **Resolution:** Bot ran the tests, discovered no `.db` file is created anymore, corrected the memory note.
- **Lesson:** Memory notes that instruct a concrete action ("rm X", "restart Y", "always delete Z") can go stale when the underlying condition is fixed. Before executing a destructive procedural note, verify the condition still holds. Don't treat "rm" instructions in memory as permanent facts.
- **Doc target:** characters/coder/memory/MEMORY.md (SC12)

## Dropped (duplicates or too specific)

- 3ecf7b5b "I broke 7 tests proving it" → SC1 (verify before asserting to Ingi)
- 3ecf7b5b "had it backwards, trace the flow" → `feedback_code_tracing_procedure.md` already covers this
- 3ecf7b5b "conflated two maps (extension→family vs parse target)" → domain knowledge, not a habit
- d9ecbe3a stash/clean churn → SC11
- d9ecbe3a disposal hypothesis refuted → one-off investigation, no general pattern
- d9ecbe3a "`as int` cast semantics" → too specific to one area
- 8ead10a3 "`app` not Engine" → `feedback_app_is_not_engine.md` already in MEMORY.md
- 8ead10a3 "arrow direction wrong in diagram" → one-off
- 8ead10a3 "stale test for nested Data" → specific test cleanup, not a habit
