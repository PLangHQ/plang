# Coder v1 Plan — Terminology Consistency Rename

Follow architect's 9-step execution order. Purely mechanical rename, no behavior changes.

## Steps

1. **Delete stale `IAction.cs`** entity interface + remove GlobalUsing aliases for it
2. **`git mv PLang/App/actions PLang/App/modules`** — folder rename
3. **Namespace replace**: `App.actions` → `App.modules` in all .cs files
4. **Rename `IClass` → `IAction`**: interface name, file name, all references
5. **Library internals**: `_handlers` → `_actions`, `handler` → `action`, tuple `Handler` → `Action`, `"HandlerError"` → `"ActionError"`
6. **Source generator**: update 3 namespace string literals in `LazyParamsGenerator.cs`
7. **Test files**: namespace + `IClass` → `IAction` references
8. **Build** both PLang and PLang.Tests
9. **Run full test suite**

## Risk

- Tuple field rename `Handler` → `Action` breaks call sites — must find all `.Handler` usages
- `"HandlerError"` → `"ActionError"` may affect test assertions
- Source generator string literals must match exactly
