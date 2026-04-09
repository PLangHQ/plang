# Architect Summary — system-goals-architecture

## v1: Everything is Data — Revised Proposal
Rewrote the everything_is_data.md proposal after design conversation with Ingi. Key refinements: dropped CRTP constraint, dropped `__` prefix, added `.` vs `!` navigation convention with DeclaredOnly, registered per-type navigators on engine, scoped inheritance (Goal/Step/Action stay out), `new` keyword for property collisions. See [v1/summary.md](v1/summary.md) for details.
