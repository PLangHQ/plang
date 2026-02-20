# Data Envelope Architecture — Tester Summary

**v1** — Analyzed coder's 62 Engine.Types tests. All 1229 C# tests pass. Found 1 critical issue: Kind(null)/Mime(null) crash with unhandled ArgumentNullException. Found 1 false green: Name() test uses Uri (works) but misses HashSet<string> which produces invalid "hashset`1". BuilderNames() and ComplexSchemas() have zero tests. Verdict: needs-fixes. See [v1/summary.md](v1/summary.md) for details.
