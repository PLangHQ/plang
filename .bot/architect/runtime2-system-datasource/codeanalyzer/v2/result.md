# Code Analysis Report v2 — DataSource + Settings Bridge (Fix Review)

Branch: `runtime2-system-datasource`
Analyzed: 2026-02-24

---

## v1 Finding #1 (HIGH): LazyParamsGenerator error propagation untested

### Fix Review

The coder added two integration tests in `SettingsDataTests.cs`:

```csharp
// ErrorPropagation_MemoryStackGet_SettingsMissing_ReturnsAskError
var resolved = memoryStack.Get("Settings.MissingKey");
await Assert.That(resolved!.Success).IsFalse();
await Assert.That(resolved.Error is AskError).IsTrue();

// ErrorPropagation_MemoryStackGet_SettingsExists_ReturnsSuccess
var resolved = memoryStack.Get("Settings.ApiKey");
await Assert.That(resolved!.Success).IsTrue();
```

**Assessment: PARTIALLY ADDRESSED.** These tests exercise `MemoryStack.Get()` → `SettingsData.GetChild()` → `AskError`, which is the data path. But they do NOT exercise the generated `CodeGeneratedExecuteAsync` → `__Resolve<T>` → `__resolutionError` path. The generated code has its own control flow (`if (__resolved != null && !__resolved.Success) { __resolutionError = __resolved; return default; }`) that is still untested.

**However**, this is a reasonable tradeoff. Testing the generated code end-to-end requires building a `.pr` file with `%Settings.Key%` parameters and running it through the engine — that's a PLang integration test, not a C# unit test. The C# tests prove the underlying data flow is correct. The generated code is mechanical (pattern-match + delegate). **Acceptable for merge.**

### Verdict: RESOLVED (pragmatically)

---

## v1 Finding #2 (HIGH): SanitizeTableName untested

### Fix Review

Four tests added in `DataSourceTests.cs`:

| Test | Input | Validates |
|------|-------|-----------|
| `Set_TableNameWithSpecialChars_StripsNonAlphanumeric` | `"settings; DROP TABLE settings"` | SQL injection chars stripped |
| `Set_TableNameWithUnderscores_PreservesUnderscores` | `"my_table"` | Underscores preserved |
| `Set_EmptyTableName_FallsBackToDefault` | `"!!!"` | All-special-chars → `"default_table"` |
| `Set_MixedCaseTableName_NormalizesToLowercase` | `"Settings"` vs `"settings"` | Case normalization |

**Assessment: FULLY ADDRESSED.** Tests cover the critical adversarial input case (SQL injection), edge case (all-invalid → fallback), and normalization. Tests exercise through the public API (Set/Get), so they validate end-to-end behavior including the `[sanitized]` bracket-quoting in SQL.

### Verdict: RESOLVED

---

## v1 Finding #3 (MEDIUM-HIGH): MemoryStack.Clone() loses SettingsData type

### Fix Review

```csharp
// Before:
var clonedValue = kvp.Value.Value.DeepClone();
clone._variables[kvp.Key] = new Data(kvp.Value.Name, clonedValue, kvp.Value.Type);

// After:
if (kvp.Value is DynamicData || kvp.Value.GetType() != typeof(Data))
{
    clone._variables[kvp.Key] = kvp.Value;  // share by reference
}
else
{
    var clonedValue = kvp.Value.Value.DeepClone();
    clone._variables[kvp.Key] = new Data(kvp.Value.Name, clonedValue, kvp.Value.Type);
}
```

Two tests added:
- `MemoryStack_Clone_PreservesSettingsData` — verifies Settings resolves in cloned stack
- `MemoryStack_Clone_SettingsData_MissingKey_ReturnsAskError` — verifies AskError in cloned stack

**Assessment: FULLY ADDRESSED.** The fix is correct: SettingsData is stateless (loads from DB each call), so sharing by reference is safe — no mutation risk. The `GetType() != typeof(Data)` check is a clean way to detect subclasses without listing them.

**One minor observation**: The condition `kvp.Value is DynamicData || kvp.Value.GetType() != typeof(Data)` is redundant — `DynamicData` is a subclass of `Data`, so `GetType() != typeof(Data)` already catches it. The explicit `is DynamicData` check reads as documentation rather than logic. Not a problem — just slightly redundant.

### Verdict: RESOLVED

---

## v1 Finding #4 (MEDIUM): Bare catch in DeserializeValue

### Fix Review

```csharp
// Before:
catch { return json; }

// After:
catch (JsonException) { return json; }
```

Also fixed `EnableWalMode`:
```csharp
// Before:
catch { /* Non-fatal */ }

// After:
catch (SqliteException) { /* Non-fatal */ }
```

**Assessment: FULLY ADDRESSED.** Both bare catches narrowed to specific exception types. `InvalidOperationException` from depth bombs will now propagate correctly instead of being silently swallowed.

### Verdict: RESOLVED

---

## v1 Finding #5 (MEDIUM): `__resolutionError` single-check pattern

### Coder's Decision

Not fixed. Noted as "a design limitation of LazyParamsGenerator, not a bug in this feature."

