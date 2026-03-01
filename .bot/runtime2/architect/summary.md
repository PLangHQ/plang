# Setup.goal — Architect Summary

## v1 — Setup.goal execution system design
Designed the run-once setup system for runtime2. Setup is an object on `engine.Goals.Setup` with `Executions` (sqlite-backed smart collection, one row per step hash) and `RunAsync`. Context propagates through goal.call so all reachable steps get tracked. Steps.RunAsync owns the iteration and run-once check. See [v1/summary.md](v1/summary.md) for details.
