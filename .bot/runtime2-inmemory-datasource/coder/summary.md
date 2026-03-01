# In-Memory SQLite DataSource — Coder Summary

## v1

Implemented in-memory SQLite datasource: `SqliteDataSource.InMemory(name)` with sentinel connection, `Engine.Building` object (like Testing/Debug), Actor auto-selects in-memory when Testing or Building is enabled. 7 new tests, all 1472 tests pass. See [v1/summary.md](v1/summary.md).
