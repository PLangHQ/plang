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

**5. `→ returns item` is unverified in a real prompt.** Undefined `T` now renders as `item` in the
builder's action catalog (was `data`). Nothing in the templates keys off the old value — checked —
but I never confirmed `item` appears in the type list the LLM is shown. A token the model doesn't
recognise degrades every build silently. Cheap to settle: one `cache:false` build, read the rendered
compile prompt.

**6. `%!app.type.list%` render still unverified.** Same class of risk, older. A debug watch showed
`(undefined)`, but this branch's debug-arg binding is broken three separate ways, so the watcher
lies. Needs checking via the rendered `CompileUser` output, not the watcher.

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

## Small cleanups

**10. `goal.list` late stamp** — `_goals = new goal.list.@this { App = this }`, the same smell removed
from the module registry (which now takes its App at construction). Left alone only because the
injection seam differs.

**11. Stale test** — `ModulesDescribe_BuilderRecordHandlers` NREs looking up module `"builder"`; the
module is `"build"`.
