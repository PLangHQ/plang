# Code Analysis v2 — Re-Review Result

Branch: `runtime2-builder-v2-http`

---

## v1 Finding Verification

### Finding 1: Provider disposal — RESOLVED
`Engine.DisposeAsync()` now iterates `Providers.All()` and disposes IDisposable/IAsyncDisposable providers. `Providers.All()` correctly flattens all ConcurrentDictionary values. HttpClient will be properly disposed.

### Finding 2: ExecuteHttpAsync catch-all — RESOLVED
Catch is now `when (ex is TaskCanceledException or HttpRequestException or IOException or UnauthorizedAccessException or FormatException)`. Programming errors (NRE, ArgumentException) propagate correctly.

### Finding 3: LoadAllAsync error swallowing — RESOLVED
`LoadAllAsync` now returns `Data<List<IdentityVariable>>`. All 6 callers (`CreateAsync`, `SetDefaultAsync`, `RenameAsync`, `ListAsync`, `GetOrCreateDefaultAsync`, and one I missed in v1 — the `Rename` caller) properly check `.Success` before using `.Value!`. Error propagation is complete.

### Finding 4: StreamPlangAsync malformed lines — RESOLVED
`JsonException` is caught, error reported to stderr via `engine.Channels.WriteAsync`. Continue is appropriate (don't kill the stream for one bad line).

### Finding 5: Ed25519Provider.Verify catch — RESOLVED
Narrowed to `FormatException or ArgumentException or CryptographicException or InvalidOperationException`. OOM and ThreadAbortException now propagate.

### Finding 6: Config.ResolvePrefix duplication — RESOLVED
`For<T>()` now calls `ResolvePrefix<T>()`. Single source of truth.

### Finding 7: SigningConfig naming — RESOLVED
Renamed to `Config`. Both `SignedData.CreateAsync` and `SignedData.VerifyAsync` updated to `engine.Config.For<Config>()`.

All 7 findings from v1 are correctly addressed.

---

## New Code: Disposal Lifecycle (CallFrame)

### File: Engine/CallStack/CallFrame.cs

#### OBP Violations
None. CallFrame owns its disposable list — behavior belongs to owner. Correct.

#### Simplifications
None.

#### Readability
Clean. Three focused methods: AddDisposable, TransferDisposable, DisposeAsync.

#### Behavioral Reasoning
1. **Line 198-202: TransferDisposable naming is slightly misleading** — Calls `_disposables.Remove(disposable)` then `target.AddDisposable(disposable)`. In practice, `Remove` is always a no-op because the disposable was never on the source frame — `TransferDisposable` is only called from `Action/Methods.cs` which never adds to the current frame first. It's effectively "register on parent frame." Not a bug (Remove is a no-op when item isn't found), but the name implies the item exists on the source frame. Minor naming concern — code is correct.

#### Deletion Test
- No direct test for AddDisposable/TransferDisposable/DisposeAsync on CallFrame. The `PopAsync()` path is tested via `CallStackIntegrationTests.cs`, but the disposal within it is not specifically verified.
- **Lines 190-215 could be removed without any test failing.** This is new infrastructure code without dedicated tests.

#### Verdict: CLEAN (minor naming concern, no tests for disposal)

---

## New Code: Action Disposable Transfer

### File: Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs

#### OBP Violations
None.

#### Behavioral Reasoning
1. **Lines 40-47: Transfer only happens when `this.Return != null`** — If a handler returns an IDisposable value but the step has no return variable mapping, the disposable is not tracked. It exists in `result` which goes out of scope — non-deterministic GC disposal. This is an edge case (who returns a disposable without capturing it?), but worth documenting.

2. **Lines 43-44: Root frame has no parent** — When `currentFrame.Parent` is null (root goal), the transfer is skipped. A disposable returned from a root goal's action is not tracked anywhere. The engine's DisposeAsync handles providers and KeepAlive objects, but not arbitrary return values. Again, edge case — root goals typically don't produce disposables for external consumption.

#### Verdict: CLEAN

---

## New Code: Engine KeepAlive

### File: Engine/this.cs

#### OBP Violations
None. Engine owns lifetime management.

#### Behavioral Reasoning
1. **Line 213: RemoveKeepAlive uses sync-over-async** — `ad.DisposeAsync().AsTask().GetAwaiter().GetResult()`. This is the same pattern as `IdentityData.ResolveDefault` — documented as safe because PLang runs sequentially per context with no SynchronizationContext. Acceptable.

