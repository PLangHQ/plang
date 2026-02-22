# Tester v7 Plan — Verify Coder v5 Security Hardening

## Context

Coder v5 added security hardening: depth limits on 5 recursive methods, cycle detection on variable resolution, fromJson dedup, Verified → private set, plus tests for zip bomb, Merge, StatusCode, depth limits. 12 new tests, 1384 total pass.

## What to verify

1. All 1384 tests pass — no regressions
2. Each depth limit has adequate test coverage (boundary cases, integration paths)
3. Cycle detection in MemoryStack is tested
4. fromJson dedup works through the action, not just at Data level
5. Verified private set is enforced
6. GetChild behavior change (null → Data.FromError) doesn't break callers
7. Carry-forward: thread safety concurrent test still open from v6
