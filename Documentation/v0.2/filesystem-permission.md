# Filesystem Permission System

The gate between PLang code and the host filesystem. Every file/directory
operation runs `path.Authorize(verb)` before touching disk, and only an
explicit user consent — or a previously-stored grant — lets the I/O
proceed.

This doc covers the **shape** of the system. For the OBP rationale behind
the `Verb` variant design, see `good_to_know.md` § "OBP Variant Design".

## Concepts

```
app.filesystem.permission/
├── this.cs                     -- PermissionRecord + Match enum
├── verb/
│   ├── this.cs                 -- Verb @this (composes Read/Write/Delete)
│   ├── Read.cs                 -- record Read(bool Recursive, bool Metadata)
│   ├── Write.cs                -- record Write(bool Create, bool Overwrite, bool Append, bool Mkdir)
│   └── Delete.cs               -- record Delete(bool Recursive, bool Permanent)
└── (Match: Exact | Glob | Regex)

app.actor.permission/
└── this.cs                     -- per-actor Find/Add/Revoke

app.filesystem/path.Authorize.cs -- the gate (Authorize, BuildRequest, SignAndStore)
```

**PermissionRecord** — `(Actor, Path, Verb, Match)`. Identity is the
three-tuple `(Actor + Path + Verb)`. `Match` decides how `Path` is
interpreted (`Exact` today; `Glob`/`Regex` reserved for grant policies).

**Verb** — always-present sub-options for Read/Write/Delete. A grant's
`Verb.Covers(request.Verb)` answers "does this grant cover this request?"
by per-sub-option boolean ≥. Defaulted to full capability so a hand-written
`new Verb { Read = new Read() }` means "all of Read."

## Authorize flow

`path.Authorize(verb)` is the single entry point. Every FS handler
(`file.read`, `file.save`, `file.copy`, `file.move`, `file.delete`,
`file.exists`, `file.list`) calls it before any I/O.

```
1. Resolve actor from Context (must exist; otherwise InvalidOperationException).
2. IsInRoot(path)? → Ok()                       (auto-grant for own root + OsDirectory)
3. actor.Permission.Find(path, verb)? → Ok()    (existing grant covers)
4. Loop:
     output.ask "Allow {actor} to {verb} {abs}? (y/n/a)"
     y → SignAndStore(persist:false)            → in-memory grant, no signature
     a → SignAndStore(persist:true)             → sqlite grant, Ed25519-signed
     n → Fail(PermissionDenied)
     other → re-prompt with "Invalid answer '{x}'." prefix
```

The y/a split is what the user sees at the prompt; the system encodes it
as **two grant homes**:

- **Session ("y")** — unsigned `Data<PermissionRecord>` in
  `actor.Permission._inMemory`. Lives until App disposes.
- **Persisted ("a")** — signed via `Data.EnsureSigned()`, routed to
  `app.SettingsStore` table `permission`. Survives `new app()` on the
  same root.

`Permission.Add` routes by signature presence (`signed.RawSignature != null`
→ sqlite; else in-memory). Same `Path` overwrites in either home.

## In-root short-circuit

`IsInRoot()` auto-grants any path under the actor's `RootDirectory` **or**
under `OsDirectory`. The OsDirectory exemption covers system-built-in
goals (test, build) that live outside the actor's root — those are
runtime-owned files, not user content.

The case comparison is `RootComparison` (OS-aware: `OrdinalIgnoreCase` on
Windows/Mac, `Ordinal` on Linux). **Carry-over (auditor F-C):** two call
sites still use `OrdinalIgnoreCase` directly (`Path.cs:125,127`,
`PLangFileSystem.cs:254`) where `RootComparison` belongs. Non-blocking;
tracked.

## Find — two-pass lookup

`actor.Permission.Find(path, verb)` builds a synthetic `PermissionRecord`
request and walks both homes:

