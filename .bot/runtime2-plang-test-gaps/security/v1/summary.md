# Security Review v1 — runtime2-plang-test-gaps

## What this is

Security review of 7 runtime C# file changes on the runtime2-plang-test-gaps branch. The branch adds PLang integration tests and fixes 3 runtime bugs (step/goal return value propagation, setup discovery, PrPath keying).

## What was done

Reviewed all production code changes (excluding tests and .bot output) for:
- Input validation and injection vectors
- Deserialization safety
- Resource exhaustion
- Information disclosure
- Access control / trust boundary changes

### Key findings

**Net-positive security impact.** The branch improves security:

1. **Setup discovery narrowed** — Old code did `Directory.GetFiles(root, "*.pr", SearchOption.AllDirectories)`, loading every .pr file on disk. New code checks exactly 2 convention paths (`root/.build/setup.pr` and `root/Setup/.build/setup.pr`). This eliminates a resource exhaustion vector and reduces deserialization attack surface.

2. **PrPath validation added** — `Goals.Add()` now throws `ArgumentException` if `goal.PrPath` is null/empty, preventing garbage keys in the dictionary.

3. **DiscoverAsync made private** — Reduces public API surface.

4. **Test isolation improved** — Each test gets its own engine root instead of sharing `Tests/App/`.

### 3 low-severity findings (all accepted-risk)

1. **Bare catch in DiscoverAsync** — Swallows all exceptions from .pr deserialization. Pre-existing pattern, not introduced by this branch.
2. **Path in test error messages** — Developer-facing tool, not production surface.
3. **O(n) linear scan in Get()** — User controls goal count; not exploitable by external attacker.

## Verdict: PASS

No critical or high severity findings. Branch is safe to merge.

Recommend running the **auditor** next for code integrity review before merge.
