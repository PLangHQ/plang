# item-doors — the auditor's final audit before merge

Range: my last verdict `1bc356ba9` → `origin/item-doors` `469003c50` (444 commits, 323 production files, +4.1k / −3.1k). Read-only on the code. I ran probes in two throwaway worktrees, then removed them. Nothing in them was committed. I did not read the codeanalyzer's report.

## Verdict: FAIL — three merge blockers, each a small fix

In plain words: the branch is in good shape. The security work holds up almost everywhere. The [Own] settings, consent through the actor's input, the stores' folder, `app.filesystem`, the json reader and the kept list all match the decisions. The reshape majors from my last pass (R1, R2) are fixed at the root.

Three things are wrong and are regressions in this range. Each was found by tracing the code and confirmed by a probe that fails today:

| # | What breaks | Where the root is | Proof |
|---|---|---|---|
| **X1** | Copying a **file into a folder** writes through a link in that folder to outside the root, unasked | the destination's entry is made after the gate (`Operations.cs:571`); the gate judged only the folder | probe: `/outside/target.txt` became "planted", 0 asks |
| **X2** | **Delete / move of a link** is judged by where the link leads, not by the link. An outside link into the root is deleted or moved unasked; the archive cleanup then drops the refused delete and says it was removed | `Location` follows the last part (`Operations.cs:302-305`) for verbs that act on the link itself; result dropped at `bundle/this.cs:129` | probe: an outside link deleted, 0 asks; moved, 0 asks |
| **X3** | A **signed value that arrives** can no longer be verified, and its signer can't be read: `verify %x%` and `%x!signature.identity%` answer NonceReplay | the boundary's verify spends the nonce (`plang/this.cs:93` → `Ed25519.cs:141-146`); the same Data, re-verified, meets its own nonce (`Ed25519.cs:96`) | probe red at 469003c50, green at 1bc356ba9 |

Everything else is next-branch work. It is listed with file:line in §Next-branch.

## Tests (469003c50, built fresh)

- C#: Modules 1324, Types 951, Data 863, Generator 191 and Runtime 1227 all green. Wire is 590/596: the known six snapshot tests (the snapshot branch, decision 223).
- plang (`cd test && plang --test`): 299 total, 294 pass, 1 fail, 3 stale, 1 skipped. The one failure is `BareLiteralJudgedByForm`, red by choice (278). At my last verdict it was 277/303 with 11 failures, so the line is better.

## Previous findings (mine)

