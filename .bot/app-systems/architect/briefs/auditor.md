# auditor: app-systems

Traces to: decisions 665 (Ingi: "we need to make sure all the plans are checked off"; the bar is owned, not green), 670 (a done plan's tests move into the tree) and 651 and 689 (the locked core and its surface golden), in `.bot/app-systems/architect/summary.md`.

Read `common.md` beside this file first. You run after codeanalyzer, tester and security; read their reports first (`.bot/app-systems/{codeanalyzer,tester,security}/`, `test-report.json`, `security-report.json`).

## Why

Ingi: "We need to make sure all the plans are checked off." Nobody has checked the plans on the line since runtime2. Merging up means each one is **done**, or **carried** with the decision that carried it.

## The plans on the line

Added since runtime2 (`git log --diff-filter=A 0ea5a4b94..app-systems -- '.bot/*/architect/plan.md' 'test/plan/*/start.md'`):

- branch plans: `.bot/{scalars-as-native, settings-config-unification, cli-app-property-override, module-discovery, builder-formal, app-systems, clr-navigators, compare-redesign, context-never-null, goal-graph-singular, navigation-driven-record-builder, wire-source-split}/architect/plan.md`, and `.bot/get-builder-running/architect/summary.md` (no plan file);
- test plans: `test/plan/{app-systems, task, list-query, snapshot}/start.md` with their `start.goal` index.

For each plan, each stage or item: **done** (point at the commit or the passing test), **carried** (point at the decision in that branch's `summary.md`, or `.bot/app-systems/architect/next-branch.md`), or **missing** (neither). app-systems keeps its own list in `test/plan/app-systems/done.list`; snapshot is a plan only, carried as a whole (665).

## Also

- **The reviewers' findings:** for each, does it really block, did severity fit the PLang model, and is it already carried (the others may have missed `next-branch.md`)?
- **Cross-file contracts and the locked core:** anything in this line that changed a locked surface without a decision naming Ingi's yes (the surface golden in the gate pins it; a re-pin should cite its decision).

## Output

Per your character, plus one table per plan (item, status, evidence). A **missing** item is a finding: blocks the merge, or carry with a reason Ingi can say yes to.

## After you

When the plans are checked off, the app-systems plan's tests move from `test/plan/app-systems/` into the concept tree (Ingi, 670), and the docs bot merges up the line.

## In plang

The same brief as steps. The comments under it show what a finding looks like.

```plang
Check off every plan on the line before app-systems merges into its parent (decision 718; not runtime2, 734)
Start
- read .bot/app-systems/architect/briefs/common.md
- read the other reviewers' reports first: .bot/app-systems/{codeanalyzer,tester,security}/, test-report.json, security-report.json
- for each plan below, mark each stage or item done (the commit or passing test), carried (the decision in that branch's summary.md, or next-branch.md) or missing (neither)
- branch plans: .bot/{scalars-as-native, settings-config-unification, cli-app-property-override, module-discovery, builder-formal, app-systems, clr-navigators, compare-redesign, context-never-null, goal-graph-singular, navigation-driven-record-builder, wire-source-split}/architect/plan.md, and .bot/get-builder-running/architect/summary.md (no plan file)
- test plans: test/plan/{app-systems, task, list-query, snapshot}/start.md with their start.goal; app-systems keeps its own list in test/plan/app-systems/done.list; snapshot is a plan only, carried as a whole (665)
- for each reviewer finding: does it really block, did severity fit the PLang model, is it already carried (another reviewer may have missed next-branch.md)?
- check for a locked surface changed without a decision naming Ingi's yes (the surface golden in the gate pins it; a re-pin should cite its decision)
LIMITS: read only; no fixes, no moves
DONE WHEN: one table per plan (item, status, evidence), and every missing item marked blocks or carry with a reason Ingi can say yes to
REPLY: your report in .bot/app-systems/auditor/, and a message in the Done/HASH/CHECK/OPEN shape
WHY: Ingi: "We need to make sure all the plans are checked off"; nobody has checked the plans on the line since runtime2, and merging up means each is done or carried with the decision that carried it

/ how a test plan reads (test/plan/app-systems/start.goal): each stage a goal, each comment a behaviour, the step under it runs its test
/ Stage9
/ / every value is born through its type, so a program can hook any value's birth (plus checks.md: the module pass)
/ / "after file create" fires when a file value is born
/ - call /plan/app-systems/app/type/file/CreateFiresOnBirth
/ / converting a value to another type is a birth: "set %p% as path" fires "after path create"
/ - call /plan/app-systems/app/type/path/AsPathIsABirth
/
/ a row in your table
/ | 9: converting is a birth | done | plan/app-systems/app/type/path/AsPathIsABirth passes |
/
/ after you: the app-systems plan's tests move from test/plan/app-systems/ into the concept tree (Ingi, 670), and the docs bot merges up the line
```
