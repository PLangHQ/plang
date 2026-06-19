# v2 Findings — Coder sessions 2026-06-18 (93456b3b, efe53299)

## Kept findings (new — not already in SC1–SC8)

### F1 — Dispatchers dispatch; construction belongs in the type family
- **Category:** self-correction
- **Session:** 93456b3b, 2026-06-18T15:42:57
- **Quote:** "You're right, and it's a real OBP smell — not the dispatch, but the *construction* logic. `Lift` should move construction to families, not do it in `Data.Lift`."
- **Trigger:** Ingi pointed out `Data.Lift` doing value-sequence construction for collections (IEnumerable dispatch in data/this.cs)
- **Resolution:** Bot moved construction logic to `list/this.cs` constructor, leaving `Lift` as thin dispatcher
- **Lesson:** When a dispatcher (Lift, route, forward) contains construction logic, that's an OBP violation. The type family owns construction; the dispatcher only routes to it. If you find yourself `new List(...)` inside a dispatch switch, push it into the type's own ctor/factory.
- **Doc target:** characters/coder/memory/MEMORY.md

### F2 — Fix the test, don't bend the runtime
- **Category:** self-correction
- **Session:** 93456b3b, 2026-06-18T16:58:58
- **Quote:** "I'll go with that: the test is wrong, fix the test, don't bend runtime to accept mis-authored values."
- **Trigger:** Ingi: "I have in general a problem how tests are creating types" — test was passing a mis-authored variable-name string as bare text to `Data.Lift`
- **Resolution:** Bot fixed test to properly author the variable, removed permissive fallback from runtime
- **Lesson:** When a test fails because it's passing bad input, fix the test authoring — don't add permissive fallbacks to runtime code so that bad inputs pass. Tests model correct usage. Runtime must not be weakened to accommodate a broken test.
- **Doc target:** characters/coder/memory/MEMORY.md

### F3 — Establish a clean baseline before making changes
- **Category:** frustration + self-correction
- **Session:** efe53299, 2026-06-18T14:47:43
- **Quote:** "You're right — I was churning. Here's the full picture so we decide together."
- **Trigger:** Ingi called out repeated stash/unstash and rebuild cycles without clarity on which failures were pre-existing vs. regressions
- **Resolution:** Stopped. Established clean baseline (pre-existing failures vs. new regressions) before proceeding.
- **Lesson:** Before making any further changes when tests are failing, stop and establish which failures are pre-existing and which are new regressions. Repeated stash/unstash cycles signal that the bot lost its bearings — stop, surface the full picture to Ingi, agree on a baseline.
- **Doc target:** characters/coder/memory/MEMORY.md

## Dropped findings (not new or too specific)

- **efe53299 OBP smell #3 (three copies):** Overlaps with SC6 (allocate-then-transform) and OBP smell checklist item #3 already in CLAUDE.md.
- **efe53299 frustration "slow down":** Overlaps with F3 (same session, same pattern — post-hoc construction also covered by F1).
- **efe53299 stale Slot doc-comment:** One-off, too file-specific. Not a pattern.
- **efe53299 stale AsT_* test names:** Test-naming hygiene, not a teachable coder pattern.
- **93456b3b frustration "try again":** Too terse to extract a lesson; no context on what the fix should have been.
