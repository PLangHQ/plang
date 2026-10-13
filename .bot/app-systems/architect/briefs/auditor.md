# auditor: app-systems

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
