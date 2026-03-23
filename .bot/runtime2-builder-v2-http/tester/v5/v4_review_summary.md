# Review of Post-v4 Changes

Three commits since tester v4:
1. **Security fixes** (coder v4): size-limited reads (MaxResponseSize 100MB), SSE buffer cap (MaxSSEBufferSize 10MB), thread-safe SignedData.ToSigningBytes
2. **Provider DLL fixture fix**: committed real DLL fixtures, fixing 2 pre-existing test failures
3. **Auditor fixes**: FormatBytes error message, strengthened 5 of 7 weak assertions from auditor finding #3

All 1924 tests now pass (0 failures — DLL fixture issue resolved).
