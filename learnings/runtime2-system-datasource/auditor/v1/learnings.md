# Learnings — Auditor v1, runtime2-system-datasource

## 1. SettingsData as a Data subclass is an elegant OBP pattern
Overriding `GetChild` on a Data subclass lets you intercept dot-notation navigation (`%Settings.ApiKey%`) and redirect it to a database read, while the rest of the system treats it as a normal variable. This is the OBP "behavior belongs to the owner" principle applied to data navigation — the SettingsData object owns how its children are resolved. **Source**: SettingsData.cs review.

## 2. Lazy<T> for resource lifecycle on Actor
Using `Lazy<IDataSource>` in Actor ensures the SQLite database is only created when first accessed. This avoids creating empty .db files for actors that never use persistence. The `DisposeAsync` checks `_dataSource.IsValueCreated` before disposing. **Pattern**: use `Lazy<T>` when a resource may not be needed and its creation has side effects (directory creation, file creation). **Source**: Actor.cs review.

## 3. Source generator error propagation via shared field
The LazyParamsGenerator uses a `__resolutionError` instance field shared across all property resolutions within a single `Execute()` call. This works because resolution is eager (all properties resolved upfront in Execute before Run is called), not lazy. If resolution were lazy (at property access time), this pattern would have race conditions. **Key insight**: the eager resolution in Execute is what makes the shared field safe. **Source**: LazyParamsGenerator.cs diff review.

## 4. DeserializeValue exception filter pattern
When a method calls another that can throw multiple exception types, the catch filter must cover ALL of them. `JsonDocument.Parse` throws `JsonException`, but `Data.UnwrapJsonElement` throws `InvalidOperationException` on depth > 128. The catch only covers `JsonException`. **Audit method**: list every method call inside a try block, check what each can throw, verify the filter covers all types. **Source**: SqliteDataSource.cs:256-270.

## 5. Variables.Clone must preserve subclass identity
Deep-cloning a `SettingsData` (which overrides `GetChild`) would create a plain `Data` copy that loses the virtual override. The clone method correctly detects non-Data subtypes (`kvp.Value.GetType() != typeof(Data)`) and preserves them by reference. **Rule**: when cloning a collection of polymorphic objects, check if the subclass has behavioral overrides before deep-cloning. **Source**: Variables.cs:194-196 review.
