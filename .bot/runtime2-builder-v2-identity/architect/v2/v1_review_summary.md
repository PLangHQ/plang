# v1 Review Summary

Test-designer (v1) raised two open questions after creating 48 test stubs:

## Question 1: Create duplicate name when archived exists

**Context:** `Create_DuplicateName_ReturnsError` tests that creating with a taken name errors. But what if that name is archived — re-create (new keys under same name) or still error?

**Decision:** Still error. The name is taken. Archived means inactive, not deleted. The old key pair's public key may have been shared, used in signatures, stored externally. Creating new keys under the same name would cause identity confusion. If the developer wants the name back, they unarchive it. If they want fresh keys, they pick a new name.

**Consequence:** Add `unarchive` action. Add `rename` action (developer needs to change identity labels without changing keys).

## Question 2: %MyIdentity.PrivateKey% — blocked at variable level?

**Context:** `DotNavigation_PrivateKey_IsBlocked` assumes dot navigation to PrivateKey should return null/error. Is `[Sensitive]` a variable-level gate or serialization-only?

**Decision:** Serialization only. Dot navigation works — `%MyIdentity.PrivateKey%` returns the private key. The developer is writing code, they have full control. Protection layers are:
1. **Serialization** — `[Sensitive]` strips it from JSON output, HTTP responses, logs (automatic safety net)
2. **Builder intent** — `export` action gets LLM-gated confirmation prompt (user-facing)
3. **Dot navigation** — code-level access, no gate

**Consequence:** Rename test from `DotNavigation_PrivateKey_IsBlocked` to `DotNavigation_PrivateKey_ReturnsPrivateKey` and assert it works.
