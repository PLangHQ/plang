# security — item-doors pre-merge review

Tip `50ed915a1`, merge base `1adfb9555`. Read-only; one proving test added (red by design). Semgrep: 25 hits vs baseline 24; the extra (`type/this.cs` ~458-497) hand-builds JSON but escapes every value via `JsonSerializer.Serialize` — not an injection.

## Verdict: FAIL — one High (S1), the rest Low/informational

## S1 (High) — a step can mint a permanent grant for its own actor

- **Reach:** `set %!app.actor.permission.setting.grant% = [{actor:User, path:"/etc/x", verbs:all, match:glob}]` then `save %!app.actor.permission.setting%`. The node's `Set` (`PLang/app/type/item/setting/this.cs:130-153`) checks only that the option exists and writes the run layer; `setting.save` (`module/setting/save.cs`) stores whatever setting it is handed as the actor's signed row (`actor/setting/this.cs:182-196`). `Permission.Find` (`actor/permission/this.cs:95`) then answers the forged grant: the actor was never asked. Same route for `identity.setting` (`[Own]`).
- **Why High:** the consent prompt (`path.Authorize` → `Permission.Ask`) is the only thing between a third-party app's steps and out-of-root IO; this bypasses it silently and persistently.
- **Proof:** `PLang.Tests/Runtime/App/Settings/StepWrittenGrantTests.cs` (uncommitted; `./dev.sh test StepWrittenGrantTests`) — set and save succeed, final assert fails "Expected to be null but found permission". Re-run by me.
- **Age:** `save.cs` and the per-option write check are unchanged vs merge base, so likely pre-existing; it sits inside the "store just stores / one Set" area (257) so it is reported here.
- **Root fix:** an `[Own]` setting is not writable from a step or run values: the node `Set` and `Save` refuse an `[Own]` class, and `Under()` skips run values for it. Grants then arrive only through `Permission.Add` (after the prompt) / `Stand`; identity only through the identity module's own actions. (Locked-core adjacent → architect.)

## Lows / informational

- **L1** digest depends on `kept` enumeration order (`type/item/kept/list/this.cs`, `wire/kind/plang/writer.cs:75-84`): `ConcurrentDictionary` order feeds `Data.Digest`; two Datas with the same members added in different order digest differently (proved). Fails closed (spurious `DataHashMismatch`), not a bypass. Fix: canonical (ordinal or insertion) order.
- **L2** data reader accepts `name`/`variable` in Out view that the Out digest never covers (`data/reader/this.cs:56,86`). A wire altered in transit can set the top-level name; signature still verifies. Inert for `variable` (Template null, IsBuilt false). Fix: ignore both on Out-view reads.
- **L3** unread `{item,json}` caches its parse with the first opener's context (`kind/json/source.cs:22-26`, `kind/json/this.cs:82-88`): a dict opened by System is handed on to a user goal carrying System's context. PLAUSIBLE; not shown to matter. Fix: don't cache on the shared source.
- **L4** `wire/kind/plang/this.cs:107-113` catch narrowed to `AppException`/`JsonException`: `FormatException`/`InvalidOperationException` from an unsigned malformed wire escape raw. Fails closed; receive loop must survive. Fix: route through `Error.FromException`.
- **L5** console `bin` format writes raw bytes to a tty (`channel/type/console/this.cs:12-21`); only selectable by the user's own flag/row. Hygiene.
- **L6** regexes without timeout (duration parse ×2, `goal/step/this.Validate.cs:86`); backtracking linear-quadratic. Fix: `NonBacktracking`/timeout.
- **I1** `.data/setting/data.sqlite` is in-root so file verbs auto-grant reads; it holds the unmasked identity private key and accepts unsigned rows on read (`wire/kind/plang/this.cs:98-102`). Pre-existing; touches item 4. Fix: deny `/.data/**` to file verbs for non-system actors. Architect to rate.
- **I2** a Data keeps its `Signature` after in-place mutation; `verify` correctly fails, but `%x!signature.identity%` still names the signer — never use it as auth without `verify`.
- **I3** `IStore` swap via `code.load` is still `AuthGate(execute)`-gated; same accepted-risk class as other code providers.
- v1 F1 (Ed25519 NRE live/no Expires) closed: live signatures now always have a window (`signature/this.cs:124`).

