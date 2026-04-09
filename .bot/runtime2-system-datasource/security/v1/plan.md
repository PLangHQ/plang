# Security Audit Plan — runtime2-system-datasource v1

## Scope

This branch introduces:
1. **IDataSource / SqliteDataSource** — persistent key-value store per actor (SQLite-backed)
2. **SettingsData** — lazy-loading Data subclass bridging `%Settings.X%` variable resolution to DataSource reads
3. **Settings action handlers** (get/set/remove) — PLang actions for settings CRUD
4. **Actor.DataSource** — lazy-initialized DataSource per actor, DB at `.db/{actorname}.sqlite`
5. **LazyParamsGenerator changes** — `__resolutionError` propagation for AskError on missing settings
6. **AskError / DataSourceError** — new error types
7. **SqliteSettingsRepository table rename** — v1 table collision fix (Settings → SettingsV1)
8. **Variables.Clone changes** — preserve SettingsData by reference in cloned stacks

## Phase 1: Blue Team (Attack Surface Mapping)

Map all trust boundaries and input paths:

1. **SqliteDataSource SQL injection surface** — table names, key values, stored data
2. **DeserializeValue** — JSON parsing of stored data (calls Data.UnwrapJsonElement)
3. **SettingsData.GetChild** — sync-over-async pattern, path parsing, depth propagation
4. **Actor.CreateDataSource** — db path construction from actor name
5. **LazyParamsGenerator __resolutionError** — error propagation in generated code
6. **SqliteSettingsRepository migration** — schema migration race conditions
7. **Resource exhaustion** — unbounded table creation, key count, value size

## Phase 2: Red Team (Attack Vectors)

For each surface identified in Phase 1, attempt to construct attack scenarios:

1. SQL injection via table names → SanitizeTableName bypass?
2. Deeply nested JSON stored/retrieved → DoS via UnwrapJsonElement
3. Path traversal in actor name → file writes outside .db/
4. Deadlock/hang from sync-over-async in SettingsData.GetChild
5. Key/value size exhaustion (no limits on SQLite storage)
6. Malicious .pr setting attacker-controlled table names
7. Race condition in SqliteSettingsRepository table rename migration

## Phase 3: Report

Write security-report.json, verdict.json, summary.md, learnings.

## Key Prior Findings to Verify

From data-envelope-architecture security report:
- Finding #1 (UnwrapJsonElement depth limit) — verify it protects DeserializeValue path
- Finding #8 (Newtonsoft shim) — still open, low priority
- Finding #11 (Data.Merge unbounded) — still open, low priority
- Finding #12 (Variables.Clone) — changes in this branch, verify

From runtime2-settings security report:
- Finding #1 (unbounded scope dictionary) — accepted risk
- Finding #4 (configurable security limits) — accepted risk