- **R1** (wire template decided kept-or-used itself): **fixed**. `wire/template.cs` has no override, and the view decides (187).
- **R2** (any one-key dict list read as arguments): **fixed at the root**. `[Records]` sits on the slot (189/193), `formal/reader.cs:375-380`.
- **R3** (a standard's entry found by a type built from names): **still open, and has no home.** Decision 198 (Ingi: "sounds good") approved `type.list.Of(name, kind, …)` and making the by-name constructor the list's own. It was never built: 96 sites still write `new global::app.type.@this("…")` (`size/this.cs:38,63`, `duration/this.cs:60,84`, …). 211a built only `Of(kind, …)` (`type/list/this.cs:138`). Neither 198 nor F2 is in `backlog.md` or `v3-triage.md`. It is not a blocker, but it should be added to the backlog so it isn't lost.

## Blockers

### X1 — a file copied into a folder writes through a link there, outside the root, unasked

Same class as security's R2 (pre-merge, fixed for trees). This is the branch R2's fix doesn't cover.

1. `file/this.Operations.cs:439`: `target` is the **destination folder's** landing. `:447` asks `Standing` of that folder, which is in the root, so it answers Ok with no ask.
2. `:458`: the source is a file, so `tree` is null and `Refusal` (`:464`, where R2's per-entry check lives) never runs.
3. `:571`: `destination.Into(this, filesystem)` (`:85`) makes `dst/<name>` after the gate. It is never landed or checked.
4. `:573` → `filesystem/this.cs:154`: `System.IO.File.Copy(from, to, overwrite)` opens `dst/x.txt` and follows the link to its target.
5. Why it was missed: security's probe `ACopyOverAFileLink_…` (`RealPathGateReCheckProbeTests.cs:74-87`) copies the folder `src`, which goes through `Tree`. No test copies a single file into a folder that holds a link.

Probe (`x.txt` copied into `dst/`, where `dst/x.txt -> /outside/target.txt`, overwrite): success, **0 asks, the outside file now reads "planted"**. Reach: a cloned app ships the link, and a step copies or moves a file into the folder.

Fix at the root: the file case lands and judges the entry it writes, as `Refusal` does for a tree's entry. Make `Into(...)` before the gate and pass it to `Location` and `Standing` as the destination. The same applies to a move into a folder.

### X2 — a verb that acts on a link is judged by where the link leads

1. `this.Authorize.cs:30`: the gate judges `Location` → `file/this.cs:55` → `Landing` (`Operations.cs:302-305`), which reaches with `last: true` and so follows the path's own last part.
2. For `/outside/lnk -> <root>/in.txt`, the place is in the root. `Standing` → `IsInRoot` (`this.Authorize.cs:59`) answers Ok.
3. `Operations.cs:374-375`: `Delete` sees `Link(this)` and unlinks `/outside/lnk`, an entry outside the root, with no ask. A move goes the same way: `BundledTransfer` judges the source's landing (`:438,446`) and `filesystem.Move` renames the link itself (`filesystem/this.cs:141`).
4. The reverse also happens. An in-root `lnk -> /home/u/doc`: delete is asked as "delete lnk, which is /home/u/doc", and `Keep` (`this.Authorize.cs:49`) stores the delete grant on `/home/u/doc`, a file the user never named. With "a", a later real delete of that file goes unasked.
5. **Cascade (archive):** `bundle/this.cs:129` drops the result of `at.Delete(context)`. Because of steps 1–3, that delete of a leaving link is judged on its outside target: it is asked, and with "n" (or a non-interactive input) it is refused. The refusal is discarded, and `:130-131` answers "…it was removed" while the link stays. `:146` drops a `Delete` result the same way.
6. This contradicts decision 306(2), "delete removes the link; move renames it". The verb acts on the link, but the gate judges what the link leads to. Before the range, the gate judged the path as written, so this was asked.

Probe: an outside link into the root, deleted → success, 0 asks, the link is gone. The same link moved into the root → success, 0 asks.

Fix at the root: a verb that acts on the entry itself (unlink, rename) is judged where its parent lands, plus its own name. `Reach(..., last: false)` already exists for that. `Leaving` and `Land` answer their delete's result.

### X3 — a signed value that arrives can't be verified, or name its signer

1. `wire/kind/plang/this.cs:84-93`: Decode verifies a signed wire at the boundary, with deferred verify. For a Live signature, `Ed25519.cs:141-146` records its nonce.
2. Since 262 (c26b68bc9) the Data **keeps** that Live layer in memory ("in memory as off the wire").
3. `signature/this.cs:97-99` (4a191d418, 310): `%x!signature.identity%` runs `verify` on the bearer. An explicit `verify %x%` does the same.
4. `Ed25519.cs:95-97`: the signature is Live and its nonce is already in the cache, so it answers **NonceReplay**. This happens on the first read after arrival, not just the second.
5. Why it was missed: every identity and verify test signs in memory, with Origin Signed and no nonce (`VerifyActionTests.cs:196-214`). Security's R5 called it "a second read may report a replay — not tested". It is every read.

Probe: sign, Encode Out, Decode Out, then read `arrived!signature.identity`. It is NonceReplay at 469003c50, and `verify %arrived%` is NonceReplay too. The **same probe is green at 1bc356ba9**. What breaks is the use signing exists for: verifying a message from a peer and naming who sent it.

Fix at the root: the nonce guards a *wire read*, not a Data. The boundary's verify is the read. A verify of the Data it produced, already in memory, re-checks the hash and signature but doesn't present the nonce again. One way is for the Data that left the boundary's verify to carry that its nonce was spent by it. Which shape is the architect's call. A red test for both reads should come first.

## Next-branch (not blockers), with file:line

Security / gate:
- **N1** `identity.export` can't export. It returns `Data<Identity>` whole (`identity/code/Default.cs:208`), but `PrivateKey` is `[Sensitive, Store]` with no `[LlmBuilder]` (`identity/type/identity.cs:33`, decision 201). So `%privateKey%` holds the identity, whose text is its **public** key (`:52`). The consent asks "the step gets the key as text it can send anywhere" (`Default.cs:204`), and the teaching promises a raw string (`os/system/modules/identity/export.*.md`, `export.cs` doc). It fails safe: no leak, but a silently wrong value and a question that says something untrue. The tests take `.PrivateKey` in C#, which no step can do (`ConsentedExportMeansTheStepHoldsTheKeyTests.cs:40`, `IdentityPrivateKeyLeakTests.cs:55`). This belongs to 307 (critical actions). Until then, either fix the question's wording or have export refuse loudly.
- **N2** Archived identities: 316b's note, "rows read as foreign until saved again", is optimistic. There is no route that saves them again.
  1. The base `IsOwn` takes the active identities only (`type/item/setting/this.cs:142` → `Default.cs:190`). The identity setting's own `IsOwn` counts archived ones (`identity/setting/this.cs:24`), so two definitions of "the app's identities" disagree (**fork**).
  2. After a rotation (setDefault new, archive old), `Get<permission.setting>` throws `SettingForeign` (`actor/setting/this.cs:342-345`).
  3. So `Find` throws (`permission/this.cs:140`), every out-of-root verb throws before it can ask, and `Add` throws before it can re-save (`:157`).
  - Unarchiving is the only way back. The fix is one definition, on the identity module (archived ones included); it also removes the core setting's reach into `module.identity.code.IIdentity` (`setting/this.cs:140`).
- **N3** A failed "always" save is silent: `Add` returns `Task` and drops `Save`'s answer (`permission/this.cs:162`), and `Keep` answers Ok (`this.Authorize.cs:49-50`).
- **N4** A one-time "y" on the os folder itself replaces its standing grant. `Add` overwrites the same-path in-memory entry (`permission/this.cs:169-171`), including `Stand`'s exact grant (`:43`), so {read, execute} becomes {write}.
- **N5** `PersistedGrantCannotOverrideStoresTests` doesn't pin what its comment says. Its fixture overwrites the store with text, so `Add(persist)` never saves (see N3), and all 7 cases pass whatever the order. `Standing` actually asks `Find` before `IsStore` (`this.Authorize.cs:60-61`). That order matters because System's store access is itself a grant. Assert `Find` is non-null after the persist.
- **N6** The action-option reader `actor/setting.Get(action, option)` (`actor/setting/this.cs:105-116`) reads run values and the saved row without the `[Own]` checks. No option collides with an `[Own]` address today.
- **N7** Consent over a message channel suspends forever. The pending `Ask` is returned (`permission/this.cs:91`), and nothing pre-binds the answer on resume. This is latent, since no message channel ships.
- **N8** `Permission.Ask` turns any exception (except NRE/OOM/SO) into PermissionDenied (`permission/this.cs:84`), so a real bug reads as a "no".

Signature / readers:
- **N9** On egress, a Data that holds a signature is never re-signed (`wire/kind/plang/this.cs:38`). A Copy keeps its layer (`data/this.cs:741`), and Store and Out digest differently (`View.cs:40,135,144`). So a Data read from the store and sent out (or the reverse) fails `DataHashMismatch` where it lands, and a forwarded Data carries the first sender's nonce. There is no production path today. The architect should rule on egress, with a red test.
- **N10** The json reader's `Number()` doesn't check its token (`kind/json/reader.cs:59`). `"value": true` from a peer becomes a typed null number with no error. The L4 rule is incomplete.
- **N11** Type readers (size, parallel, choice, …) throw `FormatException`, and kind throws `NotSupportedException`. Since 279 narrowed the catch to AppException/JsonException, these escape Decode raw and land as a generic 500. The fix is in the readers.
- **N12** A Data signed with contracts can't name its signer: the identity read's verify passes no Contracts, and `ContractsMatch` fails (`Ed25519.cs:151-155`). The `sign.cs:15` doc "Default: [C0]" is stale.
- **N13** `kind.Make` gained `variable`, `built` and `format` parameters beyond the `(raw, type, origin)` that 209 approved. The surface is re-pinned; this is a line for the architect.

Filesystem / start:
- **N14** Windows: the build seeds `%path%` with the raw working folder (`Executor.cs:102,112`), but compares it to the landed root (`build/code/Default.cs:35,39`). A folder reached through a junction or an 8.3 name builds the wrong folder. Seed it from `app.filesystem.root`.
- **N15** `path/file/this.Validate.cs:46`: `path.StartsWith(rootAbsolutePath)` is culture-sensitive and has no separator boundary. `%!app.filesystem.root%/x` now relies on it. Use `IsUnder`.
- **N16** `app/start` edge cases. Each is answered quietly instead of refused:
  - a second positional becomes `Other=true` (`start/this.cs:42-55`);
  - a dotted positional is read as a property (`:124`);
  - `--test=module/x` drops the value (`Executor.cs:73`);
  - `--debug=false` with `--debug.setting.goal=…` still turns debug on;
  - repeats that differ only in case are both kept (`start/property/list/this.cs:200`).
- **N17** A test that can only pass on Linux: `OsTrustTests.cs:54` compares with a `/` separator.

Docs (for the docs pass):
- **N18** Stale names:
  - `CLAUDE.md:11,27` (`App.AbsolutePath`/`OsDirectory`/`OsAbsolutePath`) — by proposal;
  - `CLAUDE.md` "Debugging" teaches `--debug={…}`, which the branch now refuses (`ExecutorTests.cs:300-307`);
  - `Documentation/v0.2/bans.md:47-48`, `app-tree.md:63,74`, `filesystem-permission.md:46,71`, `debug.md:212,234`, `scripts/check-app-tree.sh:79`, `os.targets:2`.

## Checked, clean

- **[Own] settings:**
  - Every write door refuses an [Own] class: node `Set`, `Apply(path…)`/`start.Set`, `setting.save`/`remove`, and the terminal's child line.
  - `Under()` takes no run value for one, and `Saved()` reads the actor's own row only.
  - An unsigned row is refused, and a foreign-signed one is refused loudly (316b).
  - Only owner code calls `Setting.Save`.
- **Consent:**
  - It asks the actor's input directly, never `output.ask`, `%!ask.answer%` or `on.ask` (`permission/this.cs:71-81`).
  - No input, EOF or an unanswered ask means denied. Identity asks never keep "always".
  - Matches 312/312b/313e/313f.
- **Identity:**
  - get, create, list and the mutations answer `Public()`.
  - `PrivateKey` is not navigable, and Out drops it.
  - Debug masks it (`View.cs` `Sensitive` → `****`), and no error carries a key.
- **Gate:**
  - Every file verb passes `AuthGate` or `Standing`; http's are gated too.
  - The stores' folder is refused case-blind (`this.Authorize.cs:72`), and its folder's own Mkdir is fixed by the exact `Stand`.
  - There is no bare catch.
- **`app.filesystem`:**
  - No consumer of `AbsolutePath`/`OsAbsolutePath` is left in C#, `.goal`, `.llm`, module `.md` or a tracked `.pr`. The browser goals and their `.pr` use `%!app.filesystem.*%`.
  - The surface was re-pinned citing 314, and isOs is path equality (`build/this.cs:73`).
  - `CommandLineParser` is gone with no reference left.
- **Signature:**
  - Sign settles once and hashes and holds the same Data. Verify rehashes the bearer, and neither digest includes the signature.
  - `Expired` is navigated (309), `Copy(value, signature)` is private (310 B), and contracts cross as bare strings (315).
  - The nonce is recorded only after the hash and signature pass.
- **Json reader / kept:**
  - Typed reads check their token, except `Number()` (N10).
  - The source's parse is per context (L3), and depth is the default 64.
  - There is one kept order, `OrdinalIgnoreCase` (`kept/list/this.cs:243`), used by the writer, the digest and the snapshot.
- **Locked surface:**
  - `surface_golden.txt` moved 301 lines over 26 re-pins, each citing its decision: 203–211, 257, 262, 270a, 279, 306, 308, 309, 314 and A13/280.
  - The changes since my last pass to `View`, `data`, `call.list`/`call` (setting handed to each push, `Format` gone with 214's channel format) and `item` are renames or match their decisions.
  - Out's self-reference cap moved from `data.Output` to the Out view (`View.cs` `_depth`, AsyncLocal per flow), with the same behaviour.
