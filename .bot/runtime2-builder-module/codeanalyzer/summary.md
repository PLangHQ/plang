# Code Analyzer — Builder Module (Cross-Session Summary)

## v1: NEEDS WORK — 5 minor findings
5-pass analysis. No OBP violations. Action handlers are the cleanest in the codebase. Findings: 2 untested Parse() edge cases, unguarded Activator.CreateInstance in GetDefaults(), untested IConfigure<T> defaults path, Runtime1 type reference in FormatForLlm. See [v1/summary.md](v1/summary.md).
