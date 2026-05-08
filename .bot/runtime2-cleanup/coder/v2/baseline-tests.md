# Baseline tests — stage 2

Baseline taken at the tip of branch `runtime2-cleanup` after pulling the
codeanalyzer report and stage-2 brief. Stage 1 had already merged green;
no fresh measurement needed beyond confirming the same numbers hold.

- C#: 2755/2755 pass (matches stage 1 finish).
- PLang: 199/199 pass (matches stage 1 finish).

Build: clean (0 errors, 448 warnings — same as baseline).

Stage 2 is dead-code deletion + parameter drop; expect the numbers to
match exactly when stage 2 is complete.
