# Security v2 Plan — Re-audit after coder fixes

## Context
Security v1 found 12 findings (3 HIGH, 6 MEDIUM, 2 LOW, 1 accepted-risk). Coder fixed all 11 open findings. This is a fresh-eyes re-audit to:

1. Verify all 11 fixes are correctly implemented
2. Scan with fresh perspective for anything v1 missed

## Approach
1. **Verify fixes** — Read the actual code (not just diff) for all 6 affected files
2. **Fresh scan** — Three parallel deep scans:
   - HTTP module: SSRF, header injection, redirect credential leaking, Content-Type confusion, throughput bypass
   - Variable resolution: ThreadStatic safety, variable injection, skipInfrastructure gaps
   - Action dispatch + events: .pr type injection, DLL loading paths, event bindings
3. **Write updated security-report.json** with fix statuses and any new findings
4. **Write verdict**

## Status
All fixes verified. Fresh scan complete. Writing reports.
