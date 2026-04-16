# Coder v7 Summary — Security Fixes

## What this is

Fixes for all 11 open findings from the security audit v1. Finding #4 (Fluid SSTI) was accepted-risk and skipped.

## What was done

### HTTP module — DefaultHttpProvider.cs (F1, F2, F3, F11)
- **F1**: Added `maxBytes` parameter to `StreamWithProgressAsync` — file downloads now enforce a configurable `MaxDownloadSize` (defaults to 100MB, matching in-memory limit)
- **F2**: Added `consecutiveOverflows` counter to `StreamSSEAsync` — disconnects after 3 consecutive buffer overflows without a successful event delivery
- **F3**: Added slow-loris throughput check to `ReadLimitedStringAsync`, `ReadLimitedBytesAsync`, and `StreamWithProgressAsync` — aborts if throughput drops below 1KB/sec for 30 seconds
- **F11**: Added URL scheme validation in `ResolveUrl` — only `http://` and `https://` allowed, rejects `file://`, `gopher://`, etc.

### Ed25519Provider.cs (F7, F8)
- **F7**: Replaced `string.Equals(Ordinal)` header comparison with `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes — eliminates timing side-channel
- **F8**: Documented that the existing timeout check (step 2) already bounds the nonce replay window after restart. Cache TTL already matches `effectiveTimeout`.

### JsonStringNavigator.cs (F5, F6)
- **F5**: Added `MaxElementCount = 100,000` — counter passed by ref through all Unwrap methods, throws `JsonException` if exceeded
- **F6**: Added explicit `depth` parameter to `UnwrapElement`/`UnwrapObject`/`UnwrapArray` — throws at depth > 64 (defense-in-depth, matching JsonDocument default)

### DefaultFileProvider.cs (F9)
- Removed `path.Absolute` from "File not found" error message — now only shows `path.Raw`

### Variables/this.cs (F10)
- Added `_resolveItemCount` breadth guard — resets at top-level call, stops resolving after 100,000 items

### ObjectNavigator.cs (F12)
- Removed `value.GetType().Name` from error message — now just says "Failed to read 'key': message"

## Code example — the throughput check pattern (appears in 3 read methods)

```csharp
var throughputStart = DateTimeOffset.UtcNow;
long throughputBytes = 0;

// inside read loop:
throughputBytes += bytesRead;
var elapsed = (DateTimeOffset.UtcNow - throughputStart).TotalSeconds;
if (elapsed >= 30)
{
    if (throughputBytes / elapsed < 1024)
        throw new InvalidOperationException("Response too slow — possible slow-loris attack");
    throughputStart = DateTimeOffset.UtcNow;
    throughputBytes = 0;
}
```

## Test results

2085/2086 pass (1 pre-existing LLM snapshot failure). No new failures introduced.
