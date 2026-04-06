# Code Analysis Report — DataSource + Settings Bridge

Branch: `architect/runtime2-system-datasource`
Analyzed: 2026-02-23

---

## PLang/App/DataSource/IDataSource.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
1. **Line 52: `ResolveTableName` is a static interface method** — Well-placed convention helper. Clear name, clear purpose. Clean.

### Verdict: CLEAN
Well-designed interface. All methods return `Task<Data>`, never throw. XML docs on every method. The static `ResolveTableName` convention is useful for handlers.

---

## PLang/App/DataSource/SqliteDataSource.cs

### OBP Violations
None. Constructor receives primitives (`string dbPath`, `IPLangFileSystem`) rather than navigating Engine, but this is acceptable at the infrastructure boundary — SqliteDataSource should not depend on the Engine graph.

### Simplifications
1. **Lines 57-85 (and all CRUD methods): EnsureTable opens a separate connection per call** — Every Get/Set/Remove/Exists calls `EnsureTable()` (which opens connection + executes `CREATE TABLE IF NOT EXISTS`) then opens a *second* connection for the actual operation. That's 2 connections per operation. SQLite pools internally so this isn't broken, but `EnsureTable` could use a `HashSet<string>` cache to skip known tables after the first call.

2. **Lines 261-270: Bare `catch` in `DeserializeValue`** — Catches ALL exceptions (including `OutOfMemoryException`, `StackOverflowException`) and silently returns the raw string. Should catch `JsonException` specifically.
   - Current: `catch { return json; }`
   - Should be: `catch (JsonException) { return json; }`

### Readability
1. **Lines 41-55: `EnableWalMode` try/catch** — The bare `catch` with comment is acceptable here (non-fatal optimization). But an explicit `catch (SqliteException)` would be more precise.

### Behavioral Reasoning
1. **Line 266: Bare catch masks non-JSON errors** — If `JsonDocument.Parse` throws something other than `JsonException` (e.g., `ArgumentException` for certain malformed inputs), the error is silently swallowed and the raw string is returned. This could mask data corruption.

### Verdict: NEEDS WORK
Solid implementation. The bare `catch` in `DeserializeValue` is the main issue — it should be `catch (JsonException)`. The EnsureTable overhead is a minor performance concern.

---

## PLang/App/DataSource/SettingsData.cs

### OBP Violations
None. Keeps engine reference (OBP rule 3), navigates via `_engine.System.DataSource` (OBP rule 2).

### Simplifications
None. The path-splitting logic duplicates `Data.GetChild` but this is necessary — SettingsData intercepts at the first segment for a database lookup. Can't delegate to base.

### Readability
Clean. 74 lines. Clear variable names. The `GetAwaiter().GetResult()` has a comment explaining safety.

### Behavioral Reasoning
1. **Lines 51-52: `GetAwaiter().GetResult()` is fragile** — Safe for SQLite (no async I/O), but if `IDataSource` ever gets a non-SQLite implementation with real async, this becomes a deadlock risk on UI/ASP.NET synchronization contexts. Should be documented as an architectural constraint on IDataSource implementations.

2. **Variables.Clone() does NOT preserve SettingsData type** — `Variables.Clone()` (line 182 in Variables.cs) creates `new Data(kvp.Value.Name, clonedValue, kvp.Value.Type)` — plain `Data`, not `SettingsData`. If the System actor's context is ever cloned (via `PLangContext.CreateChild()` or `PLangContext.Clone()`), the cloned stack will have a plain `Data` named "Settings" whose value is `null`, losing the lazy-loading override. Any `%Settings.ApiKey%` resolution in child contexts would get `null` instead of loading from the database.
   - **Severity**: Medium-High. Depends on whether System actor contexts are ever cloned. If System actor only runs single-context (no child goals), this is theoretical. If goals can run under System context, it's a real bug.
   - **Fix options**: (a) Make SettingsData a DynamicData-like approach where the Value itself is the lookup mechanism rather than overriding GetChild. (b) Add SettingsData awareness to Variables.Clone(). (c) Register SettingsData with `!Settings` prefix so it's treated as a system variable (skipped during clone, shared by reference).

### Verdict: NEEDS WORK
The Variables.Clone() type-loss is a design concern that should be addressed or explicitly documented as a known limitation.

---

## PLang/App/Errors/AskError.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. 29 lines. Clear purpose.

### Verdict: CLEAN

---

## PLang/App/Errors/DataSourceError.cs

### OBP Violations
None.

### Simplifications
None. The `ClassifyException` string-matching is fragile but there's no better way with SQLite exceptions.

### Readability
Clean. 63 lines. `FormatExtra` follows the established pattern.

### Verdict: CLEAN

---

## PLang/App/Memory/Data.Navigation.cs

### OBP Violations
None.

### Simplifications
None. Single-line change (`virtual` keyword added).

### Readability
No change to readability.

### Behavioral Reasoning
1. **Line 17: `virtual` on GetChild** — This is the enabling change for SettingsData. Risk: any future Data subclass that overrides GetChild must be careful about the depth parameter and path-splitting semantics. The base implementation is non-trivial (handles dots, brackets, nested navigation). Override authors must understand the full contract.

### Verdict: CLEAN
Minimal, surgical change. Correct.

---

## PLang/App/Context/Actor.cs

### OBP Violations
None. Actor owns its DataSource (OBP rule 1). Navigates Engine for file system and paths (OBP rule 2).

### Simplifications
None.

### Readability
Clean. 83 lines. Well-commented.

