# Previous version (v3) review summary

My v3 verdict was **PASS** (C# 4082/0, plang 273/273, compare path mutation-verified;
minor: new `where` action lacked a plang test). That was before the v3→v6 churn.

Between then and now the branch went through codeanalyzer v3 (**FAIL**) and coder v4–v6:
- **F1 list aliasing** (`add %b% to %a%` entangled the two variables) — coder fixed via
  `list.@this.CopyStructure()` at the merge boundary. I re-verified this independently
  (mutation: `CopyStructure → return this` reds both aliasing tests). Honest fix.
- **F3 O(n²)** structural ops — hoisted to single materialized views. Fine.
- **F4** stale comments — fixed.
- **F2 — two signing regression tests disabled.** This is the v6 blocker I caught below.
  codeanalyzer accepted it as a documented, merge-gated deferral and explicitly asked
  tester to confirm scope. On confirming scope I found the disable masks a live regression.
