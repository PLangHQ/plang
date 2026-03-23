# v1 Review Summary

v1 found 3 must-fix, 2 should-fix, 2 minor, and deletion-test findings.

## Coder's fixes (commit 49c9309d):

1. **Provider disposal** — Added `Providers.All()` method and provider disposal loop in `Engine.DisposeAsync()`. Also added `KeepAlive` pattern for objects that outlive their goal. **Fixed.**
2. **ExecuteHttpAsync catch-all** — Narrowed to `when (ex is TaskCanceledException or HttpRequestException or IOException or UnauthorizedAccessException or FormatException)`. **Fixed.**
3. **LoadAllAsync error swallowing** — Changed return type to `Data<List<IdentityVariable>>`, all 6 callers updated to check `.Success`. **Fixed.**
4. **StreamPlangAsync malformed lines** — Now catches `JsonException` and reports to stderr. **Fixed.**
5. **Ed25519Provider.Verify catch** — Narrowed to `when (ex is FormatException or ArgumentException or CryptographicException or InvalidOperationException)`. **Fixed.**
6. **Config.ResolvePrefix duplication** — `For<T>()` now calls `ResolvePrefix<T>()`. **Fixed.**
7. **SigningConfig naming** — Renamed to `Config`, updated references in `SignedData.cs`. **Fixed.**

## Additional coder work:
- Disposal lifecycle: `CallFrame.AddDisposable`, `TransferDisposable`, `DisposeAsync` on frame pop
- `Engine.KeepAlive` / `RemoveKeepAlive` for engine-level lifetime management
- Path moved from `Engine/Memory/Path.cs` to `Engine/FileSystem/Path.cs`
- Relative path resolution against goal folder (not engine root)
- `.pr` pipeline tests
