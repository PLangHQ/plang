# tester — runtime2-builder-bootstrap

## v1 (2026-04-28) — needs-fixes

C# 2281/2289, PLang `/Tests/` 132/161 (+4 stale), PLang `/tests/` 8/9. 12 findings: 1 critical, 7 major, 4 minor. Headline: 8 `BuildingGuardTests` are red — the guard helper that existed in `runtime2` was deleted by the v2 builder squash and 4 codeanalyzer rounds missed it because they reviewed code, not test outcomes. Also: `BuilderValidateValid` flood of `'int = 1' → Int32` conversion errors (the @known unwrap is missing); Loop test returns `"0 + 1 + 1 + 1"` instead of `3`; 9 Signing tests fail; both v4 carryovers confirmed (locale-format asymmetry has zero coverage, `promoteGroups` + `enrichResponse` both at 0%); coder-handover Gap 2 (file.read ResolveVariables) and Gap 3 (single→list auto-wrap) ship with zero direct unit tests. Send back to coder. See [`v1/summary.md`](v1/summary.md).
