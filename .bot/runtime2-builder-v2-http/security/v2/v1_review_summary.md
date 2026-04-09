# Review of Security v1 — What Needed Fixing

v1 initially rated PASS with medium severities. Corrected to FAIL after recognizing the HTTP module's core job is safe external data handling.

## Findings sent to coder

| # | Severity | Issue | Fix requested |
|---|----------|-------|---------------|
| 1 | High | No response body size limit | Add MaxResponseSize to Config, size-limited read wrapper |
| 2 | Medium | ToSigningBytes not thread-safe | Serialize without mutating Signature |
| 3 | Medium | SSE StringBuilder unbounded | Add MaxSSEBufferSize check |
| 4 | Medium | Error body read unbounded | Size-limit error reads, truncate to 4KB |
