# Security Audit Plan — runtime2-inmemory-datasource v1

## Scope

Delta from `runtime2` base — the in-memory SQLite datasource, Building object, settings action handlers, and related changes. Cross-referenced with runtime2-system-datasource security findings for carry-forwards.

## What Changed

**New files:**
- `Engine/DataSource/SqliteDataSource.cs` — InMemory() factory + sentinel connection
- `Engine/Build/this.cs` — Building mode controller
- `Engine/Context/Actor.cs` — CreateDataSource() routing
- `Engine/DataSource/SettingsData.cs` — Lazy settings bridge on Variables
- `Engine/DataSource/IDataSource.cs` — interface
- `Engine/Errors/DataSourceError.cs`, `AskError.cs` — error types
- `actions/settings/get.cs`, `set.cs`, `remove.cs`, `types.cs` — settings handlers

**Modified:**
- `Engine/this.cs` — Building property added
- `Engine/Memory/Variables.cs` — SettingsData registration
- Various test and .pr file additions

## Phase 1: Blue Team — Attack Surface Analysis

### 1A. In-Memory DB Isolation
- SQLite shared-cache means `InMemory("system")` from two Engine instances shares state
- Is this exploitable? In what contexts do multiple engines exist?
- Verdict: rate by PLang threat model (user-sovereign, trust boundary = signatures)

### 1B. Sentinel Lifecycle
- Sentinel connection keeps in-memory DB alive
- What happens if Dispose() isn't called? (leak, not crash — low priority)
- What happens on use-after-dispose? (CRUD methods don't check `_disposed`)

### 1C. DeserializeValue Exception Gap (Carry-Forward)
- `SqliteDataSource.DeserializeValue` → `Data.UnwrapJsonElement` → `InvalidOperationException` on depth > 128
- Only `JsonException` caught, not `InvalidOperationException`
- Propagates through `SettingsData.GetChild` (no try/catch) to caller
- Rate: medium (requires externally-sourced deeply nested JSON in settings)

### 1D. SQL Injection Surface
- Table names: `SanitizeTableName` strips non-alphanumeric + bracket-quotes
- Keys: parameterized via `@key`
- Values: JSON-serialized
- Verdict: properly mitigated

### 1E. SettingsData.GetChild — Trust Boundary
- Lazy-loads from System actor's DataSource on property access
- `.GetAwaiter().GetResult()` — sync-over-async (safe for SQLite, constrains future IDataSource)
- No depth guard on its own recursive call? Check: it calls `child.GetChild(remaining, depth + 1)` — depth IS propagated

### 1F. Building.IsEnabled — Public Setter
- Same pattern as Testing.IsEnabled
- Can .pr code set `%!engine.Building.IsEnabled%` to true?
- Under PLang threat model: user-sovereign, this is the user's prerogative — not a vulnerability
- But: does it have side effects? (Actor.CreateDataSource is lazy — if already created, flag change does nothing)

## Phase 2: Red Team — Exploit Assessment

For each finding from Phase 1, assess:
- Preconditions required
- Feasibility of exploitation
- Severity under PLang threat model
- Concrete fix proposal (if warranted)

## Phase 3: Deliverables

1. `security-report.json` — structured findings
2. `verdict.json` — pass/fail
3. `summary.md` — session summary
4. `result.md` — detailed findings
5. Update bot root `summary.md`
6. Update `report.json` with session end
7. Commit and push
