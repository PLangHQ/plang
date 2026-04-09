# Code Analyzer — Cross-Session Summary

## v1 — Code simplicity analysis
First analysis of the data-envelope-architecture branch. 16 files, three-pass review (OBP, simplification, readability). Result: 12 CLEAN, 3 NEEDS WORK (minor). No OBP violations. Seven low-to-medium findings — duplicated CleanName, duplicated system var check, inverse dict sync risk, inconsistent concurrency, loose rehydration heuristic, silent Merge data loss, single-level generic limitation. Overall: well-structured, OBP-compliant code. See [v1/summary.md](v1/summary.md) for details.

## v2 — Higher-level review after coder v5 + tester v7
Applied learnings from runtime2-settings: trace data origins, audit clone/copy families, review fixes against full type surface. Found 4 cross-concern gaps that file-by-file review missed: (1) UnwrapJsonElement loses decimal precision (19.99 → double), (2) Variables.Clone() doesn't propagate Context, (3) GetChild→Variables contract change untested at integration level, (4) fromJson masks depth-exceeded as "Invalid JSON". Added 4 todos, recorded 6 learnings. Key pattern: Clone methods on both branches have the same blind spot of not propagating new properties.
