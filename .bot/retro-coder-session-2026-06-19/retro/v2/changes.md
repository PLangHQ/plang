# v2 Changes Applied

## CLAUDE.md

Added OBP smell #9 (SC9, F1): "Dispatcher contains construction logic." After item #8, before the "If removing one line..." closing note.

## characters/coder/memory/MEMORY.md

Added three bullets under "Coder discipline" (after SC6/SC7 block, before Smell #8):

- **SC9 (F1)** — Dispatchers dispatch; construction belongs in the type family.
  Backed by: `93456b3b`, 2026-06-18T15:42, bot catching `Data.Lift` doing IEnumerable construction.

- **SC10 (F2)** — Fix the test, don't bend the runtime.
  Backed by: `93456b3b`, 2026-06-18T16:58, Ingi: "problem how tests are creating types" — bot removed permissive fallback.

- **SC11 (F3)** — Establish a clean baseline before making further changes.
  Backed by: `efe53299`, 2026-06-18T14:47, Ingi calling out repeated stash/unstash churn — bot: "I was churning."
