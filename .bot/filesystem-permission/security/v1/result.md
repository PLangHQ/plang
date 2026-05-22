# security v1 — filesystem-permission

Full security audit of the `filesystem-permission` branch. First security
pass; codeanalyzer (v1–v3) and tester (v1–v4) already cleared shape and test
quality.

## What the branch does

Every file action handler routes filesystem I/O through
`Path.Authorize(verb)`:

- **In-root** paths (under `RootDirectory` or `OsDirectory`) auto-grant —
  the actor owns its own root.
- **Out-of-root** paths look for an existing covering grant; on a miss they
  prompt the actor (`output.ask`, answer `y`/`n`/`a`).
- `y` → a session grant, unsigned, kept in an in-memory list, dies with the
  process.
- `a` → a persisted grant, Ed25519-signed via `EnsureSigned()`, written to
  the per-actor sqlite `permission` table.
- `n` → `PermissionDenied`.

`v5` dropped `PermissionRecord.AppId` so a persisted grant is keyed by
`(Actor + Path + Verb)` and survives a fresh `App` on the same root.

## Blue team — what holds

- **Grant creation has no PLang-reachable fabrication path.** Grants are
  built only in `Path.Authorize.SignAndStore` and
  `Path.Operations.StoreGrant`, both C#-internal and both gated behind a
  user-consent prompt. There is no `permission.add` action module.
  `settings.set` hardcodes the `"settings"` table — a goal cannot redirect
  it at `"permission"`. So a malicious goal cannot self-grant.
- **Path containment is correct.** `IsUnder` appends a trailing separator
  before `StartsWith`, so `/root` does not match `/rootEvil`. `Absolute` is
  the `ValidatePath`-normalised path (`GetFullPath` collapses `../`), so
  traversal in the raw input is already resolved. The `RootComparison`
  helper centralises OS-aware case sensitivity.
- **Fail-closed verification catch.** `Permission.VerifySignature` rethrows
  only the unrecoverable set (`NRE`, `OOM`, `StackOverflow`,
  `OperationCanceled`) and otherwise denies. Correct posture.
- **The Authorize loop is a loop, not recursion** — adversarial garbage
  input cannot grow the async state machine without bound (codeanalyzer's
  earlier fix).

## Red team — findings

### F1 — `signing.verify` does not pin the signer identity (Medium, latent)

`Ed25519.VerifyAsync` step 8 verifies the signature against
`signedData.Identity` — the public key carried *inside* the signature
envelope. It confirms "this signature is valid for the key it claims," never
"this key is trusted." `Actor.Permission.TryCover` treats a successful
verify as proof the grant is genuine.

Consequence: a grant signed by **any** Ed25519 keypair verifies. The only
actor binding, `PermissionRecord.Actor`, is a plain name string also under
the signer's control (and covered by the same attacker-chosen signature).
The Ed25519 signature therefore provides **no integrity over the sqlite
`permission` table against an adversarial tamperer** — anyone who can
rewrite a row can re-sign it with their own key. It only catches accidental
corruption.

Today the only write path to the `permission` table is direct tampering
with the sqlite file on disk — user-owned, out of immediate threat-model
scope — so this is Medium/latent. It ratchets to **High/Critical** the
moment that table becomes reachable by any non-disk input (channel sync,
settings import, a shared/multi-tenant store).

**Fix:** after `signing.verify` succeeds, assert
`grant.RawSignature.Identity == identity.Get().PublicKey` (the actor's
trusted identity). Deny grants signed by an untrusted identity.

### F2 — `TryCover` auto-trusts unsigned persisted rows (Low, defense-in-depth)

`Actor/Permission/this.cs` ~line 125:

```csharp
if (grantData.RawSignature == null) return true; // in-memory unsigned grant
```

The comment is true for the in-memory loop. But `Find` calls the same
`TryCover` for the **persisted-store loop** too — so an unsigned row in the
sqlite `permission` table is honored with no verification.

Currently unreachable: `Add` routes unsigned → in-memory, signed → sqlite,
and nothing else writes a `permission` table. But the gate's correctness
now rests entirely on that one routing convention in a sibling method — the
allocate-here / trust-there smell. A refactor, a legacy-grant import, or a
bulk-load path that drifts from it silently opens the gate.

