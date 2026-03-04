# PLang Test Gaps — Architect Summary

## v1 — PLang test gap analysis (updated)
Mapped all runtime2 modules and engine subsystems against existing `.test.goal` coverage. Initially 23 suites; updated after tester's first pass added 6 new suites (29 total). Resolved: context variables, goal calls (partial), variable ops, convert, list ops, math. Remaining gaps: error handling (biggest — builder limitation flagged), events (half untested), caching (sliding/keys), goal calls (dynamic/recursive), actors (zero), setup (zero PLang), library.load. See [v1/plan.md](v1/plan.md) for full gap analysis with example test code.
