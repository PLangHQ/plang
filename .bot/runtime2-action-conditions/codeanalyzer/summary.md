# Code Analyzer — runtime2-action-conditions

## v1
5-pass analysis of coder v1 (IEvaluator, DefaultEvaluator, if.cs, compare.cs, Steps sub-step logic). Verdict: NEEDS WORK. 4 mechanical fixes (duplicate summary, public helper, unsealed class, per-call array allocation) + 1 medium-severity behavioral bug (Contains with boxed numerics returns wrong results for int vs long in collections). See [v1/summary.md](v1/summary.md).
