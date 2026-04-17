# Security Audit Plan — runtime2-builder-plan v1

## Scope
Full blue team + red team security audit of all 147 runtime source files changed on this branch.

## Threat Model
PLang is **user-sovereign**: the user owns their software. .pr files run in a trusted environment.
- **Trust boundary** = cryptographic signatures on Data
- **Don't flag user actions as attacks** — module.add, provider.load, mock.intercept, settings access are user prerogatives
- **Defend against untrusted external data** — HTTP responses, JSON from external APIs, template rendering with external data

## Methodology
Six parallel research agents audited:
1. Data/Navigators — type confusion, recursion, deserialization
2. Variable resolution — injection, traversal, scope leaks
3. Builder pipeline — prompt injection, .pr validation, merge safety
4. HTTP/File/Crypto/LLM — SSRF, path traversal, resource exhaustion, crypto
5. Engine/Goal/Step/Event — stack overflow, event loops, dispatch bypass
6. Actor/Config/Cache/Utils — serialization, cache poisoning, SSTI, mock in prod

## Findings Summary (after threat-model filtering)

### Findings that survive threat-model filtering (external data vectors):

| # | Severity | Category | Issue |
|---|----------|----------|-------|
| 1 | **HIGH** | Resource Exhaustion | HTTP file download has NO size limit (`StreamWithProgressAsync` writes unbounded to disk) |
| 2 | **HIGH** | Resource Exhaustion | SSE buffer overflow: clears buffer but keeps connection open, allowing repeated 10MB allocations |
| 3 | **HIGH** | Resource Exhaustion | HTTP slow-loris: no per-read timeout on stream reads, only overall CancellationToken |
| 4 | **HIGH** | SSTI | FluidProvider: `UnsafeMemberAccessStrategy` + `callGoal` tag + app/context in AmbientValues — external data in template variables can access full object graph |
| 5 | **MEDIUM** | Deserialization | `JsonStringNavigator`: no element count limit on unwrapped objects/arrays (10MB JSON → millions of small items → OOM) |
| 6 | **MEDIUM** | Stack Overflow | `JsonStringNavigator.UnwrapElement()` has no depth guard (relies on `JsonDocument.Parse` default depth=64, but no explicit guard) |
| 7 | **MEDIUM** | Crypto | Ed25519Provider header matching uses non-constant-time `string.Equals()` (line 104) — timing side-channel |
| 8 | **MEDIUM** | Crypto | Nonce replay protection depends on in-memory cache — server restart allows replay |
| 9 | **MEDIUM** | Info Disclosure | File error messages expose absolute paths (DefaultFileProvider lines 39, 76) |
| 10 | **MEDIUM** | Resource Exhaustion | `ResolveDeep()` has depth guard (100) but no breadth guard — wide nested structures cause O(N*M) allocations |
| 11 | **LOW** | SSRF | No URL scheme validation — `file://`, `gopher://` schemes accepted by HTTP module |
| 12 | **LOW** | Info Disclosure | ObjectNavigator error messages expose internal type names |

### Findings filtered out (user-sovereign / accepted risk):

- Module/provider loading without signatures → accepted risk, user's prerogative
- Mock module in production → user writes .goal files, not an external attack
- Settings access without ACL → user owns their app
- Builder prompt injection → user writes steps, not attacking themselves
- .pr file merge without semantic validation → build-time, user's environment
- GoalCall path traversal → user controls .goal files
- Error handler suppressing exceptions → user chose IgnoreError
- Foreach without iteration limit → user controls their loops
- Recursive goal calls → CallStack has MaxDepth=1000 guard (though Push() is underused)
- JSON deserialization in TypeMapping → operates on already-parsed internal data, not raw external input

## Deliverables
1. `security-report.json` — structured findings
2. `verdict.json` — pass/fail
3. `v1/summary.md` — narrative summary
4. `v1/result.md` — detailed findings with exploit sketches
5. Update `report.json` session entry
6. Commit and push
