# Open items — branch `goal-graph-singular`

Kept here so the list survives a session. Ordered by what I'd do, not by when it was found.
Everything below is on top of a clean, pushed tree (`16bbc24c4`).

## The work itself

**1. Finish Stage D — the Validate trilogy.** `action.Validate` is landed but has **no callers**
(an incomplete rung is its own small liability). Remaining: `action.list.Validate`, then point
`build.validate` at it so the builder only *reacts* (re-prompt / abort). `build.validate` receives
an action LIST, not a goal — so `goal.Validate` / `Step.Validate` have no caller yet and were
deliberately not written. Item 3 below changes what `Validate` yields, so do it first.

**2. `action.Requires` — plural bothers Ingi.** It returns the set of things an action reaches
(`network`, `llm`). Every other collection on the graph is a singular concept node (`action.Warning`,
`step.Action`), so the plural is the odd one out. Needs a name that reads singular over a collection.

**3. `action.BuildError` → `action.error` (probably `error.list`).** Ingi: it should be the action's
error node, not a one-off compound. There is already `app.error.list.@this`, and `action.Warning` is
`warning.list` — so `action.Error` as an `error.list` matches the existing shape exactly. It also
makes `Validate` natural: verdicts land on the node instead of being yielded to a caller. **Do this
before Stage D** — it decides `Validate`'s signature.

**4. `Property.Rows` is a middleman.** `Rows => Items.Select(d => d.Clr<Property>()).ToList()` —
proxies what the collection already is, lowers to CLR, and reallocates per read. The fix is NOT
a rename to `.list`: the list holds `Data` wrappers and `Rows` exists to unwrap them, so renaming
just spreads `.Clr<Property>()` into all four callers. Make `property.list` enumerate its own typed
rows (`IEnumerable<Property>`) so it reads `foreach (var row in element.Property)`.

## Risk carried into main

**5 + 6 — CLOSED (`4deaa8921`).** Both were prompt-level and both were real. Read the rendered
prompt from a `cache:false` build: `## Available types` rendered EMPTY always (`%!app.type.list%`
navigates to nothing — `module.list` has a native `list` member, `type.list` has none), dead since
`92f846d09`; and `→ returns item` reached the LLM with `item` defined nowhere in the prompt. Section
and its feeding step deleted (primitives + catalog types already render elsewhere); `item` now
explained once. **Lesson worth keeping: neither was visible from code or tests — only from the bytes
the model receives.**

**5b. The builder's `.pr` hashes are stale.** `goal.Hash` is `SHA256(Name + concat(step.Text))`,
computed AND stored in the `.pr`. In `BuildStep/.build/start.pr` the stored hash disagrees with the
steps for ROOT, `Compile`, `QueryAndVerify`, `RefineActions`, `FixValidation` — **verified stale at
`HEAD~1`, i.e. before my edit**, from earlier hand-edits. `HandleStepFailure` and `EmitSummary` match.
Impact looks nil for building (builds pass), but anything using hash for staleness sees a lie.
Recomputing is a few lines; deliberately NOT done unilaterally on a bootstrap artifact.

## Test-infrastructure problems (these are why the above stayed hidden)

**7. Suite discovery is flaky — the meta-problem.** Totals swing 425–756 across runs of identical
code; whole classes silently don't run. Consequences: every change this stretch had to be verified
by stash+rebuild and a failure-NAME diff, because counts prove nothing; and item 8 sat red all
session without appearing in a single full-suite sample. Until this is fixed, no green result on
this branch means much.

**8. `DiscoverActionTests` is 7/10 red.** Verified identical on the stashed tree — pre-existing, not
from this work. But it includes **both auto-tag tests**, so `action.Requires` (item 2) has no green
coverage.

**9. `PathSerializerMigrationTests` alternates 0-then-2 failures** across consecutive isolated runs
of identical code. Real shared-state race in scheme registration, not caused by this work.

## Process gap

**12. The builder has never built itself on this branch.** Every commit touching
`os/system/builder/*/.build/*.pr` is a hand-edit ("bootstrap builder .pr", "fix param wire shape",
"fix stale visibility"). There is no forcing function that the builder's own goals still compile —
which is exactly how items 5 and 6 survived across many commits, and how five stored hashes drifted.
Ingi (2026-09): worth recording, but we are deep in refactoring and this branch merges up into
another that may be where this belongs — decide when we get there, do not chase it now.

## Small cleanups

**10. `goal.list` late stamp** — `_goals = new goal.list.@this { App = this }`, the same smell removed
from the module registry (which now takes its App at construction). Left alone only because the
injection seam differs.

**11. Stale test** — `ModulesDescribe_BuilderRecordHandlers` NREs looking up module `"builder"`; the
module is `"build"`.
