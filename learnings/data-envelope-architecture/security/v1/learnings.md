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

---

# Meta-Learnings — Process & Mindset

## 2026-02-21 — Establish the threat model BEFORE auditing

**Status:** permanent
**Details:** I walked in with a generic web-app threat model ("users are untrusted, defend every boundary") and burned effort on findings #4 and #5 that turned out to be features, not bugs. PLang is developer tooling — the user IS the developer. The question "who is the attacker and what do they control?" should be answered before writing a single finding.
**Lesson:** First session action for any new codebase: ask the creator about the deployment model and trust assumptions. Don't assume a web-app threat model. Developer tools, CLIs, language runtimes, and infrastructure software all have fundamentally different threat models.

## 2026-02-21 — Read previous bots' reports before starting

**Status:** permanent
**Details:** The auditor already identified thread safety issues, temporal coupling, and other findings. I partially re-covered that ground. The security analyst's job is adversarial analysis at trust boundaries — specifically what the auditor didn't do. Reading the auditor-report.json more carefully would have let me skip re-treading and focus on genuine attack vectors from the start.
**Lesson:** The multi-bot pipeline (architect → coder → tester → auditor → security) means each bot builds on the previous. Read what's already been done. Focus on what's missing, not what's covered.

## 2026-02-21 — The trust model determines finding severity, not technical impact alone

**Status:** permanent
**Details:** Finding #9 (Verified settable bool) went from "medium time bomb" to "the most important finding" once I understood that signatures are PLang's trust boundary. Without the trust model, I ranked it by current exploitability. With it, I ranked it by what it undermines. Conversely, #4 (system variable writes) went from "high injection" to "by design" — same technical capability, completely different meaning.
**Lesson:** A finding's severity is threat-model-relative. Two codebases with identical code can have opposite severity ratings for the same pattern. Always calibrate to the actual trust architecture.

## 2026-02-21 — Evaluate attack chains end-to-end before rating individual links

**Status:** permanent
**Details:** I rated #8 (Newtonsoft namespace spoofing) as medium because it enables code execution via reflection. But the prerequisite (library.load) already gives unrestricted code execution. The chain adds zero incremental risk. I should have traced the full chain first: "attacker needs library.load → which already gives RCE → so namespace spoofing is redundant."
**Lesson:** Map the full attack chain before rating individual vulnerabilities. If an early link already grants the attacker's goal, later links are noise. Rate the chain, not the link.