### Behavioral Reasoning
1. **Line 38: `_dataSource ??= CreateDataSource()`** — Lazy initialization with `??=` is not thread-safe. If two threads access `DataSource` concurrently on the same Actor, two SqliteDataSource instances could be created (one is discarded, the other leaked without Dispose). In practice, Actor construction and first DataSource access likely happen on the same thread, but this should use `Lazy<IDataSource>` or a lock.

2. **Lines 63-67: SettingsData registration is System-actor-only** — Correct. But uses `string.Equals(name, "System", ...)` — if actor naming changes, this silently stops working. Consider using a constant.

### Verdict: CLEAN
Minor thread-safety concern on lazy init, but pragmatically fine for current usage.

---

## PLang/App/actions/settings/get.cs

### OBP Violations
None. Navigates `Context.Engine.System.DataSource` — correct OBP chain.

### Simplifications
None.

### Readability
Clean. 32 lines.

### Verdict: CLEAN

---

## PLang/App/actions/settings/set.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. 25 lines.

### Verdict: CLEAN

---

## PLang/App/actions/settings/remove.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. 24 lines.

### Verdict: CLEAN

---

## PLang/App/actions/settings/types.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
1. **Lines 5-9: `types.setting` lowercase naming** — Follows PLang action type conventions. Consistent with codebase patterns.

### Verdict: CLEAN

---

## PLang.Generators/LazyParamsGenerator.cs

### OBP Violations
None.

### Simplifications
None. The generated code is necessarily verbose.

### Readability
1. **Lines 295-305 (generated): `__interpolationError` flag pattern** — The boolean flag + Regex.Replace callback is slightly convoluted, but necessary because Regex.Replace doesn't support early exit. The pattern is clear once you understand the constraint.

### Behavioral Reasoning
1. **`__resolutionError` only checked once, before `Run()`** — The error check at line 263 (`if (__resolutionError != null) return __resolutionError`) happens after property validation but before `Run()`. If a property is accessed *during* `Run()` (not during validation), its lazy resolution calls `__Resolve<T>`, which may set `__resolutionError` and return `default(T)`. But `__resolutionError` is never checked again after `Run()` returns.

   **Impact**: For required non-nullable properties, this is fine — they're validated before `Run()`, which triggers lazy resolution and catches errors. For nullable/optional properties only accessed inside `Run()`, a resolution error manifests as `null`/`default` rather than a clean error return. The `Run()` result (success or exception) takes precedence.

   **Severity**: Low-Medium. For Settings specifically, a missing setting on an optional parameter would be `null`, which is reasonable behavior. But the pattern is fragile — a required parameter that happens to pass null-check validation (e.g., value type with default) could silently use `default` on error.

2. **Lines 286-293 (generated): Error data from `Get()` vs `GetValue()`** — The change from `GetValue()` to `Get()` is critical for error propagation. Previously, `GetValue()` would call `Get()` internally, extract `.Value`, and discard the error. Now `Get()` returns the full `Data` with error information. This is the correct fix.

### Verdict: NEEDS WORK
The `__resolutionError` single-check pattern is a design limitation. It works for the current use case (required params fail validation, optional params get null) but is fragile for edge cases.

---

## PLang.Tests/App/Modules/datasource/DataSourceTests.cs

### OBP Violations
None. Tests are exempt from OBP production code rules.

### Simplifications
None.

### Readability
Clean. Good test names, clear arrange-act-assert pattern.

### Verdict: CLEAN

---

## PLang.Tests/App/Modules/settings/SettingsDataTests.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Comprehensive coverage of the SettingsData → Variables → handler chain.

### Verdict: CLEAN

---

# Cross-Cutting Findings

## Deletion Tests (Pass 5)

These code paths have no test that would fail if deleted:

| Lines | File | What | Risk |
|-------|------|------|------|
| 41-55 | SqliteDataSource.cs | `EnableWalMode()` | Low — performance optimization |
| 277-281 | SqliteDataSource.cs | `SanitizeTableName()` | **High** — SQL injection defense. Tests only use clean names. |
| 35-53 | DataSourceError.cs | `ClassifyException()` | Medium — error classification. All tests use happy-path. |
| 27 | AskError.cs | `FixSuggestion` | Low — UX hint, not behavior. |
| 72 | SettingsData.cs | Nested path navigation (`depth + 1`) | Medium — no test for `Settings.Config.SubKey` pattern. |
| 154, 263 | LazyParamsGenerator.cs | `__resolutionError` field + check | **High** — the entire error propagation pipeline from SettingsData through LazyParamsGenerator is untested. Tests exercise SettingsData directly but NOT through the generated code path. |

## Summary of Issues by Severity

### High
1. **LazyParamsGenerator error propagation is untested** — No test exercises the full path: PLang step with `%Settings.Key%` → generated code → `Variables.Get()` → `SettingsData.GetChild()` → AskError → `__resolutionError` → returned from `CodeGeneratedExecuteAsync`. This is the core integration that motivated the LazyParamsGenerator changes, yet it has no test.

2. **`SanitizeTableName` untested** — Security-critical code with no test coverage.

### Medium-High
3. **Variables.Clone() loses SettingsData type** — Cloning the System actor's Variables creates plain `Data` objects, losing the `GetChild` override. If System context is ever cloned, Settings lazy-loading breaks silently.

### Medium
4. **DeserializeValue bare `catch`** — Should be `catch (JsonException)` to avoid masking non-JSON errors.

5. **`__resolutionError` single-check pattern** — Errors from optional property resolution during `Run()` are silently swallowed.

### Low
6. **Actor.DataSource lazy init not thread-safe** — `??=` can create duplicate instances under concurrent access.
7. **Nested settings path untested** — `Settings.Config.SubKey` pattern has no coverage.
8. **ClassifyException untested** — Error classification logic has no test coverage.
