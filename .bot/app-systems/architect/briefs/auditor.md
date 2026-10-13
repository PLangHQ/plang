Check off every plan on the line before the merge to runtime2 (decision 718)
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
