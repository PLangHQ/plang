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
