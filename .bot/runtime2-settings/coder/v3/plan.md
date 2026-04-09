# v3 Plan — Fix auditor/security findings

## 1. Add `Scope.Clone()` method
- File: `PLang/App/Settings/Scope.cs`
- New method: `public Scope Clone()` — creates new `ConcurrentDictionary` from existing entries
- Simple snapshot copy — new dictionary, same key-value pairs

## 2. Fix `PLangContext.Clone()` to deep-clone SettingsScope
- File: `PLang/App/Context/PLangContext.cs`
- Change: `SettingsScope = SettingsScope` → `SettingsScope = SettingsScope?.Clone()`

## 3. Narrow `Cast<T>` catch clause
- File: `PLang/App/Settings/this.cs`
- Change bare `catch` to `catch (InvalidCastException) catch (FormatException) catch (OverflowException)`

## 4. Add tests
- `ScopeTests`: Clone test (clone is independent copy)
- `SettingsTests`: Clone_WritesToClone_DoNotAffectOriginal
- Verify existing `Clone_PreservesSettingsScope` still passes (read behavior)

## 5. Run all tests, commit, push
