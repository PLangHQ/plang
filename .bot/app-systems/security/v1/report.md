# security v1 — app-systems pre-merge review

Branch: `app-systems` (tip `52904b8e0`). Base: `runtime2` at `0ea5a4b94` (2026-06-05). Range: eight branches, +54k −32k across 977 files in PLang/.

Scope: areas 1-6 from `.bot/app-systems/architect/briefs/security.md`. Threat model: user-sovereign PLang — the actor is the trust unit; a step the user did not approve must not read or write outside what its actor already holds. Read-only; no fixes.

**Tooling:** `scripts/semgrep-scan.sh` (24 blocking hits, baseline-known: 22 serializer-hygiene audit list + 1 Assembly.LoadFrom at the single gated loader + 1 `Display.Gate` which is a lock-object false positive, not a public collection). Full read of every area's own files + call-site grep for AuthGate / Authorize / FileSystem.Read|Write / Assembly.Load / Console. + a canonicalization regression check against the standing finding `pattern_authgate_canonicalization.md`.

## Verdict: PASS (no merge-blocking findings)

The eight branches merged into app-systems have not weakened the gate: every verb-side I/O entry still passes through `AuthGate`, the terminal sandbox still fails closed on non-Linux / Landlock < 3, `[Sensitive]` still cannot cross the Out wire, the signing pipeline still verifies before trust, and the settings store still partitions rows per actor. The two system invariants the brief singled out — decision 382 (os/ write hole closed) and the FilePath canonicalization regression (closed in purge-systemio-from-actions/v2) — both hold on the tip.

Four minor findings below. All **carry** — none blocks the merge; three are latent (surface the moment some other change lands).

---

## Findings

### F1 — `signing.verify` NRE on live signature with no expiry (Low, carry)

**File:** `PLang/app/module/signing/code/Ed25519.cs:129` (step 6, nonce record).
**Shape:** `VerifyAsync` step 1 (expiry) returns false when `Expires` is null; step 6 (nonce record) then dereferences `signature.Expires!.Value` unconditionally for any `Origin.Live` signature. `SignAsync` (line 36-37) accepts `action.Expires == null` and produces a signature with `_expires = null`.

**Reachability:** a legitimate caller invokes `signing.sign` without naming `Expires` → produces a live signature with no expiry → that wire reaches a `signing.verify` → step 1 passes, step 5 crypto-passes, step 6 NREs. Crypto result is correct; the pipeline then throws before returning the OK.

**Why it isn't an auth bypass:** fails safe — the NRE escapes `VerifyAsync` (the step-5 try/catch does not cover step 6), is caught by the action pipeline, and surfaces as a failed verify. No consumer sees "verified: true" when it is not.

**Why it still matters:** `SignAsync` and `VerifyAsync` disagree on whether a live signature may have no expiry. Either SignAsync should refuse `Expires == null` on live, or VerifyAsync's step 6 should either skip (no nonce recording possible) or default a window. **Carry** — tracker in `todos.md`.

### F2 — Case-sensitivity split between `GlobMatches` and `Places` (Low, latent, carry)

**Files:** `PLang/app/type/item/permission/this.cs:109-139` (regex, case-sensitive), `PLang/app/type/item/path/file/this.Validate.cs:80-83` (`StartsWith(..., OrdinalIgnoreCase)`).
**Shape:** the standing `/os/**` grant is a `Match.glob`. `GlobMatches` compiles it to a regex with no `IgnoreCase`. Meanwhile the `/system/` overlay in `Places` matches `OrdinalIgnoreCase`. On a case-insensitive filesystem (Windows/macOS default) `GetFullPath` may normalize casing differently from the stored `app.OsAbsolutePath` string.

**Reachability today:** none — both strings derive from `app.OsAbsolutePath` and `PathHelper.GetFullPath` preserves the folder's actual casing. No divergent path produces mismatch today.

**Why it still matters:** the two sides should agree on comparison discipline. A later change that routes `app.OsAbsolutePath` through a lower-casing normalizer would silently stop granting the standing read. The fix direction is "`GlobMatches` honours the platform's path comparison for file-scheme paths" (RegexOptions.IgnoreCase under `RootComparison == OrdinalIgnoreCase`). **Carry.**

### F3 — Standing in-memory grants are keyed only by Path (Low, note)

**File:** `PLang/app/actor/permission/this.cs:36-47` (`Stand`), :141-148 (`Add`).
**Shape:** the in-memory grant store (`_inMemory`) uses `Path` as the dedup key. A later `Stand(verbs...)` for the same path REPLACES the earlier one. This is intentional — the system actor's call at `actor/list/this.cs:15-17` is written to overwrite the base's `read+execute` with `read+execute+write+delete`.

**Why it still matters:** the invariant is "the ctor-base call runs before the system-list call" and nothing in a type system enforces it. A future change that reorders those two calls (or adds a third `Stand` on an actor subclass) would silently narrow or widen the system actor's rights. **Note; carry.** The fix direction is to make the system actor's extended rights explicit in one place (e.g. a protected `Baseline()` virtual the actor ctor calls, overridden on System) rather than relying on a later `Stand` to overwrite.

### F4 — Terminal sandbox: Landlock lock reaches network (documented, no action)

