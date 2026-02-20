# Auditor v4 — Review Summary

## What this is

Code review of the data-envelope-architecture branch, covering Phases 1-4 of the Data envelope system. This is the first auditor review — the tester approved v5, all 1372 tests pass.

## What was reviewed

4 phases of work adding self-describing Data to PLang Runtime2:

- **Phase 1**: `Engine.Types` — consolidates PLang name/CLR type, file extension/Kind/MIME, and compressibility into one live instance on Engine.
- **Phase 2**: Type gets context + lazy derivation. Data gets late-bound context. MemoryStack/PLangContext propagate context automatically.
- **Phase 3**: Data.cs split into 4 partial class files (core, result, navigation, envelope). `Out` view added for transport serialization.
- **Phase 4**: Envelope pipeline methods: `Wrap().Compress().Encrypt()` (outbound) and `Decrypt().Decompress().Unwrap()` (inbound).

## Findings

### 2 Major

1. **Engine.Types thread safety** — Uses mutable `Dictionary<>` and `HashSet<>` with public `Add()`/`Remove()` methods, but Engine is a singleton shared across concurrent PLangContexts. No synchronization. Concurrent mutations will corrupt state.

2. **RehydrateNestedData temporal coupling** — Mutates Data via Value setter (which clears `_type`), then immediately restores Type. Creates a window of inconsistent state and is fragile against future refactoring.

### 3 Minor

3. **Decompress uses generic `Error` instead of `ServiceError`** — Misses the project convention for service-layer errors with distinct keys.
4. **Compress double-navigates** — Bypasses `Type.Compressible` to call `Engine.Types.Compressible()` directly.
5. **Remove() uses O(n) ContainsValue** — Acceptable for current scale.

### 3 Nit

6. Newtonsoft `[JsonConstructor]` attribute on Data constructor (should be System.Text.Json only in Runtime2).
7. Decompress error tests don't assert `Error.Key`.
8. GZip helpers are unbounded (zip bomb risk for untrusted input).

## OBP Assessment

Generally strong compliance:
- Behavior belongs to the owner — Data.Wrap()/Compress() inspect themselves, decide action
- Navigate, don't pass — Pipeline navigates `_context.Engine.Types` for decisions
- Finding #4 (Compress double-navigation) is a minor OBP deviation

## Test Assessment

Tests are thorough. 1372 pass, covering all phases including error paths, multi-level nesting, and round-trip. Main gap: error key assertions (finding #7) should be added once error types are fixed.

## Verdict

**Approved with fixes recommended.** The two major findings should be addressed before merge. The thread safety issue (finding #1) is the higher priority — it's a latent race that becomes real as PLang adds concurrent execution or plugin loading.

## Files reviewed

- `PLang/Runtime2/Engine/Types/this.cs`
- `PLang/Runtime2/Engine/Memory/Data.cs`
- `PLang/Runtime2/Engine/Memory/Data.Result.cs`
- `PLang/Runtime2/Engine/Memory/Data.Navigation.cs`
- `PLang/Runtime2/Engine/Memory/Data.Envelope.cs`
- `PLang/Runtime2/Engine/Memory/MemoryStack.cs`
- `PLang/Runtime2/Engine/Context/PLangContext.cs`
- `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs`
- `PLang/Runtime2/Engine/View.cs`
- `PLang/Runtime2/Engine/this.cs`
- `PLang/Runtime2/GlobalUsings.cs`
- `PLang.Tests/Runtime2/Types/EngineTypesTests.cs`
- `PLang.Tests/Runtime2/Memory/DataTests.cs`
- `PLang.Tests/Runtime2/Memory/MemoryStackTests.cs`
- `PLang.Tests/GlobalUsings.cs`