## Checked, clean

Sign settles once and hashes the same Data; verify follows the variable and rehashes the bearer (`[Whole]`), digest never includes `Signature`; digest covers type/value/properties/kept; sign and verify views agree (Store only for `.pr`, view from local `Origin` never wire); Encode re-emits a held signature over the changed value (refused, never re-signed); list rows keep signatures; nonce recorded after hash+signature. `[Sensitive]` not reachable via `PrivateKey`, `!PrivateKey`, `!Value`, `!Context…` (probed). Wire: `MaxReadDepth=64` intact, `kept` recursion on the same default-depth reader, no new JsonReaderOptions/JsonDocument overrides, `kept` cannot dispatch a signature schema, `Record.cs`/`setting.Create` guards unchanged, no wire-named type instantiation. Settings: per-actor `<actor>!<path>` isolation; `[Own]` stops at the actor's layer in `Saved()`/`Under()`; only 4 callers of the blind `Set`, each checked. CLI/files: `--test <path>` resolves canonically and lists/reads through the gate; file channel gated then `WriteBytes`; `.pr` `file` bare-name rule, `Find` `..` refusal, `AskedByOs`/`IsInRoot` unchanged; no banned System.IO/Console/Assembly.Load/Process in added lines.

---

# Re-check at a3260edf5 (decision 300: S1, I1, L1, L4, L6)

## Verdict: FAIL — S1, L1, L4 closed; I1 is not closed (5 holes + 1 breakage risk); L6 reviewed by diff only

