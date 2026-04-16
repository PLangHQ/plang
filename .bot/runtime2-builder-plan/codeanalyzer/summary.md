# Codeanalyzer Summary — runtime2-builder-plan

## v1 — Full 5-Pass Analysis
23 findings across the massive Data<T> composition / return removal / condition orchestration branch. 3 critical: foreach silently drops dictionary key/value support, ResolveDeep mutates shared template objects, old .pr files silently lose Return mappings. 8 should-fix including broadened Handled semantics and uncached reflection. Verdict: FAIL — send back for critical fixes. See [v1/summary.md](v1/summary.md).