1. In-memory list (snapshot under `_lock`, verify outside the lock so
   `VerifySignature` doesn't hold the actor lock).
2. Sqlite via `SettingsStore.GetAll<Data<PermissionRecord>>("permission")`,
   filtered client-side to `grant.Actor == _actor.Name`.

For each candidate, `TryCover` runs `grant.Covers(request)` and then —
if the grant has a signature — verifies it. Verification is cached in
the `Data` instance's `Properties` bag under `permission.verified`, so
repeat `Find` calls on the **same** `Data<PermissionRecord>` skip re-verify.
Sqlite grants always re-verify because `SettingsStore.GetAll` yields a
fresh `Data` per call.

## Grant verification — Ed25519 with SkipFreshnessCheck

`VerifySignature` constructs `app.module.signing.verify`:

```csharp
var action = new signing.verify {
    Data = data,
    SkipFreshnessCheck = new Data<bool>("", true),
};
```

`Ed25519.VerifyAsync` runs eight steps:

| Step | What | Run for grants? |
|---|---|---|
| 1 | Data-type check | yes |
| 2 | Wire-freshness (`Created + TimeoutMs`) | **skipped** |
| 3 | Expires lifetime (`Expires == null` → permanent) | yes |
| 4 | Nonce-replay cache | **skipped** |
| 5 | Contract check | yes |
| 6 | Header check | yes |
| 7 | Data-hash check | yes |
| 8 | Ed25519 cryptographic signature | yes |

Step 2 is skipped because grants are long-lived artifacts; a 5-minute
wire-freshness window would expire "always allow" after 5 minutes.
Step 4 is skipped because a persisted grant re-presents the same nonce
on every read — that is not a replay. The grant's `Expires` field
(step 3) is the only time bound that applies.

**Why this is safe (security v2's 4-step bypass-scoping template):**

1. Identify which checks the flag actually skips → steps 2 and 4.
2. Identify which checks still run → 1, 3, 5, 6, 7, **8**. Step 8 (the
   Ed25519 signature) is the core integrity gate; forgery still requires
   the actor's private key.
3. Enumerate every production site constructing `signing.verify`:
   - `app/actor/permission/this.cs:147` — single true-setter.
   - `app/modules/http/code/Default.cs:603, 636, 655, 917` — wire-message
     verify sites, all leave `SkipFreshnessCheck` default-false. Wire
     anti-replay is fully intact.
4. Confirm the bypass is correct *by design* in the one true-set site —
   not "tolerable", but the right behavior for stored grants.

Regression coverage:
- `Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow` advances
  `NowUtc` past `Config.TimeoutMs` and re-reads.
- `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt` reads
  twice with a stateless channel; each `Find` re-deserializes the grant,
  so each read is a real `VerifySignature` pass.
- Mutation-verified: flipping `SkipFreshnessCheck` true→false kills
  exactly one of those two on independent assertions (step 2 vs. step 4).

## Revoke

`Revoke(record)` removes from in-memory by `(Actor + Path)` and from
sqlite by `Path` key. Returns true if either home dropped a row.
`Scenario5_RevokeReprompts` covers the round-trip: revoke + next read
fires a fresh prompt.

## Known follow-ups (non-blocking)

These were called out by the reviewer chain and deferred deliberately.
They are tracked in `coder/v6/result.md`.

- **`RootComparison` thread-through.** `Path.cs:125,127` and
  `PLangFileSystem.cs:254` still use raw `OrdinalIgnoreCase`. (auditor F-C)
- **`Add` dedup-by-Path overwrites different-verb grants.** The class
  doc claims `(Actor + Path + Verb)` identity, but `Add` dedups by
  `Path` alone. Granting `Read` then `Write` on the same path drops the
  `Read` grant. Either the doc or the dedup key needs to move; the
  audit pass left it because no test pins it.
- **Bundled-consent prompts in `copy`/`move`.** `MoveCopyBundledConsentTests`
  exercises bundled consent on the v2 `Path` surface; the production
  `copy.cs`/`move.cs` handlers still issue two prompts. (auditor F-5,
  deferred with F-C/D/E.)
- **`output.ask` is text-only**, which forces the awkward
  `BuildRequest`/`SignAndStore` shape in `path.Authorize.cs` (the
  permission is built twice — once to format the question, once to seal
  on the answer). When `output.ask` grows structured options, the
  permission becomes a first-class option, defined once. Tracked in
  `Documentation/v0.2/todos.md`.

## File map

| What | Where |
|---|---|
| Authorize gate (the entry point) | `PLang/app/filesystem/path.Authorize.cs` |
| PermissionRecord + Match | `PLang/app/filesystem/permission/this.cs` |
| Verb (Read/Write/Delete) | `PLang/app/filesystem/permission/verb/` |
| Per-actor Find/Add/Revoke | `PLang/app/actor/permission/this.cs` |
| Permission denied error | `PLang/app/errors/PermissionDenied.cs` |
| Ed25519 verify (5+3 step pipeline) | `PLang/app/modules/signing/` |
| Tests | `PLang.Tests/App/FileSystem/Stage5MessagesEndToEndTests.cs` and siblings |

For the wider type catalog see `app-tree.md` (Actor.Permission line).
For OBP rationale on the Verb variant shape see `good_to_know.md`
§ "OBP Variant Design".
