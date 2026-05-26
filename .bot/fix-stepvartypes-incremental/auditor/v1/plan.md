# Auditor v1 plan — fix-stepvartypes-incremental

## Context
- codeanalyzer v3 → FAIL → coder (0943e5fda) closed HIGH/LOW → no v4 (codeanalyzer didn't re-verify), but tester v6 confirms 208/208 + 3036/3036.
- tester v6 → PASS with one minor note (4-of-6 Stale branches in discover.cs untested; "pre-existing").
- security v1 → PASS, no new critical/high.
- Late commits (after codeanalyzer's last full read):
  - 1b1b226bb tester/File.cs slim (drop 6 mirror props; only Goal ref left)
  - 463339c90 step.@this drops Guidance/Level/Confidence
  - 0f8886ab0 builder template restructure
  - dfd7429a7 stepActionDetails template path update

## What the other bots did NOT cover
- Did **every** consumer of File.cs migrate from `file.Path`/`file.PrPath`/`file.EntryGoalName`/`file.Directory`/`file.GoalHash`/`file.BuilderVersion` to `file.Goal.*`? Tester says grep is clean — verify and also check builder-output `.pr` consumers and JSON snapshot files.
- Step prop drop — any serializer/JSON shape coupling that survives via "ignore unknown" but reads stale data elsewhere? Tester checked deserialize; check serialize and any reflection sites.
- Evaluator extract: the helper now boxes `data.@this<Operator>` and `data.@this?` — null/short-circuit semantics preserved? Verify behavior parity with three prior bodies.
- Compare result-shape after Compare went through the helper: pre-extract returned `data.@this<bool>` directly; post-extract goes through `EvaluateOperator(...)` then returns. Any wrapper double-wrap risk per CLAUDE.md "Action Run returns are typed footgun"?
- Are the 4-of-6 untested Stale branches in discover.cs actually reachable? If the slim restructure dropped state that fed one branch, that branch might be dead code — opposite worry from tester.
- formats/this.cs +2 lines — what's added? Any new MIME registration that affects deserialization of .pr files?
- path/this.Derivation.cs and this.cs canonical form changes — security covered AuthGate, but did they trace equality semantics through PathEqualityTests?

## Plan
1. Read full File.cs + every consumer site that the report-summary identified. Grep production+tests+os/ for stale property names.
2. Read condition/code/Default.cs full post-extract. Compare against pre-extract bodies (mentally) for null/exception/result shape parity.
3. Check step.this.cs + builder/code/Default.cs for stale references and any serialization sites still emitting the dropped props.
4. Inspect formats/this.cs delta.
5. Read test/run.cs full to verify the IsEntryGoalStep simplification and Output/Timings additions cohere.
6. Reach a verdict — PASS if no new critical/major; FAIL with named issues otherwise.
