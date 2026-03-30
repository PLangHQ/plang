# Security — Builder Module Summary

## v1 — Initial Security Audit
PASS — 0 critical, 0 high, 2 medium, 3 low. Builder module follows PLang threat model correctly. BuildingGuard consistent, file I/O through abstractions, JSON errors caught. Two medium findings: Goal.Parse() no size limit (OOM risk), Providers.ResolveType defaults to signing on empty type. See [v1/summary.md](v1/summary.md).
