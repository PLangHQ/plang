# v2 Summary — Fix hard cast + add missing tests

## What this is
Addresses the critical hard cast bug and missing test coverage identified by tester and code analyzer.

## What was done

### 1. Fixed hard cast in Resolve<T>
Replaced `(T)value` with a `Cast<T>` helper that tries three strategies:
1. `value is T typed` — exact match (fastest)
2. `Convert.ChangeType(value, typeof(T))` — handles numeric widening (int→long, etc.)
3. Falls back to `classDefault` on any conversion failure

### 2. Added 5 tests (1259 total, 0 failures)
| Test | Finding |
|------|---------|
| `Resolve_WidensIntToLong` | #1 — the critical false green |
| `Resolve_TypeMismatch_ReturnsClassDefault` | #1 — graceful fallback |
| `Resolve_SkipsNullScopeInParentChain` | #3 — 3-level chain with gap |
| `GoalRunAsync_ScopesSettingsPerGoal` | #2 — save/null/restore isolation |
| `Set_OverwritesExistingValue` | #4 — overwrite contract |

### Not addressed
- **Finding #5** (null value on Set): `ArgumentNullException` from `ConcurrentDictionary` is correct — settings shouldn't be null.
- **Finding #6** (more PLang tests): requires builder with `--llmservice=openai`.

## Code example

```csharp
private static T Cast<T>(object value, T fallback)
{
    if (value is T typed) return typed;
    try { return (T)Convert.ChangeType(value, typeof(T)); }
    catch { return fallback; }
}
```
