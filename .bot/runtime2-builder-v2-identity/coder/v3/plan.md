# Coder v3 Plan — Address Code Analyzer v2 Feedback

## Fix

1. **types.cs** — `GetOrCreateDefaultAsync`: check `SaveAsync` result. Throw `InvalidOperationException` on failure (keeps return type as `Task<IdentityVariable>`).
2. **get.cs** — Catch `InvalidOperationException` from `GetOrCreateDefaultAsync` and return `Data.FromError(new ServiceError(...))`.
3. **IdentityData.cs** — No change needed. `ResolveDefault()` calls `GetOrCreateDefaultAsync` via `GetAwaiter().GetResult()` — the throw propagates naturally and prevents returning a phantom identity.