2. **Line 205: KeepAlive accepts `object`** — No type constraint. Any object can be kept alive, but only IDisposable/IAsyncDisposable will be disposed. Non-disposable objects just get collected by GC when `_keepAlive.Clear()` runs. Not a bug, but `KeepAlive(object)` could mislead callers into thinking non-disposable objects get special treatment. Consider documenting this.

#### Verdict: CLEAN

---

## New Code: Providers.All()

### File: Engine/Providers/this.cs

#### Readability
1. **Lines 95-101: Duplicate `<summary>` XML doc** — The `Has<T>()` method's summary accidentally precedes the `All()` method's summary. Two `<summary>` blocks in a row. Compiler won't complain but documentation tools may pick up the wrong one.
   - Current:
     ```csharp
     /// <summary>
     /// Checks if any provider is registered for the given type.
     /// </summary>
     /// <summary>
     /// Iterates all provider instances across all types.
     /// Used for disposal and inspection.
     /// </summary>
     public IEnumerable<IProvider> All() => ...
     ```
   - Fix: Remove the first `<summary>` block (it belongs to `Has<T>()` which follows).

#### Verdict: CLEAN (cosmetic doc fix needed)

---

## New Code: Path Resolution

### File: Engine/FileSystem/Path.cs

#### OBP Violations
None. Path owns resolution, Copy, Move, Delete, Read, List, Save. Behavior belongs to owner. The action record is passed as parameter (`Delete action`, `Copy action`), letting Path navigate what it needs — OBP rule 2 ("navigate, don't pass") applied correctly.

#### Simplifications
None.

#### Readability
Clean. Cached properties for string-derived values. Clear separation between structural (IsFile, IsDirectory) and live (Exists, Size) properties.

#### Behavioral Reasoning
1. **Lines 51-60: Relative path resolution against goal folder** — Checks `!rawPath.StartsWith('/') && !rawPath.StartsWith('\\') && !rawPath.Contains("://")`. On Windows, this misses drive-letter absolute paths like `C:\foo`. But PLang's fileSystem.ValidatePath handles that — so the worst case is an extra Combine with the goal directory followed by ValidatePath normalizing it. Not a bug, but the intent check could be tighter.

2. **Lines 53-59: context.Goal can be null** — If `context.Goal` is null (e.g., during setup before any goal loads), `goalPath` is null, and the code falls through to resolve against engine root via `_fs.ValidatePath(rawPath)`. This is correct behavior.

3. **Line 33-38: Private constructor for absolute paths** — Used by `ResolveDestination`. Bypasses goal-relative resolution. Correct — destination paths are already resolved.

#### Deletion Test
- `PathTests.cs` (172 lines, modified) covers path resolution.
- `PrPipelineTests.cs` (395 lines, new) covers the full .pr pipeline with file paths.

#### Verdict: CLEAN

---

# Overall v2 Summary

## v1 Findings: All 7 Resolved
Every must-fix, should-fix, and minor finding from v1 has been correctly addressed.

## New Code Analysis

| Component | Lines | Verdict | Notes |
|-----------|-------|---------|-------|
| CallFrame disposal lifecycle | ~25 | CLEAN | No tests for disposal specifically |
| Action disposable transfer | ~8 | CLEAN | Edge case: root frames don't transfer |
| Engine KeepAlive | ~15 | CLEAN | Sync-over-async acceptable |
| Providers.All() | ~3 | CLEAN | Duplicate XML doc (cosmetic) |
| Path resolution | ~50 new | CLEAN | Goal-relative resolution correct |

## Remaining Items (non-blocking)

1. **Cosmetic**: `Providers/this.cs:95-97` — duplicate `<summary>` XML doc on `All()`. Easy fix.
2. **Documentation**: `KeepAlive(object)` could note that only IDisposable objects benefit from disposal.
3. **Deletion test gap**: `CallFrame.AddDisposable/TransferDisposable/DisposeAsync` have no dedicated tests. The infrastructure works but isn't proven.
4. **v1 deletion test carry-forward**: `TryExtractSignedErrorIdentity` and all streaming methods still have zero C# test coverage. This is a tester concern, not a coder concern.

## Overall Verdict: PASS

All v1 findings resolved. New code (disposal lifecycle, path resolution) is well-designed and follows OBP. No must-fix or should-fix issues remain. The cosmetic XML doc issue and missing disposal tests are non-blocking.
