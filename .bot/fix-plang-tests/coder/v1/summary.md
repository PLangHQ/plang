# v1 Summary — Code Analyzer Fixes

## What this is
Addressed all 23 findings from the code analyzer report on the runtime2-builder-plan branch. The fixes span core runtime (Data, Variables, condition orchestration), module OBP compliance (list/loop modules), and developer experience (headless build, dead code removal).

## What was done

### Critical fixes
- **Foreach dictionary (#1)**: `Data.EnumerateItems()` now yields `(Data key, Data value)` pairs. Dicts yield (dictKey, value), lists yield (index, element). Foreach and all list modules use this — no raw object access.
- **Condition guard (#2)**: Moved from `Context.Variables` to `Context._data` with step-scoped keys. PLang devs can't override it, inner goals don't collide.
- **ResolveDeep mutation (#5)**: Clones typed objects via `MemberwiseClone` before mutating string properties. Original .pr template data is never contaminated.
- **Data.Value instability (#4)**: Getter now resolves once and caches (like `_valueFactory`). `data.Value == data.Value` is guaranteed.

### OBP improvements
- **list.any, list.group**: Removed `ExtractProperty`/`ExtractKey` helpers. Use `Data.EnumerateItems()` + `GetChild(key)`. Data navigates itself.
- **list.contains, count, first, last, get, indexof, join**: All rewritten to use Data's navigation (`GetChild`, `EnumerateItems`) instead of reaching into `.Value` for raw `IList`/`IDictionary`.
- **loop/foreach**: Uses `item.Name = variableName; Context.Variables.Put(item)` — all Data, no extraction.

### Other fixes
- **As<T>() reflection (#17)**: Cached Resolve method lookups in `ConcurrentDictionary`.
- **Headless build (#18)**: `Console.IsInputRedirected` check + `App.Create` property (`--app={"create":true}`).
- **PromoteGroups (#8)**: Warning log for JsonElement no-op instead of silent failure.
- **Handled audit (#20)**: All 3 setters verified correct.
- **Action.Return**: Removed deprecated property and all references (debug, tests, methods).
- **StartGoal test**: Removed broken test with missing fixture file.

## Code example

The pattern change in list modules (before/after):

```csharp
// BEFORE: reaches into raw objects
var existing = Context.Variables.Get(ListName).Value;
if (existing is not System.Collections.IList rawList) return Error(...);
foreach (var item in rawList) {
    var propValue = ExtractProperty(item, key); // dict? json? poco?
}

// AFTER: Data owns everything
var data = Context.Variables.Get(ListName);
foreach (var (_, item) in data.EnumerateItems()) {
    var left = item.GetChild(key); // Data navigates itself
}
```

## Files modified
- `PLang/App/Data/this.cs` — EnumerateItems, WrapItem, Value getter cache, As<T> reflection cache
- `PLang/App/Variables/this.cs` — ResolveDeep clone
- `PLang/App/modules/condition/if.cs` — Guard on Context._data
- `PLang/App/modules/loop/foreach.cs` — Data-based iteration
- `PLang/App/modules/list/*.cs` — 9 files rewritten for OBP
- `PLang/App/this.cs` — App.Create property, headless check
- `PLang/Executor.cs` — --app parameter wiring
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — Return removed
- `PLang/App/Debug/this.cs`, `PLang/App/Goals/Goal/Methods.cs` — Return cleanup
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — SetValue warning
- `PLang.Tests/` — 6 test files updated
