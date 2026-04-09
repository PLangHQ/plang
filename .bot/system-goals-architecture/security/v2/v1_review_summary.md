# Review of Security v1 — Coder Fixes

Coder addressed 3 of the 12 findings (both HIGHs and one MEDIUM). All fixes are correct.

## Fixes Applied

### Finding 1 (HIGH): Binding.Run try-finally — FIXED ✓
- `Binding/this.cs:34-42`: Handler wrapped in try-finally, ExitEvent always executes
- Exact match to proposed fix
- No regressions — skipAction override logic preserved outside the try block

### Finding 2 (HIGH): Variable expansion from untrusted sources — FIXED ✓
- `Variables/this.cs:268`: Added `skipInfrastructure` parameter (default false for backward compat)
- `Variables/this.cs:276-277`: `%!var%` patterns left unresolved when skipInfrastructure=true
- `file/read.cs:26`: Passes `skipInfrastructure: true` — file content treated as untrusted
- Good doc comment explaining the purpose (lines 264-267)

### Finding 3 (MEDIUM): HTTP header injection — FIXED ✓
- `DefaultHttpProvider.cs:425`: CRLF stripped from header values before applying
- Simple `.Replace("\r", "").Replace("\n", "")` — effective against header injection
- Still uses TryAddWithoutValidation (not Add), but CRLF is the real attack vector and it's blocked

## Not Addressed (expected — lower severity)

- Finding 4 (MEDIUM): Event handlers outside step timeout
- Finding 5 (MEDIUM): LLM cache cross-actor leakage
- Finding 6 (MEDIUM): /system/ path fallback traversal
- Findings 7-12 (MEDIUM/LOW): symlinks, indirect event loops, LLM error disclosure, etc.
