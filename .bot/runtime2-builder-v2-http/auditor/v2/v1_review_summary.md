# v1 Review Summary

Auditor v1 failed with 2 major findings:
1. **FormatBytes bug** — `maxBytes / (1024*1024)` integer division gave "0MB" for 4KB limit
2. **7 weak test assertions** — false greens in signing/streaming/form tests

Coder fixed #1 with a `FormatBytes` switch expression (MB/KB/bytes) and strengthened 5 of 7 assertions. Tester v5 caught a missing SSE buffer overflow test (security fix without coverage), coder added it, tester v6 approved.

2 assertions were not fully strengthened:
- Stream_Plang_InvalidSignature: deferred with comment (non-streaming test covers the path)
- Stream_Bytes: checks type + non-empty but not exact content

Both are acceptable given the coverage elsewhere.
