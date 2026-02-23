# Learnings — Code Analysis v1

## MemoryStack.Clone() creates plain Data objects — subtype information is lost

When `MemoryStack.Clone()` is called, it iterates `_variables` and creates `new Data(name, clonedValue, type)` for each entry. This means any `Data` subclass (like `SettingsData`, `DynamicData`) loses its runtime type and becomes plain `Data`. Any behavior depending on virtual method overrides (like `SettingsData.GetChild`) is silently lost in cloned stacks.

**Pattern to watch for**: Any time a new `Data` subclass with behavioral overrides is registered on a `MemoryStack`, verify that `Clone()` either preserves the subclass or the variable is treated as a system variable (skipped during clone).

## Source generators produce code that needs its own test coverage

The `LazyParamsGenerator` changes (error propagation via `__resolutionError`) are tested indirectly through SettingsData tests, but the actual generated code path is never exercised. The tests call `SettingsData.GetChild()` directly and test handlers with manually-set properties, bypassing the generated `CodeGeneratedExecuteAsync → __Resolve<T> → MemoryStack.Get()` path.

**Pattern**: When modifying generated code, always write a test that exercises the generated path end-to-end, not just the underlying components.

## Security-critical code needs dedicated tests even when the happy path works

`SanitizeTableName` is the SQL injection defense for dynamic table names. All tests use clean names like "settings" and "encryption". A test with `"; DROP TABLE settings; --"` would verify the defense actually works.

**Pattern**: For any security boundary (sanitization, validation, access control), write at least one test with adversarial input.

## Bare catches hide the unexpected

`catch { return json; }` in `DeserializeValue` is meant to handle invalid JSON, but it also catches `OutOfMemoryException`, `StackOverflowException`, etc. Always catch the specific exception type you expect (`JsonException` in this case).

**Pattern**: In PLang Runtime2, the convention is to catch specific exceptions at boundaries: `IOException | UnauthorizedAccessException` at filesystem boundaries, `JsonException | NotSupportedException` at serialization boundaries.

## The `??=` lazy initialization pattern is not thread-safe

`Actor.DataSource => _dataSource ??= CreateDataSource()` can create two instances under concurrent access. For reference types, this is usually tolerable (one instance is GC'd), but for types with external resources (like SQLite connections), it can leak. Consider `Lazy<T>` for resource-holding types.

## Virtual methods on Data are a powerful extension point but fragile

Making `Data.GetChild` virtual enables `SettingsData` to intercept navigation. But the base implementation has non-trivial semantics (dot/bracket parsing, depth tracking). Override authors must understand the full contract. The depth parameter and path-splitting logic create a subtle API surface.
