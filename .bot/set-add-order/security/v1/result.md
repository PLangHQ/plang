# set-add-order — security v1 (37b2995fa, against runtime2)

Verdict: PASS with one Medium (F1) for the architect to rule. No Critical or High.

Pattern: 12 (one question, one answer, on the element — `Part` is the one answer to "is the decider sure of this module"). The finding is that the answer reads the settled module's whole stage-1 mass as "already answered".

## The question: can a destructive or sensitive action reach the starting line where the words didn't name it?

Yes, in a window the 09-30 guard (fb14118e0) used to close. Probes `PLang.Tests/Wire/App/Decider/ModulePartSecurityProbeTests.cs` (9 green; they pin the observed edge).

`Part(module) = share / (1 − Σ shares of Settled modules)`, and `Settled` is every module with a common action the decider scored ≥ 0.9 (`:501`) — in practice `variable.set` ("does the step write a variable"), which the decider says yes to for most steps. A module the plain share rated Possible becomes Certain once the settled variable's share is divided out.

| stage 1 (module / variable) | variable.set | file.delete (code.remove, identity.archive the same) | before (plain share) |
|---|---|---|---|
| file 0.60 / variable 0.36 | none | Possible (C1) | Possible |
| file 0.60 / variable 0.36 | 0.95 | **Certain, on the line** (W1) | Possible |
| file 0.57 / 0.36 | 0.95 | Possible; 0.58 is the edge (W2) | Possible |
| file 0.80 / 0.18 | 0.89 | Possible (W2: needs ≥ 0.9) | Possible |
| file 0.50 / variable 0.46 | 0.95 | **Certain** (W3) | Possible |
| variable 0.53 (main) / file 0.43 | 0.95 | **Certain** (W3) | Possible |
| settled shares ≥ 1 | | not Certain, no division by zero (W4) | |

`Part` itself is safe: `ranked.Single(...)` cannot throw, because `Asked()` takes its modules from `Ranked()` and only an asked module has a Through pick; `left ≤ 0` answers 0.

## F1 — Medium: the 09-30 guard's window reopens when a common variable.set settles variable (pattern 12)

- **Vector:** `remove %x%` is ambiguous (variable.remove or file.delete). Stage 1 splits file / variable; a high `variable.set` yes/no settles variable; `Part` divides variable's mass out; file.delete is Certain at a file share of 0.58 (even 0.43 when variable is the decider's main reading). The mass `Part` discards is the one place the arithmetic is wrong: a settled module's share may be for *another action of the same module* (here variable.remove), which no common action answers. The plan names this edge ("rare; the builder probes it once"); the builder's runs got stage 1 at file 0.94, so the window was not exercised.
- **Amplifier:** a Certain action is not only pre-filled: `Agree` refuses a build that leaves it out (`:217`, "leaves out file.delete, which the decider is certain of"), so the retry loop pushes the writer toward the destructive action rather than only offering it.
- **Impact:** a goal written to drop a variable builds a file delete of the path in that variable (data loss at run time). Same for code.remove, identity.archive, http.* by the same arithmetic; nothing is limited to file. Not an attack on an untrusted input (the step is the user's own words, and the build is the user's), so Medium for a guarded mis-pick that a recorded guard existed for; the likelihood depends on how often the decider scores `variable.set` ≥ 0.9 on a bare remove, which is the number the builder can measure.
- **Fix options (the architect's call; none is a locked-core addition):**
  1. Subtract a settled module's share only when that module's *own picked action* is the common one: i.e. only when the settled module's stage-2 answer (or its single action) is the common action. For `variable` here, the settled variable.set would have to be the module's only candidate; variable.remove stays open, so the share is not subtracted. Smallest change: compute `left` from the settled modules whose `_named` action is the module's only asked action.
  2. Keep `Part` but also require the module's plain share to be at least `Possible` + a margin (it already is ≥ 0.5 at the window's edge, so a floor of 0.8 closes W1–W3 and keeps set+add at 0.67? no: set+add is 0.67, so the floor cannot be 0.8; a floor at 0.6 closes the 0.43/0.50/0.57 cases only).
  3. Do not refuse a build for leaving out a Certain action that is `Part`-certain but plain-share-Possible (removes the amplifier; the pre-fill stays).
- **Measure:** the builder's `variable.set` yes/no on `remove %x%` and `remove %x%, write to %y%`, and stage 1's two shares. If the yes/no is far below 0.9 on those, the window is not reached by the decider as it answers today.

## Info

- I1. The prompt's "(module file 0.58)" beside a Certain action stays stage 1's share, so the prompt shows a Certain mark beside a 0.58: the reader of the prompt can't tell why. Cosmetic.
- I2. Nothing else in the diff touches a trust boundary: `Placed()`/`Assigned()` read the step text through the variable parser and a `Literal` regex (anchored, no backtracking on `\S+`), and only hint the starting line (the formal reader re-reads).

No production edit, no mutation test.
