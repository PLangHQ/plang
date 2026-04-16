# Security Audit Results — runtime2-builder-plan v1

## Threat Model Applied

PLang is user-sovereign. The user owns their software, writes .goal files, and controls .pr generation. The trust boundary is cryptographic signatures on Data. External data (HTTP responses, SSE streams, JSON from APIs) is the real attack surface.

**Filtered out:** module loading, mock in prod, settings ACL, builder prompt injection, foreach limits, recursive goals — all user-sovereign actions.

---

## HIGH Findings

### Finding 1: HTTP File Download — No Size Limit

**File:** `PLang/App/modules/http/providers/DefaultHttpProvider.cs:911-948`

`StreamWithProgressAsync` writes to disk in 8KB chunks with **no size check**. The `DefaultMaxResponseSize` (100MB) only applies to `ReadLimitedStringAsync`/`ReadLimitedBytesAsync` (in-memory reads). File downloads bypass this entirely.

```csharp
// Line 925 — no size limit check
while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
{
    await destination.WriteAsync(buffer, 0, bytesRead, ct);  // Writes forever
    bytesTransferred += bytesRead;
    // No: if (bytesTransferred > maxBytes) throw ...
}
```

**Exploit:** Attacker serves multi-GB response to `download` action. Disk fills up.

**Fix:** Add `maxBytes` parameter, default to `DefaultMaxResponseSize`, check after each write.

---

### Finding 2: SSE Buffer Overflow — Connection Stays Open

**File:** `PLang/App/modules/http/providers/DefaultHttpProvider.cs:828-835`

When SSE message exceeds 10MB buffer, the handler clears the buffer and **continues** — it doesn't disconnect. An attacker can repeatedly trigger this cycle.

```csharp
if (dataBuffer.Length + data.Length + 1 > maxBufferSize)
{
    await app.Channels.WriteAsync(AppChannels.StdErr, ...);
    dataBuffer.Clear();   // Clears buffer
    continue;             // But continues reading! Attacker sends another 10MB.
}
```

**Exploit:** Attacker SSE server sends 10MB events repeatedly. Each cycle allocates ~10MB, clears it, allocates again. GC pressure causes degradation.

**Fix:** Add overflow counter. After 3 consecutive overflows, disconnect.

---

### Finding 3: HTTP Slow-Loris — No Per-Read Timeout

**File:** `PLang/App/modules/http/providers/DefaultHttpProvider.cs:290-306, 314-330`

Stream reads use `await stream.ReadAsync()` with only the overall `CancellationToken`. A slow server that sends 1 byte per interval keeps the connection alive indefinitely — each `ReadAsync` returns successfully with 1 byte.

```csharp
while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
{
    totalRead += bytesRead;  // 1 byte at a time — never exceeds maxBytes
    // No minimum throughput check
}
```

**Exploit:** Server sends 1 byte every 29 seconds. Connection held indefinitely.

**Fix:** Track `bytes_per_second` over sliding window. If throughput < 1KB/sec for 30 seconds, abort.

---

### Finding 4: Template SSTI via External Data (Accepted Risk)

**File:** `PLang/App/modules/ui/providers/FluidProvider.cs:69, 81-82, 24`

`UnsafeMemberAccessStrategy` + `app`/`context` in `AmbientValues` + `callGoal` tag = full object graph access from template expressions. If external data flows into template variables, attacker controls expressions.

**Status:** Accepted risk from prior audit. Template content is user-authored. Documented here for completeness.

---

## MEDIUM Findings

### Finding 5: JSON Element Count Amplification

**File:** `PLang/App/Data/Navigators/JsonStringNavigator.cs:59-76`

10MB JSON with 500K tiny items unwraps to Dictionary with 500K entries. Each entry has ~80 bytes overhead = 40MB+ heap from 10MB input.

**Fix:** Add `MaxElementCount` constant, check during iteration.

### Finding 6: JsonStringNavigator Depth Guard Missing

**File:** `PLang/App/Data/Navigators/JsonStringNavigator.cs:45-56`

`UnwrapElement()` recurses without explicit depth guard. Currently mitigated by `JsonDocument.Parse` default `MaxDepth=64`, but fragile — no defense-in-depth.

**Fix:** Add `depth` parameter matching `Data.UnwrapJsonElement` pattern.

### Finding 7: Timing Side-Channel in Signature Verification

**File:** `PLang/App/modules/signing/providers/Ed25519Provider.cs:104`

Header comparison uses `string.Equals(Ordinal)` — not constant-time. Core signature verification (line 118, `SequenceEqual` on `Span<byte>`) IS constant-time.

```csharp
// Line 104 — timing-vulnerable
!string.Equals(signedValue?.ToString(), kvp.Value?.ToString(), StringComparison.Ordinal)
```

**Fix:** Use `CryptographicOperations.FixedTimeEquals()` on UTF-8 bytes.

### Finding 8: Nonce Replay After Restart

**File:** `PLang/App/modules/signing/providers/Ed25519Provider.cs:85-89`

Nonce cache is in-memory only. Process restart clears it, allowing replay of captured requests.

**Fix:** Persist nonce cache to disk, or use timestamp-based nonce expiry (reject nonces older than N minutes).

### Finding 9: Absolute Paths in Error Messages

**File:** `PLang/App/modules/file/providers/DefaultFileProvider.cs:39, 76`

```csharp
return Data.@this.FromError(new ServiceError($"File not found: {path.Raw} ({path.Absolute})", ...));
```

**Fix:** Only include `path.Raw` in user-facing errors. Log `path.Absolute` server-side.

### Finding 10: ResolveDeep Breadth Explosion

**File:** `PLang/App/Variables/this.cs:412-434`

Depth guard (100) exists but no breadth guard. 1000x1000 nested structure = 1M items processed.

**Fix:** Add total-items-processed counter, cap at 100K.

---

## LOW Findings

### Finding 11: URL Scheme Not Validated

**File:** `PLang/App/modules/http/providers/DefaultHttpProvider.cs:458-460`

Any URL scheme accepted (`file://`, `gopher://`). HttpClient may reject most, but `file://` works on some platforms.

**Fix:** Whitelist `http://` and `https://` only.

### Finding 12: Internal Type Names in Error Messages

**File:** `PLang/App/Data/Navigators/ObjectNavigator.cs:28`

Error messages include `value.GetType().Name`. Minor info disclosure.

**Fix:** Use generic error message.

---

## Standing Open Findings (from prior audits, re-confirmed)

- **Image ReadAllBytes no size limit** (OpenAiProvider) — multi-GB file OOM. Medium.
- **Conversation continuity no length limit** — unbounded message accumulation. Medium.
- **Fluid MaxSteps not configured** → Actually NOW configured at 100,000 (line 65). **CLOSED.**
- **Data.Clone() shallow Properties** — copies by reference. Low.

---

## Overall Assessment

The HTTP module has 3 open HIGH findings, all in its core responsibility (safe external data handling). The signing module has 2 MEDIUM crypto issues. Everything else is MEDIUM or LOW. The architecture is fundamentally sound for a user-sovereign runtime — the gaps are at system boundaries where untrusted external data enters.

**Verdict: FAIL** — 3 HIGH open findings in HTTP module need coder fixes before shipping.
