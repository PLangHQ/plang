# Security Learnings — data-envelope-architecture

## 2026-02-21 — PLang's threat model is user-sovereign (CORRECTED)

**Severity:** critical learning
**Status:** permanent
**Details:** PLang's security model is NOT "defend against the user." The user owns their software. .pr files run in a trusted environment. Writing `set %!fileSystem% = null` is the user's prerogative — same as `Environment.Exit()` in C#. System variable writes and ObjectNavigator reflection are BY DESIGN.
**The real trust boundary:** Cryptographic signatures on Data. Once data is verified, everything inside runs with full trust. Security focuses on: (1) DoS at transport boundaries, (2) integrity of signature/verification, (3) robustness against malformed external input.
**Lesson:** Don't flag user-sovereign actions as injection vulnerabilities. Focus on where untrusted external data enters the system (transport channels, decompressed payloads). The wall is signatures, not .pr files.
**Source:** Creator review of v1 findings.

## 2026-02-21 — Verified must NEVER be a settable bool (UPGRADED to HIGH)

**Vector:** Verified is a public settable bool with no crypto backing
**Severity:** high — this IS the trust boundary
**Status:** open
**Details:** Signatures are PLang's wall. Everything inside the wall has full power. If `Verified` is a public setter, any code can claim "verified" without cryptographic proof. This directly undermines the trust model. The fix direction: `{ get; private set; }` or computed from Signature. A `VerifySignature()` method should do actual crypto and set Verified. No external code should be able to claim verified without a valid signature.
**Lesson:** Security properties that represent trust boundaries must be computed, never settable. A settable "verified" flag is a door in the wall that anyone can open.
**Source:** Creator review — "signatures are the wall, everything inside the wall has full power."

## 2026-02-21 — Unbounded recursion is the #1 DoS pattern

**Vector:** UnwrapJsonElement, RehydrateNestedData, GetChild, ResolveVariablesInPath, Clr() — all recurse without depth limits
**Severity:** high (at transport boundaries), medium (elsewhere)
**Status:** open
**Details:** Every time PLang processes structured data, it uses recursive descent without depth counters. StackOverflowException is unrecoverable in .NET. The highest-priority instance is RehydrateNestedData which sits at the transport boundary processing decompressed untrusted data.
**Fix:** Add `int depth = 0` parameter. Check against const max (100-128 for JSON, 100 for navigation, 20 for generics).
**Lesson:** Check EVERY recursive method for a depth parameter. At trust boundaries (transport), this is HIGH severity. In user code paths, it's lower.

## 2026-02-21 — Gzip bomb protection is good but transformation chain is incomplete

**Severity:** info
**Status:** mitigated (partially)
**Details:** MaxDecompressedSize=100MB is well-implemented. But no corresponding limit for downstream JSON parsing. 90MB JSON → 300MB+ CLR objects.
**Lesson:** Size limits at every transformation stage. Bytes → JSON → CLR is a 3x amplification chain.

## 2026-02-21 — Duplicate UnwrapJsonElement = inconsistent fixes

**Severity:** info
**Status:** open
**Details:** UnwrapJsonElement in both Data.cs and fromJson.cs. Fixes applied to one won't reach the other.
**Lesson:** Consolidate to single implementation, or flag both locations in every fix.

## 2026-02-21 — Newtonsoft shim: low practical risk

**Severity:** low
**Status:** open
**Details:** Namespace-based detection worth hardening (check assembly name), but if library.load already gives RCE, namespace spoofing adds nothing.
**Lesson:** Evaluate redundant attack vectors. If a prerequisite already grants full access, the downstream vector is informational, not blocking.
