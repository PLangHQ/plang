# Coder v7 Plan — Security Fixes

Address all 11 open findings from security audit v1. Finding #4 (Fluid SSTI) is accepted-risk, skipped.

## Fixes by File

### 1. DefaultHttpProvider.cs (Findings 1, 2, 3, 11)

**F1 — Unbounded file download**: Add `maxBytes` parameter to `StreamWithProgressAsync`. Default to `DefaultMaxResponseSize` (100MB). Check after each chunk write. Pass from `DownloadAsync` with configurable `MaxDownloadSize`.

**F2 — SSE overflow loop**: Add overflow counter in `StreamSSEAsync`. After 3 consecutive overflows without a successful event, disconnect and return error.

**F3 — Slow-loris**: Add minimum throughput check to `ReadLimitedStringAsync`, `ReadLimitedBytesAsync`, and `StreamWithProgressAsync`. Track bytes over a sliding window; if throughput drops below 1KB/sec for 30 seconds, abort via CancellationToken.

**F11 — URL scheme validation**: In `ResolveUrl`, after constructing the final URL, validate scheme is `http` or `https`. Reject everything else.

### 2. Ed25519Provider.cs (Findings 7, 8)

**F7 — Timing side-channel**: Replace `string.Equals` header comparison with `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes.

**F8 — Nonce replay after restart**: Add timestamp-based nonce expiry. Reject nonces where `signedData.Created` is older than the timeout window, regardless of cache state. This bounds the replay window to the timeout duration (already checked in step 2 of verify). The existing timeout check already covers this — document it. The real gap is if the cache TTL and the timeout diverge. Ensure cache TTL matches the timeout.

### 3. JsonStringNavigator.cs (Findings 5, 6)

**F5 — Element count limit**: Add `MaxElementCount = 100_000`. Pass a counter ref through `UnwrapElement`/`UnwrapObject`/`UnwrapArray`. Throw if exceeded.

**F6 — Explicit depth guard**: Add `depth` parameter to `UnwrapElement`, increment on recursion, throw if > 64 (matching JsonDocument default).

### 4. DefaultFileProvider.cs (Finding 9)

**F9 — Absolute path in errors**: Change `Read` line 39 to use only `path.Raw`, not `path.Absolute`. The IOError catch blocks already use `ex.Message` which may contain paths — leave those as-is since they come from the OS.

### 5. Variables/this.cs (Finding 10)

**F10 — ResolveDeep breadth guard**: Add `[ThreadStatic] private static int _resolveItemCount` counter. Increment for each item processed in list/dict/object branches. If exceeds 100,000, return value unresolved. Reset at depth 0.

### 6. ObjectNavigator.cs (Finding 12)

**F12 — Type name in errors**: Replace `value.GetType().Name` with generic "object" in error message.

## Order of Work

1. HTTP module fixes (F1, F2, F3, F11) — biggest impact, all in one file
2. Ed25519 fixes (F7, F8)
3. JsonStringNavigator fixes (F5, F6)
4. File/Variables/ObjectNavigator (F9, F10, F12) — smallest changes

## Build & Test

- `dotnet build PLang/PLang.csproj` after each file group
- `dotnet run --project PLang.Tests` at end
- No PLang test changes needed — these are all internal hardening
