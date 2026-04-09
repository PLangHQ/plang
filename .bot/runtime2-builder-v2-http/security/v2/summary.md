# Security Audit v2 — runtime2-builder-v2-http

## What this is

Re-audit after coder fixed all 4 blocking findings from v1. Fresh review of the entire branch.

## What was done

Verified each v1 finding is properly addressed:

### Finding #1 (High → Fixed): Response body size limit
- `ReadLimitedStringAsync` / `ReadLimitedBytesAsync` — stream-copy with byte counter, throws `InvalidOperationException` on exceed
- Applied to ALL read paths: JSON (L510), XML (L528), text (L537), binary (L544), plang (L561), error (L653)
- `MaxResponseSize` in Config (default 100MB), configurable per PLang step
- `InvalidOperationException` caught by `ExecuteHttpAsync` → `ResponseTooLarge` 413
- Tests: oversized JSON, oversized binary, within-limit success

### Finding #2 (Medium → Fixed): ToSigningBytes thread safety
- Replaced mutate-serialize-restore with `TypeInfoResolver` modifier
- `ShouldSerialize = false` on Signature property in `SigningOptions`
- Zero mutation — pure read. Thread-safe by construction.
- Test: 100 concurrent `ToSigningBytes()` calls produce identical results, Signature intact after

### Finding #3 (Medium → Fixed): SSE buffer limit
- `maxBufferSize` parameter flows from Config to `StreamSSEAsync`
- Check at L844: `dataBuffer.Length + data.Length + 1 > maxBufferSize`
- On overflow: error to StdErr, buffer cleared, stream continues (doesn't crash)
- `MaxSSEBufferSize` in Config (default 10MB)

### Finding #4 (Medium → Fixed): Error body size
- `ReadErrorResponseAsync` uses `ReadLimitedStringAsync` with `MaxErrorBodySize = 4KB`
- Error body inherently truncated at read level — can't OOM

### Minor observation (not blocking)
- `InvalidOperationException` catch in `ExecuteHttpAsync` is now broader than before. If something other than size-limit helpers throws `InvalidOperationException`, it'd be mislabeled as "ResponseTooLarge". In practice, this is unlikely in the HTTP pipeline — and `ex.Message` would still be accurate. Not worth a custom exception type for this.

## Remaining findings (all accepted-risk from v1)

| # | Severity | Status | Reason |
|---|----------|--------|--------|
| 5 | Low | Accepted | STJ MaxDepth=64 default handles deep JSON |
| 6 | Low | Accepted | SignedData internal set — crypto validates regardless |
| 7 | Low | Accepted | Nonce cache per-engine — deployment concern, not code bug |
| 8 | Low | Accepted | SetDefault race — returns valid provider regardless |
| 9 | Low | Accepted | TryExtractSignedErrorIdentity bare catch — best-effort by design |

## Verdict

**PASS** — 0 critical, 0 high, 0 medium open. All blocking findings fixed. Recommend running the **auditor** next.