**Fix:** the persisted loop must treat `RawSignature == null` as **deny**.
Parameterise `TryCover(allowUnsigned: bool)` or split into
`TryCoverInMemory` / `TryCoverPersisted`.

### F3 — persisted "always allow" grants silently expire after 5 minutes (Medium)

`EnsureSigned()` signs the `a` grant with no `Expires`, so
`Signature.Expires == null`. But `Ed25519.VerifyAsync` **step 2** rejects
any signature whose `Created` is older than `effectiveTimeout`, and
`Permission.VerifySignature` builds `new signing.verify { Data = data }`
with no `TimeoutMs` — so `effectiveTimeout` = `Config.TimeoutMs`, default
`300_000 ms = 5 minutes`.

So a persisted grant fails verification 5 minutes after it was created.
`TryCover` returns false; the user is re-prompted despite having answered
"always allow."

Two impacts:

1. **The documented contract is false.** `Permission/this.cs` says grants
   "survive `new App()` on the same root, which is the contract the `a`
   answer promises." They survive 5 minutes. Stage5 Scenario4 and the
   cross-App persistence tests pass **only because unit tests run in
   milliseconds** — they false-green a feature that does not work past the
   5-minute mark. A reviewer trusting "Scenario4 passes" is misled.
2. **The natural fix is a footgun.** The obvious remediation — raise
   `Config.TimeoutMs` — also widens the **nonce-replay acceptance window
   for every signed wire message** to the same duration. `TimeoutMs` is the
   transient-message anti-replay primitive; it is the wrong tool for a
   long-lived grant.

Behaviour is fail-closed (re-prompt), so F3 is not itself an exploitable
bypass — but it is a real correctness defect, it undermines the test
suite's credibility on the central feature of the branch, and its
tempting fix introduces a genuine replay weakness.

**Fix:** decouple grant verification from the wire-freshness window. (a)
Sign `a` grants with an explicit long/parameterised `Expires` (the
architect's deferred `AlwaysExpiry` intent, already a `todos.md` item noted
in `Path.Authorize.cs`). (b) `VerifySignature` should pass a `TimeoutMs`
that disables the `Created`-age check for grant verification, leaving the
grant's own `Expires` as the only time bound. Add a test that advances
`NowUtc` past 5 minutes. Do **not** fix by raising global `Config.TimeoutMs`.

### F4 — `RegexMatches` has no match timeout (Low, latent)

`Permission.RegexMatches` calls `Regex.IsMatch(candidate, pattern)` with no
`matchTimeout`. A catastrophic-backtracking pattern hangs the thread
(ReDoS). Currently unreachable — `BuildRequest` only ever sets
`Match.Exact`, so `Regex` grants are never created — but `PathMatches`
dispatches to it, so any deserialized `Match.Regex` grant (same write path
as F1/F2) or future Regex-grant code path makes it live.

**Fix:** `new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100))`
and catch `RegexMatchTimeoutException` → return false.

## Notes (not findings)

- **v5 scope expansion.** Dropping `AppId` makes the *root directory* the
  sole persistence scope. Any plang app run from the same root, with the
  same actor name, inherits every persisted grant. This is the deliberate
  v5 design (tester v4 reviewed it as correct) — recorded here only so the
  blast-radius is explicit: a persisted grant is no longer per-process, it
  is per-root, and with F3 fixed it would be effectively permanent.
- **Authorize re-prompt loop is unbounded.** An adversarial channel that
  returns infinite non-EOF garbage spins `Authorize` / `BundledTransfer`
  forever. Fail-closed (no grant is produced), single-threaded spin — a
  local liveness nuisance, not a privilege issue. Stream channels fail fast
  on EOF, which covers the common case.
- **codeanalyzer v3 follow-up** (`Path.cs:125,127` Relative getter still
  using `OrdinalIgnoreCase`) is **not** a security finding — `Relative` is
  observability, not a gate. Leave it to codeanalyzer / the polymorphic-Path
  branch.

## Verdict

**PASS.** No critical/high open findings. The consent gate's creation side
is sound; the four findings are all on the verification side and are
improvements to land — F1 and F3 should be closed before the `permission`
table ever gains a non-disk write path, F2 and F4 are cheap
defense-in-depth.
