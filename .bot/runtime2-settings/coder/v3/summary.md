# v3 Summary — Fix auditor/security findings

## What this is
Fixes the major finding from auditor v2: `PLangContext.Clone()` shared `SettingsScope` by reference, causing cross-context mutation. Also narrows a bare `catch` in `Cast<T>` per auditor/security/tester consensus.

## What was done

### 1. Scope.Clone() — deep copy isolation
- **File**: `PLang/App/Engine/Settings/Scope.cs`
- Added `Clone()` method that creates a new `Scope` with an independent `ConcurrentDictionary` snapshot
- Clone and original are fully independent — writes to one don't affect the other

### 2. PLangContext.Clone() — uses Scope.Clone()
- **File**: `PLang/App/Engine/Context/PLangContext.cs` line 194
- Changed `SettingsScope = SettingsScope` → `SettingsScope = SettingsScope?.Clone()`
- Clone now gets an independent copy of settings

### 3. Cast<T> — narrowed catch clause
- **File**: `PLang/App/Engine/Settings/this.cs` line 54
- Changed bare `catch { return fallback; }` to `catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)`
- Critical exceptions (OutOfMemoryException, etc.) now propagate instead of being swallowed

### 4. Tests added (3 new, 1265 total)
- `ScopeTests.Clone_CreatesIndependentCopy` — verifies bidirectional isolation
- `ScopeTests.Clone_EmptyScope_ReturnsEmptyScope` — edge case
- `SettingsTests.Clone_WritesToClone_DoNotAffectOriginal` — the exact scenario the auditor flagged

## Code example

Before (reference copy — mutations cross-contaminate):
```csharp
SettingsScope = SettingsScope
```

After (independent copy):
```csharp
SettingsScope = SettingsScope?.Clone()
```
