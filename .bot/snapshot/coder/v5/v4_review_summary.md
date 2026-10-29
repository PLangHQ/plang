# v4 review summary — codeanalyzer on snapshot-7c-wip (FAIL, 36 findings)

Review: `.bot/snapshot-7c-wip/codeanalyzer/review.md` (commit 0b51f328a, reviewed at d119e797f). Ruled by the architect
in snapshot 123.

- **Bugs** (a test red before each fix): 10 (a resume inside an `if` body runs the `else`), 15 (`error.Goal` a flat
  copy, unset when Record first places the error), 21 (two Diff births; the stream keeps the live object; frames'
  diffs yielded twice), 34 (Parameter marks every variable born), 9 (two sensitive policies), 3 (view-less reader
  doors), 13 (Data's copiers drop result facts).
- **New on this branch:** 4 (Synthetic, test-only), 6 (Clone/Copy twins), 8 (token read hard-codes Store), 16
  (step.List stamped by callers), 17 (snapshot.Context setter, goal.Stamp), 18 (a provider's origin), 19 (key readers:
  one Refused, the Reads doc), 20 (the walk as a type), 26 (five reflective thunks), 28 (the identity's key form), 32
  (`_pr`), 33 (CallbackGoalNotFound), 35 (comments and names).
- **Older, scaled:** 23, 24, 25, 27, 29, 30, 31, 36, and 34's extras (Property.Holding, the bearer header).
- **Bench:** bytes over band (root birth +4.4%, sub request +6.6%, root request +6.1%); held memory flat. Measure the
  extra allocations by type, base d72538402 vs HEAD; report, don't change.
- **To Ingi, not code:** 2, 5, 6's container twins, 7's public dead parameters, 8's kind.Read view, 11, 12, 14, 22.
