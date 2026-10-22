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
