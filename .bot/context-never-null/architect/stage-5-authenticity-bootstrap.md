# Stage 5: Authenticity & bootstrap

**Goal:** Make `application/plang` reads verify *who* signed, not just that a signature is valid — by navigating the reading actor's own identity inside verify. Handle the one read that can't match (the root identity load) explicitly with `verify.Root`.
**Scope:** The authenticity addition. Included: the identity match inside verify, `verify.Root`, the keypair self-consistency check's home, bootstrap ordering, the `%MyIdentity%` → `%Identity%` rename. Excluded: at-rest key encryption (separate hardening, not this branch).
**Deliverables:**
- `module/signing/code/Ed25519.cs` (`VerifyAsync`) — after the existing signature check, assert `layer.Identity == action.Context.Actor.Identity` (reduces to public-key strings). Verify **navigates** its own context — no expected-identity parameter is passed in.
- `module/signing/verify.cs` — add `Root` (bool, default false). When true, skip the actor-identity match (the rest of verify still runs). Follow the `SkipFreshnessCheck` shape; the name is not verb+noun.
- `module/identity/code/Default.cs` — the identity-table read sets `verify.Root = true`. After load, the provider checks the keypair is self-consistent (`PublicKey` re-derives from `PrivateKey`) — this check lives here, in the keypair's owner, not in verify.
- Bootstrap order — load the system identity (with `verify.Root`) before any other `application/plang` read, so `App.System.Identity` is in memory when settings/permission reads run. `App.System` is eager (Stage 1); the identity var resolves lazily (`actor/this.cs:125-129`) — force the root load first.
- Rename the PLang variable `%MyIdentity%` → `%Identity%` (`actor/this.cs:125`); update `IdentityHandlerTests`, `IdentityErrorPathTests`, `VariablesSnapshotTests`.
**Dependencies:** Stage 4 (verify-on-read is the active path), Stage 1 (`context.Actor` non-null).

## Design

The authenticity gap: today verify checks the signature against the public key *embedded in the signature* (`Ed25519.cs:133`), so a local-write adversary can re-sign with their own keypair and pass. The fix pins the signer to the reading actor: `layer.Identity == Context.Actor.Identity`. Verify is `IContext`, so it reaches the expected identity by navigation — passing it in would decompose the actor (OBP Rule #2).

The root read is the bootstrap exception — loading the system keypair is *how* `App.System.Identity` enters memory, so it can't match against itself. `verify.Root` says "no external identity to match"; the signature check still validates the self-signature, and the provider's post-load derivation check is what authenticates the root by possession. No bootstrap root-of-trust problem, and no null-trickery — the exception is an explicit flag.

Be clear-eyed (recorded in the plan): this is integrity + authenticity-of-signer, but the private key sits in sqlite in plaintext, so it catches tampering by others, not by someone who already read your key. Key-at-rest encryption is later hardening.

Full detail: `plan/mime-and-verify.md` (caveats + "Bootstrap: loading the root key").

## You own this

`verify.Root`'s final name (and whether the identity match reads better in `Ed25519.VerifyAsync` or in `ReadSignatureLayer`'s wiring) are yours. The contract: a system-owned read asserts the signer is the reading actor; the root read skips that match via an explicit flag; the keypair derivation check lives in the identity provider.
