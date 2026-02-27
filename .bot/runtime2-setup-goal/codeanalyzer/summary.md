# Code Analyzer — runtime2-setup-goal

**v1** — Analyzed Setup.goal run-once system and DataSource/Settings bridge. FAIL: two behavioral issues — (1) failed setup steps permanently recorded as executed, never retried on restart; (2) Setup.Record silently swallows DataSource errors. One low-severity consistency finding on EngineGoals.Count/All vs Get. See [v1/summary.md](v1/summary.md).

**v2** — Re-reviewed after coder fixes. All three findings addressed correctly. No new issues. PASS. See [v2/summary.md](v2/summary.md).

**v3** — Deep re-analysis of entire branch. FAIL: SettingsData bridge registered only on System actor's MemoryStack, but PLang execution uses User.Context — `%Settings.ApiKey%` silently resolves to null. All SettingsData tests mask this by using System.Context.MemoryStack. See [v3/summary.md](v3/summary.md).
