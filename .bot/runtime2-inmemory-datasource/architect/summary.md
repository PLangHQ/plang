# Architect Summary — runtime2-inmemory-datasource

## v1 — In-Memory SQLite DataSource

Designed in-memory SQLite datasource support for tests, builder, and C# unit tests. `SqliteDataSource` gains `InMemory(name)` factory with sentinel connection. New `Engine.Building` object (like Testing). Actor navigates Testing/Building context to decide. See [v1/summary.md](v1/summary.md) for details.