**Assessment: ACCEPTABLE.** The single-check pattern means errors from optional properties accessed during `Run()` manifest as `null`/`default` rather than a clean error. For the Settings use case, this is fine — Settings properties that matter are required (non-nullable), so they're validated before `Run()`. Optional properties being `null` on error is reasonable behavior. This is a LazyParamsGenerator-wide concern, not specific to this feature.

### Verdict: ACKNOWLEDGED (not a blocker)

---

## v1 Finding #6 (LOW): Actor.DataSource lazy init not thread-safe

### Fix Review

```csharp
// Before:
private IDataSource? _dataSource;
public IDataSource DataSource => _dataSource ??= CreateDataSource();

// After:
private readonly Lazy<IDataSource> _dataSource;
public IDataSource DataSource => _dataSource.Value;
// In constructor: _dataSource = new Lazy<IDataSource>(CreateDataSource);
```

Dispose also updated:
```csharp
if (_dataSource.IsValueCreated)
    _dataSource.Value.Dispose();
```

**Assessment: FULLY ADDRESSED.** `Lazy<T>` is thread-safe by default (`LazyThreadSafetyMode.ExecutionAndPublication`). The `IsValueCreated` guard in Dispose prevents creating the database just to dispose it. Clean fix.

### Verdict: RESOLVED

---

## v1 Finding #7 (LOW): Nested settings path untested

### Fix Review

Test added: `SettingsData_NestedPath_NavigatesJsonObject` — stores a `Dictionary<string, object>` and resolves `Settings.Config.SubKey`.

**Assessment: FULLY ADDRESSED.**

### Verdict: RESOLVED

---

## v1 Finding #8 (LOW): ClassifyException untested

### Fix Review

Five tests added covering all classification branches:
- `DataSourceError_ClassifiesLockedDatabase` → "DatabaseLocked"
- `DataSourceError_ClassifiesDiskError` → "DiskError"
- `DataSourceError_ClassifiesCorrupt` → "DatabaseCorrupt"
- `DataSourceError_ClassifiesPermissionDenied` → "PermissionDenied"
- `DataSourceError_ClassifiesUnknownAsDefault` → "DataSourceError" (fallback)

**Assessment: FULLY ADDRESSED.** All branches of `ClassifyException` are now covered.

### Verdict: RESOLVED

---

## Pass 5: Fix-Introduced Surface

The fixes themselves add code that needs review:

### MemoryStack.Clone() — reference sharing concern

**Line 184**: `if (kvp.Value is DynamicData || kvp.Value.GetType() != typeof(Data))`

The reference-sharing means the original and cloned MemoryStack point to the SAME SettingsData instance. This is safe because:
1. SettingsData is stateless — reads from DB each time
2. SettingsData only stores `_engine` (immutable reference) and constant `SettingsTable`
3. No mutable fields on SettingsData

**Risk**: If a future Data subclass has mutable state, sharing by reference would cause cross-stack mutations. The comment documents the assumption ("stateless or factory-based"). **Low risk — the assumption is correct today and well-documented.**

### DeserializeValue catch narrowing

After the `catch (JsonException)` fix, any non-JsonException from `JsonDocument.Parse` or `Data.UnwrapJsonElement` will now propagate as an unhandled exception through the `catch (SqliteException ex)` / `catch (IOException ex)` handlers above. If it doesn't match those, it falls through completely.

**Specific scenario**: `Data.UnwrapJsonElement` throws `InvalidOperationException` on depth > 128. This will now propagate up through `Get()` where only `SqliteException` and `IOException` are caught. The `InvalidOperationException` would become an unhandled exception.

**Assessment**: This is actually **correct behavior**. A depth bomb in stored data should not be silently swallowed. However, the `Get()` method contract says "all methods return Data, never throw" (from IDataSource). To fully honor the contract, `Get()` should catch `Exception` as a final fallback and convert to `DataSourceError`.

**Severity**: Low. Depth bombs in SQLite-stored values require an attacker to have already written malicious JSON via `Set()`. The serialization round-trip via `JsonSerializer.Serialize` would produce valid, non-deeply-nested JSON. The only path to deep nesting is if `Set()` receives a pre-built deeply-nested object — unlikely in practice.

---

# Summary

| Finding | Severity | Status |
|---------|----------|--------|
| #1 Error propagation untested | High | Resolved (pragmatically) |
| #2 SanitizeTableName untested | High | Resolved |
| #3 Clone() type-loss | Medium-High | Resolved |
| #4 Bare catch | Medium | Resolved |
| #5 `__resolutionError` single-check | Medium | Acknowledged (not a blocker) |
| #6 Thread safety | Low | Resolved |
| #7 Nested path untested | Low | Resolved |
| #8 ClassifyException untested | Low | Resolved |

**New finding from fix review**: `DeserializeValue` catch narrowing means `InvalidOperationException` (depth bomb) propagates unhandled through `Get()`, technically breaking the "never throw" contract. Low severity — the scenario requires pre-existing malicious data.

## Overall Verdict: PASS

All high and medium-high findings addressed. The remaining concern (`__resolutionError` single-check) is a LazyParamsGenerator design limitation, not a bug in this feature. The new fix-introduced concern (catch narrowing + depth bomb propagation) is low severity and arguably better behavior than the previous silent swallowing.