### S1 — CLOSED
Probed every step-reachable route to an `[Own]` row / run value: in-place `variable.Change`/`list.add` on `%!app.actor.permission.setting.grant%` (list mutated, write-back refused; `Find` stays null — proven with the real action), a variable typed as the permission setting handed to `setting.save`/`setting.remove` (`Refusal` reads the instance's own class, 403), a dict made into the base `setting` + kind path (declines `SettingsUnreadable`), the CLI / a child program's `--app.actor.permission.setting.grant=` line (`start.Set`, refused, pinned), callback/snapshot/wire reads (go through `Record.cs` into an instance; `Refusal` blocks at save; they write no row), `code.load` (still `AuthGate(execute)`). Blind `Setting.Set` callers (`Executor.cs:120`, `test/this.cs:174`) hit non-`[Own]` addresses; `Setting.Save` callers are owner code only (`permission/this.cs:192`, `identity/code/Default.cs:243`, non-`[Own]` `llm/setting`). `StepWrittenGrantTests` 6/6 green.
Informational: `identity.*` actions write the System identity row for whichever actor asks (`identity/code/Default.cs:223-244`) — the design question of whether a user actor may create/import the app's identities is the architect's.

### L1, L4 — CLOSED
`KeptDigestOrderTests` 1/1 and `MalformedWireTests` 3/3 green (run by me).

### L6 — by diff only
NonBacktracking / 1 s timeouts read correctly in the diff; a timeout throw answered as a 500 at the action/step boundary was not exercised.

### I1 — NOT CLOSED. Probe file `PLang.Tests/Runtime/App/FileSystem/PermissionTests/AuthorizeTests/StoresFolderProbeTests.cs` (committed with this re-check, 5/5 red, run by me: `./dev.sh test StoresFolderProbeTests`).

- **H1 (Medium) — an ancestor-folder verb carries `.data` out (P1).** `CopyTo`/`MoveTo`/recursive `Delete` of `/` pass `Standing` on the in-root top, then `PerformTransfer.Copy` (`path/file/this.Operations.cs:431-440`) recurses with no per-entry gate: `user.File(root).CopyTo(/snap, subfolder)` lands `/snap/.data/setting/data.sqlite` (unmasked identity key) in the user's reach; Move/Delete of `/` hit the stores likewise. Fix: gate per entry — skip or refuse an `IsStore` entry in the recursive verbs.
- **H2 (Medium) — symlink into `.data` (P3).** The check is lexical; `/lnk -> /.data` makes `/lnk/setting/data.sqlite` in-root and the OS follows it. An untrusted archive can plant the link (`bundle/this.cs:128` → `Link`, `Operations.cs:244`, `filesystem/this.cs:58-61`, verbatim target). The bypass is older, I1 inherits it. Fix: gate the resolved real path (deepest existing ancestor with links resolved), or refuse link targets leaving the unpack folder.
- **H3 (Low-Medium) — `//` form is asked, not refused (P2).** `//home/…/.data/…` keeps its `//` prefix (`path/file/this.cs` `Canonicalize`, `Validate.cs:41`); `IsUnder` compares it against `/home/…`, so it is neither `IsStore` nor in-root and falls to `Ask` — 'y' reads the key. Fix: normalise a leading `//` inside `IsUnder`/`IsStore`.
- **H4 (Low) — listing leaks names (P5).** Recursive `List` of `/` names `.data/…`. Fix: filter `IsStore` entries.
- **H5 (Low, macOS, not probed)** — `RootComparison` is Ordinal off Windows; on a case-insensitive FS `/root/.DATA/…` is in-root and not `IsStore`.
- **Breakage risk (P4):** `System` `Mkdir` on the bare `<root>/.data` is refused — the standing grant is `<root>/.data/**`, which does not match the folder itself, while `IsStore` does (equals). `store/sqlite/this.cs:83-84` calls `parent.Mkdir(Context)` and discards the result, so on a FIRST run (no `.data` yet) the store cannot create its folder and `ReadWriteCreate` then fails. Fix: include the folder itself in the standing grant (second `Stand` on the exact folder).

Clean: `..`/`.`, trailing separator, `.database` prefix confusion, the bare folder for a user, relative path against the goal folder; file Read/Open/Pour/Stat/Exists/Write/Append all gated; `Find` before the refusal cannot be exploited (a user can't mint a matching grant: `[Own]` closed, no ask can be raised on `.data`); pack opens each file through the gate; no goal, `os/system` file or test reads `.data` as the User actor, so denying it to users breaks no legitimate user path.

Root of H1-H3: the refusal is decided on the top path's lexical form; the stores' boundary needs to be decided on each entry actually touched, on its canonical resolved form.

---

# 313 — actor choice (ACCEPTED-RISK, owner ruling via architect 313f)

**S2 (Critical as a threat, ACCEPTED by Ingi's ruling "by design", relayed in 313f): a step may run a goal as the System actor, or replace its actor's channels, unchecked.** `actor/list/this.cs:35-41` `Use` returns `this[name].Context` with no check; reached by `goal.call` (`goal/call.cs:86`), `channel.set` (`channel/set.cs:38`) and `channel.remove` (`channel/remove.cs:17`). Ingi: running a goal as System is "programmatic and fine"; replacing channels is flexibility ("send all ask request to my computer through the network").

Recorded consequence, so the accepted risk is not misread later: S1 (`[Own]` settings), I1 (`.data` stores), K (private key asks) and the consent door guard what runs **as the User actor**. They do NOT stop a third-party app's own code from choosing System: such an app can `call goal X Actor=system` and have X write the os folder, read the identity key under `.data`, and answer its own asks. Running a third-party app is therefore trusting its code with System; the consent gate is a guard against what the User actor does unasked, not a sandbox against the app's code. Docs should say so in plain words wherever consent is described as protecting the user from an app.

What stays in scope under this model (reported if found): anything that reaches another actor, or answers consent, without the app's code choosing it (a variable/setting a consent reads, a wire-borne resume answer, a user-data-controlled actor name inside an os goal, `named`/anchor spoofing).

---

# Re-check 2 at f65796aae (decisions 303-315; model: Ingi 313e/313f)

## Verdict: FAIL — one High (R1), one Medium (R2), three Lows

### R1 (High, RED) — an `[Own]` row signed by ANY key is read as the actor's own
- `actor/setting/this.cs:280` (L(1)) only checks `row.Signature != null`. Verification (`wire/kind/plang/this.cs:90-97`) uses the public key inside the signature itself (`Ed25519.cs:208-210`); nothing binds the signer to this app's identity.
- **Reach:** a cloned/unzipped third-party app ships `<root>/.data/setting/data.sqlite` with a permission row the author signed with their own key. The victim opens the app, `Permission.Find` answers the grant, no prompt. Identity rows work the same way. This is the I1/S1 hole by a shipped store, not by a step or by the app's code choosing System — in scope under 313f.
- **Probe:** `ForeignSignedOwnRowTests.APermissionRow_SignedByAnotherKey_IsNotTheActorsGrant` (commit `6af58a552`), red: "Expected to be null but found permission". Not re-run by me; run by the fork that wrote it.
- **Root fix:** an `[Own]` row is read only if its signer is the app's own identity key (the first one created); the first-identity bootstrap needs an explicit rule (the first row is signed by the key it introduces, or the bootstrap writes before any read). Everything else is refused loudly, as an unsigned `[Own]` row already is.

### R2 (Medium, RED) — a tree copy writes through a link in the destination, no ask
- `path/file/this.Operations.cs` `Refusal` (510-519), relied on by `BundledTransfer`/`PerformTransfer.Copy`: an entry that would need an ask is treated as covered by the top's answer, but the top is the in-root destination. A destination holding `dst/d -> /outside` or `dst/x.txt -> /outside/target` (a cloned/unzipped app can ship it) is written outside with no prompt. Single-file writes through the same link ask correctly.
- Probes (`RealPathGateReCheckProbeTests`, commit `d35aeedbb`): `ACopyIntoATree_NeverWritesThroughALinkInIt_ToOutsideTheRoot`, `ACopyOverAFileLink_NeverWritesWhereItLeadsOutsideTheRoot`.
- **Root fix:** per entry, an `into`/`entry` whose landed place is not under the top's landed place is asked or refused, never "covered"; same for links re-created by `Relink`.

### Lows
- **R3** the consent question names the link, not where it lands (`path/file/this.cs` `Question`, the base `Question`, the bundled copy question). An in-root `notes.txt -> /outside/secret.txt` asks about `notes.txt`; 'a' persists a grant on the outside place. Probe `TheQuestion_NamesWhereTheLinkLands_NotJustTheLinksName`. Fix: name `place.Absolute` ("notes.txt, which is /outside/secret.txt").
- **R4** a link loop or a chain over 40 hops falls back to the lexical path (`Landing(...) ?? this`, `path/file/this.cs`). Linux refuses such a chain at the OS; on Windows the OS limit is higher, so a 41-63 hop chain lands outside while the gate reads in-root. Not testable here. Fix: a null landing is a refusal.
- **R5** `Variable.Get("NowUtc")` is a plain variable entry read by expiry (`signature/this.cs:83`); not tested whether a step can overwrite it (the app's own code choosing = out of scope under 313f; noted). Reading `signature.identity` runs `verify`, which records a Live nonce, so a second read may report a replay — functional, not security; not tested.

### Closed / clean (green, run by the forks)
S1 (`StepWrittenGrantTests` 6/6), I1 pins (`StoresFolderProbeTests` 5/5, `StoresFolderTests` 8/8, `StoresFolderOpenTests` 3/3, `EntryGateTests` 6/6, `DeleteMoveWindowTests` 2/2, `LinkEntryTests` 3/3, `RealPathGateTests` 6/6, `PersistedGrantCannotOverrideStoresTests` 7/7, `PathAuthorizeTests` 14/14), K (`IdentityPrivateKeyLeakTests` 11/11, `ConsentedExportMeansTheStepHoldsTheKeyTests` 4/4), `JsonSourceContextTests` 3/3, `MalformedWireTests` 3/3, `KeptDigestOrderTests` 1/1. 11 new gate probes pass: read/list/copy/move/delete through a user link to the stores, `lnk/../.data` judged on the canonical string, case/trailing-separator/bare `.data`/`//`/`.`/`..` forms refused with no ask, link to the os folder asks and writes nothing, `a -> .` copies as a link, lists without recursing and delete removes the link not its target, mkdir/append/delete through an outside link asks, a move of an ancestor of the stores is refused.
By reading: `Permission.Ask` asks `input.Ask` directly (never `output.ask`, `%!ask.answer%` or the ask event — the only reader of `!ask.answer` is the `output.ask` action); identity and export asks share the one door, per call, never persisted, and the export wording says what a yes means; `filesystem.root`/`os` are getter-only (`OsTrustTests.TheRootAnchor_CantBeSetByAUserGoal`); the store refusal runs before `AskedByOs`; `signature.identity` re-verifies its bearer per read (310); `Expired` through navigation (309); `Copy`/`Over` change no semantics.
Not covered: `OsTrustTests` printed no result in the run (unrun); Windows 8.3 / junctions (host run pending); hard links to the store file (no path gate can see them); whether the snapshot writer masks `[Sensitive]` (the snapshot tests in `IdentityPrivateKeyLeakTests` pass; writer not read); `ARegexTimeout_Answers500_…` exists, not run.

---

# Re-check 3 at 7e35b1b68 (R1-R4 fixes 2c024b6cd, 14cc1625a, 24120c4cf, 1abb11514; probes ad7d872be)

## Verdict: PASS for the findings; ONE RESIDUAL OPEN (R1b, rated Medium-High) that needs Ingi's explicit acceptance before merge

Tests, all 0 failed: ForeignSignedOwnRowTests 1/1, RealPathGateReCheckProbeTests 15/15, StoresFolderProbeTests 5/5, StepWrittenGrantTests 6/6, **OsTrustTests 21/21** (printed a result this time), IdentityPrivateKeyLeakTests 11/11, PersistedGrantCannotOverrideStoresTests 7/7, **EngineTests 34/34 incl. `ARegexTimeout_Answers500_AtTheActionAndTheStep_AndTheRunGoesOn`** (L6/M now exercised); new `RealPathGateReCheck3ProbeTests` 5/5 and `ShippedIdentityStoreTests` 3/3 (green by design, documentation).

- **R1 closed against a foreign key.** A row signed by a key that is not the app's own throws `SettingForeign` (`actor/setting/this.cs`); `Permission.Find` throws and `Setting.Get` returns the error key — neither answers a grant. The coder's changed last line of `ForeignSignedOwnRowTests` is acceptable: the refusal is loud as the fix asked, and the test still proves the grant is not answered. An own-signed row still reads (probed with the real provider by reopening the app). A System row copied under the User's key does not grant to the User.
- **R2 closed:** copy/move into a destination holding a link, and a link nested deep in it, no longer write outside the root. Not probed: unpacking an archive into a linked destination.
- **R3 closed** (the question says "X, which is Y"). **R4 closed** (a link loop or a chain past 40 hops is refused, `LinkLoop`, no ask; a link re-created by a copy is still gated at use).
- The two cosmetic renames (`Where()`, `Loop`) change no behaviour; only the question's wording. Diff since `f65796aae` read: nothing unsafe in the gate, settings, identity or signing.

## R1b (OPEN, Medium-High; not a re-check failure of the fix, a limit of it) — where an app's identity lives
`IsMine` (`module/identity/setting/this.cs`, `type/item/setting/this.cs`) takes "the app's identities" from the identity row inside the SAME store. A cloned/unzipped third-party app that ships its whole `.data/` — the identity row and the permission row both signed by the author — is read as the victim's own: indistinguishable from the app's own store reopened (pinned: `ShippedIdentityStoreTests.AStoreShippedWithItsOwnIdentityRow_IsReadAsTheVictimsOwn_UntilTheIdentityLivesOutsideTheStore`). The R1 fix stops only a foreign-signed row in a store that lacks the matching identity row (a partial ship); a whole-`.data` ship still seeds grants (and an identity that signs as the app) with no prompt.
- **Why I do not file it as "by design":** it is the same consequence as R1 and I1 (a shipped store gives an app standing grants nobody gave) reached by a whole-store ship. It is realistic for a zip/download (a repo normally ignores `.data`, a malicious archive does not). Under Ingi's 313 model the consent gate guards what runs as the User actor; here the app's CODE chose nothing — its DATA seeded the grants.
- **Root fix (Ingi's design call):** the app's identity (its root of trust) lives outside the app's folder store — the machine's/user's own key store (home-level), created on first run and never read from the app folder; an `[Own]` row is trusted only if signed by that key. A shipped `.data/` then carries rows signed by nobody this machine knows, and R1's `SettingForeign` fires. Interim mitigation if the design question is not decided before merge: refuse (or ask once, loudly) to open a `.data/` whose identity row was not created by this machine/this run — "this app folder came with a store; trust it?" — and say so in the docs; an unconditional trust of a shipped store should not be the default.
- **Verdict handling:** record as OPEN with the decision to Ingi (the architect is taking it). I recommend the merge goes ahead only with that decision made or the residual accepted in writing in the docs ("unzipping an app that ships its `.data/` trusts that store").

Not covered: Windows junctions / 8.3 names (host run), hard links to the store file, whether the snapshot writer masks `[Sensitive]` (snapshot tests pass; writer not read), unpack into a linked destination.

---

# R1b — ACCEPTED-RISK (Ingi, decision 317, 28cabc035)

R1b (a whole shipped `.data/` whose identity and permission rows the author signed is read as the victim's own) is accepted in writing by Ingi: "if they give us zip with .data then that is ok, it is what it is" — a downloaded app never has a private key or even a `.data` folder; the runtime creates those on first run, so a folder that arrives with one is that author's whole app, its `.pr` included (313e). Neither the root fix (identity outside the app folder) nor the ask-once guard is built. Residual stays pinned by `ShippedIdentityStoreTests`. Docs line: plang makes `.data/` on an app's first run, and an app folder that arrives with one is used as it is, identity and settings included. Per-actor stores and identities: later branch (`.bot/item-doors/architect/backlog.md` §4).

**Final item-doors security status:** no open findings; accepted risks S2 (actor choice, 313f) and R1b (shipped `.data/`, 317); informational/follow-up per decisions 300/305. My part is done unless the auditor's pass changes gate code.

---

# Re-check 4 at e502d8520 (X1 b4af67960, X2 3483f9d88, X3 02ffa24a7; probes 0d21f2b8c)

## Verdict: PASS — no findings

Tests, all 0 failed: LinkSpotTests 9, ArrivedSignatureTests 7, StoresFolderProbeTests 5, RealPathGateReCheckProbeTests 17, RealPathGateReCheck3ProbeTests 5, EntryGateTests 6, DeleteMoveWindowTests 2, LinkEntryTests 3, RealPathGateTests 6, StepWrittenGrantTests 6, PersistedGrantCannotOverrideStoresTests 7, OsTrustTests 21, ForeignSignedOwnRowTests 1, ShippedIdentityStoreTests 3, KeptDigestOrderTests 1, MalformedWireTests 3. New probes (green by design, `LinkSpotReCheckProbeTests` 4, `ArrivedNonceNotBurnedTests` 2): a file moved into a folder link leading out is asked and not moved on 'no'; a delete grant on a link never answers a read/write of its target; a listing of `dl/` is asked on where it lands; a move of a folder link moves the link and leaves the target's files; a tampered signature or hash does not burn the nonce and the genuine wire still arrives afterwards.

- **X1 clean:** a file is a one-entry tree judged by `Refusal`; the earlier copy/move probes into a folder holding a file link are green.
- **X2 clean:** delete judged at `Spot` (the folder's landing plus the entry's own name, last link never followed): an outside link into the root is asked; an in-root link to outside is free and its target kept; the delete grant is keyed on the link's place and never answers a read/write of the target; a move source is asked read+delete as one grant; a move over a link replaces the link; the trailing separator is trimmed in `Canonicalize` so the string judged is the string used; a folder link is removed with `Directory.Delete(p, false)` and never recursed; archive deletes answer their result.
- **X3 clean:** `Present` runs only after hash and signature pass (a forged copy cannot burn the nonce); it settles once by `CompareExchange`, shared by every copy of the read's layer; the early look is gone and the record is atomic; the same wire decoded twice or eight times concurrently arrives exactly once; only `Decode` builds a reading `Wire`, and a direct read meeting a Live layer with no window throws `LiveSignatureNeedsWindow` (fails closed).
- **Notes (no action):** grants on one place replace each other (one grant per place) — can only cause an extra ask, never a widening; a cached `Present` answer that threw stays failed for that arrival.
- **Not covered:** the Windows junction test (`LinkSpotTests` skips it here, the host runs it), 8.3 names and junction behaviour of `Directory.Delete(p, false)`, hard links to the store file, unpack into a linked destination.

**Final item-doors security status:** no open findings; accepted risks S2 (313f) and R1b (317).