**File:** `PLang/app/module/terminal/code/Sandbox.cs:27-29` (doc comment).
**Shape:** Landlock 3 does not scope the network namespace. A sandboxed child process can still reach any TCP/UDP endpoint. The code's own XML doc states this openly. Linux 6.12 extends Landlock to signals and network; the current WSL kernel (6.6) does not have it.

**Not a finding — carrying the architect's own disclosure to make it audit-visible.** Any security review that enumerates the terminal sandbox has to name this; silence here would read as "I did not notice."

---

## Verified clean (bluetext — no finding, documented so a re-scan doesn't re-chase)

- **Decision 382 (os/ write hole)** — holds. `actor/permission/this.cs:29` seeds every actor with `read+execute` on `/os/**`; `actor/list/this.cs:15-17` extends only `System`. A user actor writing under /os/ hits the AuthGate ask prompt (`path/this.Authorize.cs:52-53` + hint at :124-126). Writing from a goal under /os/ is auto-granted only when `named=true` AND `AskedByOs(context)` — the asking step's goal folder is under `app.OsAbsolutePath` AND every path argument variable is one of the two anchors `%!app.AbsolutePath%` / `%!app.OsAbsolutePath%` (`path/this.Authorize.cs:83-90` + `terminal/type/program/this.cs:73-76`). A user goal an os goal calls stays the user's.
- **FilePath canonicalization** (standing finding `pattern_authgate_canonicalization.md`) — closed in purge-systemio-from-actions/v2 and still holds. `path/file/this.cs:31-48` canonicalizes every rooted input through `PathHelper.GetFullPath`; `..` and `.` are resolved before the string is stored. Confirmed via grep of every FilePath construction site — all route through the single public ctor.
- **Assembly loading** — only one `Assembly.LoadFrom` in production C# (`path/file/this.Operations.cs:34`), gated by `AuthGate(Verb.execute)`. Snapshot restore (`module/code/this.Snapshot.cs:109`) routes through the same gated verb.
- **Archive unpack** — `module/archive/type/archive/kind/bundle/this.cs:112-139` and `kind/this.cs:37-42` (`Under`) prevent `..`-escape; absolute symlinks are anchored to the unpack folder, not the host root (`path/file/this.Operations.cs:267-292`, `Follow`). Each kind of entry (Folder/File/Link/HardLink) goes through its own gated verb; devices/fifos are refused.
- **Terminal sandbox fails closed** — `Sandbox.Of` refuses with `PermissionNotEnforced 501` when `Unavailable()` reports non-Linux or Landlock < 3; `Sandbox.Start` wraps `Landlock.Restrict` in a background thread that throws unconditionally on lock failure. The program is never started unlocked.
- **`[Sensitive]` discipline** — `type/item/kind/reflection/Tagged.cs:102-154`: `[Sensitive]` is excluded in Out view, masked `****` in Debug, present (unmasked) in Store only. `type/item/kind/json/writer.cs:191-200` fails closed on unknown types rather than reflecting (would otherwise bypass `[Sensitive]`). Identity.PrivateKey rides on this. **Standing memory note (`pattern_tagged_transparent_fallback.md`):** types with ZERO wire tags fall through transparent — a new domain type on the wire with sensitive fields and no tag would leak. No new such type on this branch; verified via grep for domain types reflecting at Out view.
- **Settings store isolation** — `actor/setting/this.cs:174` builds the sqlite key as `"{actor.Name.ToLowerInvariant()}!{path}"`. `Read` filters by the same prefix. Actor names are the hard-coded set `{"System", "User"}` with no `!` char. A user actor's `Save` writes under `user!...`; the user fallback to System is read-only. Decision 712 ("who may name another actor") is open — nothing in the current code lets a step pick which actor's rows to write.
- **Signing pipeline** — `Ed25519.VerifyAsync` runs the 9-step gate (expiry → nonce replay → contract match → data hash → signature → nonce record). `ToSigningBytes` builds its bytes locally on a `MemoryStream` (thread-safe). Nonce replay uses the app cache with the signature's remaining lifetime. (One edge-case: F1 above.)
- **No `System.IO.*` reaches outside the `path.file.**` surface** (PLNG002 build error enforces). No `Console.*` writes in production outside the documented one-exception `PlangConsole/Program.cs:26` process-boundary. Semgrep rules enforce.
- **Pre-auth deserializer** — `data/Wire.cs:70-99` caps nested Data depth at 64 via AsyncLocal (prior security v1 F1). Still in place.

## Known, not findings (brief carried items)

- `[Sensitive]` and `sensitive` type are two mechanisms — unifying them carried (`next-branch.md`, decision 681). Checked for leaks: none new.
- Rotation of keys leaked into transcripts: skipped by Ingi — not re-raised.
- Decision 312 (goal written out showing current values): ruled not a leak (690) — not re-raised.
- Who may name another actor: open brainstorm (712) — reported what is possible today (nothing).

## Where this ran

Worktree: `/workspace/plang-app-systems-security` on `origin/app-systems` (52904b8e0). `./dev.sh build` clean. Semgrep run via `scripts/semgrep-scan.sh`. Signing-pipeline findings reproduced in-code only (no mutation; no sign/verify fixture needed to prove the Expires-null path by inspection).
