# Auditor v4 Review Summary

## Verdict: FAIL (2 major, 1 minor, 1 nit)

### F1 (Major): GetAsync doesn't filter IsSetup
`Get()` correctly filters `!goal.IsSetup`, but `GetAsync()` and `GetByPrPathAsync()` return goals loaded from disk without checking the flag. A setup goal can be called as a regular goal if it's not in cache when first requested.

### F2 (Major): Setup goals never loaded before Setup.RunAsync
`Executor.Run2` creates a fresh Engine, calls `Setup.RunAsync` immediately. No goals have been loaded — `Setup.Goals` iterates an empty collection. Setup silently succeeds without running anything. Tests mask this because they manually `Add()` goals.

### F3 (Minor): 'setup' goal name reserved
`Run2` intercepts `goalName == "setup"` unconditionally. If a user has a non-setup goal named "Setup", it's silently skipped.

### F4 (Nit): Metadata numeric boxing
Record() stores stepIndex as int in `Dictionary<string, object?>`. No action needed — flagged for awareness only.
