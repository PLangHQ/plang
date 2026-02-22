# Security Audit v2 — Post-Review Update

## What this is

Updated security report after creator review of v1 findings. The creator corrected the threat model: PLang is user-sovereign software where .pr files run in a trusted environment. The real trust boundary is cryptographic signatures on Data.

## What was done

- **Reclassified #4 and #5 as by-design** — System variable writes and ObjectNavigator reflection are intentional user capabilities, not vulnerabilities. Writing `%!fileSystem%` is the same as `Environment.Exit()` in C#.
- **Upgraded #9 (Verified) to HIGH** — Signatures are PLang's trust boundary. Once data is verified, everything inside runs with full trust. A public settable `bool? Verified` lets any code claim "verified" without cryptographic proof. This undermines the entire trust model. Fix: make it `{ get; private set; }` or compute from Signature via `VerifySignature()`.
- **Downgraded #8 (Newtonsoft shim) to LOW** — If `library.load` gives code execution, namespace spoofing adds nothing new. Worth hardening but not blocking.
- **Kept recursion findings (#1-3, #7, #10)** — These are real DoS vectors at transport boundaries where untrusted data enters.

## Priority (revised)

1. **#9 — Verified integrity** (HIGH) — Make `Verified` non-settable. This is the trust boundary.
2. **#1-3 — Recursion at transport boundary** (HIGH) — Add depth limits to UnwrapJsonElement, RehydrateNestedData, GetChild.
3. **#7, #10 — Recursion elsewhere** (MEDIUM) — Add depth limits to ResolveVariablesInPath, Clr().
4. **#6 — JsonStringNavigator size** (MEDIUM) — Add size limit.
5. **#8 — Newtonsoft assembly check** (LOW) — Harden when convenient.

## Key lesson

PLang's threat model is NOT "defend against the user." It's "defend the user's system against untrusted external data." The trust boundary is the signature verification on Data entering from transport. Everything inside that boundary runs with full user trust.
