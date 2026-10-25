# fast-apps — the auditor's final audit (decision 96)

Tip `32652d05a`, cut from runtime2 `98624ab6a`. Read only; production code untouched. The log
(`.bot/fast-apps/architect/summary.md`) is claims to check, not truth.

**Verdict: FAIL — one small pre-merge item (B1), plus a docs-shaped one (B2). Nothing in the design blocks.**
Every Ingi decision I checked holds in the code, the locked core moved only with a named decision, the plan's tests
exist and pass, and the sweep and `plang --test` carry the stage-0 red names. But the sweep on the tip carries **two
new reds** that decision 95 says are skipped, and the follow-up lists the next branch needs are in the log only.

Next: **coder** (B1, two lines in a test file) and **architect** (B2, backlog.md), then docs.

## Blocking (merge)

**B1. The sweep carries two reds decision 95 says are skipped.**
`PLang.Tests/Runtime/App/Identity/SubAppFullPassProbeTests.cs:350` `T_LATENT_ASubOfANonTestingApp_NeverWritesToTheMakersDisk_EvenAfterItsSessionCloses`
and `:364` `T_LATENT_ASubOfANonTestingApp_NotYetOpened_DoesNotWriteTheMakersDisk` fail on the tip (Runtime: 2 failed of 1,361).
Decision 95 (and 94) say "Security's two T1 probes are marked skipped with backlog 10's reason, so the sweep carries no red
the branch means to leave". The file has no `Skip`, `Explicit` or `Ignore` (grep), `git log` shows its last change is
`1cac8abed` (security v2), and `c485c66b3` touched only `ImageIccProfileProbeTests.cs`. So the claim is false at the tip.
The reds are the known, accepted T1 (security v2, backlog 10: where a sub app's stores live, Ingi's tabled question); the
fault is that the gate shows them. The rule is "same red names as stage 0, nothing new".
Fix: mark both `[Skip("…")]`-style with backlog 10's reason (or move them out of the sweep), in the same test file; the
production code does not change. Re-run Runtime: 0 red.

**B2. Where the next branch finds the follow-ups.** (docs/architect, no code)
Backlog items 9–12 (`.bot/item-doors/architect/backlog.md:31,32,29,27`) carry only: the in-process parent, the request
app's store, sub apps owning their code, a sub app's teaching. The rest of the follow-up lists live only in the 115-row
log, and Ingi's four open questions are in no list at all (grep of backlog.md, todos.md and the proposals file, 0 hits
each for `module.Remove`, `autoWire`, `Adopt`, `ImageSharp`, `ContainerFamily`, `PlangName`, `item is goal`,
`GetChangedSince`, `Classes()`, `Class pinning`). Add one section "From fast-apps (follow-up)" to backlog.md with the
"Next branch" list below, and the four questions under "To discuss with Ingi". Item 10's text ("probes `T_LATENT_*`, red")
needs one word changed once B1 is fixed (skipped). Done in the docs pass, it is enough.

## 1. Ingi's decisions against the code

Two independent read-only passes (decisions 3, 4, 17, 18, 36, 38, 39 and 19, 20, 26, 30, 35, 52) and my own reading of
the rest. Evidence is file:line at the tip.

| # | Ingi said | Built | Verdict |
|---|---|---|---|
| 3 | an app is made from a parent; its System is the calling actor's | `internal app.@this(path, actor)` `app/this.cs:306`; the runner passes `context.Actor!` `test/list/this.cs:179`; lists layer over the actor's app `actor/this.cs:194,196,201` | holds |
| 4 | System takes the caller's identity, stops with the caller; the rest its own | linked token `actor/this.cs:166`; `Identity ?? DefaultIdentity` `:204`, read by the identity store `module/identity/code/Default.cs:314`; context, channels, permission, settings born by the System's own list `actor/list/this.cs:14` | holds (linkage runs through the app's shutdown token, same result) |
| 17 | the door stays internal | `app/this.cs:306`; only caller `test/list/this.cs:179`; `origin` an internal interface `origin/this.cs:9`; the only public ctor is the CLI's `app/this.cs:299` | holds |
| 18 | in-process `app.parent` not built | `parent/this.cs` untouched on the branch | holds |
| 19, 20 | a sub app's code/DLL is its own; parent and siblings never have it; a kind on a parent's type is refused (TypesShared), an action into a parent's module (ModuleShared) | layers `type/list/this.cs:28-37`, `module/this.cs:74-80`; refusals `Registry.cs:233-234`, `module/this.cs:132-137,176-179`, answered `actor/this.cs:207-218`; no path from a layer up to its parent | holds |
| 26 | a run-time binding is held on the maker actor's context, never on the shared item; `Own()` serves read-time clauses only | `event/on/list/this.cs:23-40`; binds via `Context.Actor!.on` at `module/on/event.cs:73-76`, `module/debug/this.cs:72,81-150`, `test/this.cs:180`, `test/Coverage.cs:46`; `Own()` `private protected` `type/item/this.cs:483`, one caller `goal/step/action/this.cs:255`; cleared `actor/this.cs:229` | holds; no run-time Bind onto a shared item |
| 30 | a type's history takes no prior | `type/this.cs:703` `Blank()` → `history.closed`; `history/this.cs:24` virtual Add; `closed.cs:7` | holds |
| 35 | production code only tests call goes, with its tests | `own`'s Bind overloads, reader ctor, `service.Identity`, `CreateChild`/`Clone`, stream statics, `Registry.Add(kind)`, `Assemblies` made private: all gone; every survivor I traced has a production caller (`origin/code.cs:18`, `test/list/this.cs:179`, `HasOwn`, `HasCode`, `HasKept`) | holds; one older member missed (N11) |
| 36, 38, 39 | every app is made from an origin, two kinds (code, actor); lists are layers over it; `Shared` gone | `origin/this.cs`, `origin/code.cs`, `actor/this.cs:14`; no `caller == null`/`_parent` in `app/`; no `Shared` flag (hits are .NET's) | holds; `HasCode` is still a two-owner test (N2) |
| 52 | call list's audit at the first error; Data's properties bag when asked; `HasProperties` internal | `call/list/this.cs:70,100-101`; `data/attachment.cs:11-14`; `data/this.cs:229` | holds |
| 62 | merge after the docs bot is satisfied | not an audit item; the line is codeanalyzer → tester → security → auditor → docs | order kept |
| 64 | the C# build made faster | 11 PackageReferences left of 67, `Sdk.Web` → `Sdk` (`PLang/PLang.csproj:1,75`); no removed package's name appears in `PLang/`, `os/`, `PlangConsole/` (grep); the build and sweep pass | holds |
| 66 | boy scout: a touched file is left better, reviewed whole | review2–6 read touched files whole; `Default.cs` and `image/this.cs` (touched after review6, `5e4f2d949`, `abedc1343`) were not read whole by the codeanalyzer: see N12 | holds, one gap |
| 67 | the bench is recorded at every review | `Tools/Bench` exists; rows for review4 and review5 (partial); none for review6 or the tip: see §4 | holds, one gap |

Nothing built that Ingi did not say, except these, each with a decision: `Console => true` (73), `Testing` read by `Mode` (83, 84),
`OutsideItsMaker` (50, 51a), the `InternalsVisibleTo` for `PLang.Tests.Bench` (71, "project metadata, accepted").

## 2. The locked core

`git log -p` on `PLang.Tests/Wire/App/Surface/surface_golden.txt`, 98624ab6a..HEAD: eight commits, net +4/−25 lines.
Every move names its decision.

| commit | move | decision | Ingi's? |
|---|---|---|---|
| `0c41bc585` | `IClause.Bind` gains `app.event.on.own on` | 26 | yes ("yes") |
| `5dcb5bcfd` | `history.Add` virtual | 30, 31 | yes |
| `91b5013fc` | `own`'s two actor-taking `Bind` overloads removed | 35 | yes |
| `523300500` | `type.reader` abstract; its `ctor()` gone | 36, 39, 40 | yes |
| `3e3ea8f0e` | call list ctor loses `audit` | 52 | yes |
| `143121054` | `event.Of(on)` and nine overrides removed | 63, 68 | follows 26 (dead after the hold); named by the architect/codeanalyzer, not by an Ingi sentence — covered by the boy-scout rule 66 |
| `1dd447b73` | stream `ReadAllTextAsync`, `Input`, `Output`; `Assemblies` ×2 removed | 35, 75, 76 | yes (35) |
| `65aa45866` | `type.list.Add(kind)` removed | 35, 88 | yes (35) |

No addition to a locked surface lacks a decision. The surface gate (`TheCoresSurface_IsThePinnedOne`) is green in the sweep.

## 3. The plan's behaviours (start.goal)

All named tests exist (`grep` of `PLang.Tests`; the five plang tests under `test/plan/fast-apps/app/**` with built `.pr`) and pass at the tip
(sweep below, `plang --test` 298/303). "Fails without its change": red commits for `CacheLifetimeTests`/`RequestAppsLeaveNothingBehindTests`
(`ccf82ba90`), `SharedTypeListTests` (`14b255c6d`, `75c0c1594`), `SharedModuleTests` (`cb3b9e661`), `HoldTests`/`HeldBindingTests`
(`17a51bd58`, `b916f6064`), `SharedEntryWritesTests` (`e6877b410`), `SharedGoalTests` (`b916f6064`), `OriginTests` (`5d784c502`), the stage-4/5 tests
(`5b38cc183`…`ab1ef713a`, `ac80e0240`, `b68546552`); mutations by the tester for `ChildSystemTests`. Dropped, with decisions: `PathSchemeTests` (42),
`ListSnapshotReadTests` (56), `TestAppHasItsRunnerAsParent` (18) — `start.goal:19-20,63-70` says so and names the real tests (`539d81228`).

- `KindLookupTests.ALookup_AnswersWhileTheGateIsHeld` (the tester's one major, decision 91a): read by me, not mutated. It takes the
  entry's `_gate` on one thread and looks a kind up on another with a 2 s timeout (`KindLookupTests.cs:26-59`): a locked lookup blocks, the lock-free one
  answers. The design is sound; it binds to the private field name `_gate` by reflection (N13).
- Not mutation-tested by anyone, the tester said so: `CodeFactsTests`, `ChildSystemTests`' own-memory test, the two plug-in plang tests. Guarded
  elsewhere (the bench's 0.15 ms birth; the sweep). Noted, not a finding.
- `start.md`/`start.goal` stop at stage 5; stage 6 (decision 64, the packages) is in no plan file (N14).

## 4. The speed claims against `Tools/Bench/record.md`

| log claim | record row | holds? |
|---|---|---|
| sub birth 0.14–0.16 ms / 128 KB → 110 KB (71, 86, 87) | stage5 0.15 ms/128 KB; review4 0.14/110 KB; `dec53e6c4` 0.15/110 KB; `d050b9f50` 0.27/110 KB at load 12.7 | yes (the 0.27 is load) |
| root birth 18–20 ms / 2.36 MB (71, 87) | 18.4–20.2 ms/2.36 MB; review4 2,488 KB then fixed to 2,359 KB (`dec53e6c4`); 2,360 KB at `d050b9f50` | yes, inside 2,357–2,377 |
| held memory flat (71) | root 0.9 → 2.8 → 2.7 → 2.7; sub 2.4 → 3.0 → 3.0 → 3.0 (stage5); review4 flat | yes at review4 |
| `plang --test` 5.0–5.4 s (61) | 4.7–5.6 s, review4 5.6 s; my run: **4.8 s** (298 pass, 1 fail, 3 stale, 1 skipped) | yes |
| C# sweep ~30 s target, "close" (53) | stage5 40.6–54.7 s, review4 37.3 s; my run: **~46 s** wall (08:01:44 → 08:02:30, load 8–15) | target not met; inside the recorded band; the log says "close" and never claims 30 s |
| clean build no slower after stage 6 (90) | quiet 33.8–53.8 s vs 45.9 s before at load 9.9 | holds as "not slower"; the win is bin size and a no-change build (7.3 → 4.0 s), not the clean build |
| "all five ImageSharp advisories unreached today" (95) | not a speed claim; checked in §6 | holds |

**The gap (N15):** the last row with request-app and held-memory numbers is review4 (`d1fb4dd29`). Since then the run path changed: the call-frame
depth door (`2e5e431e5`, three pushes through one `Push<TState>`), the type-list batch guard, setup, and `image` size reading. Review5 recorded
bytes only; review6 recorded nothing ("nothing in this round touches the numbers"), and the tester's line records builds only. Decision 67's
"held memory growing with the request count always blocks" has no measurement at the tip from a reviewer.
I ran the birth and request measurements on the tip myself (below).

**My run at the tip** (`BENCH_SKIP="build sweep ptest" BENCH_REST=0`, load 2.61; the row is not committed to `record.md`, N15 asks the codeanalyzer to add it):
sub birth 0.14 ms / 110 KB; root birth 18.10 ms / 2,358 KB; sub request 1.82 ms / 540 KB; root request 17.32 ms / 3,607 KB;
held, sub 1.6 → 2.1 → 2.1 → 2.1 MB; held, root 0.1 → 1.9 → 1.9 → 1.9 MB. Every number is inside its band and held memory is flat
(the last three within 0.2 MB). So the speed claims hold at the tip, and the gap in N15 is a missing row, not a regression.

## 5. Tests on the tip

PLang and PlangConsole rebuilt (`./dev.sh build`: 95 s), the two symlink env vars unset, PLNG001/002: 0.

| suite | tip | stage 0 / line.md |
|---|---|---|
| Modules | 1,344 green | 1,333 → 1,344 |
| Types | 965 green | 951 → 964 |
| Data | 877 green | 863 → 877 |
| Generator | 191 green | 191 |
| Runtime | 1,361: **2 red (B1)** | 1,242 → 1,337 |
| Wire | 601: 6 red | the same six names (`SerializedString_…`, `PlangPath_AsSnapshotConvert_…`, `TypedSnapshotString_…`, `NavigateAndEditCapturedVariable_…`, `MidStackChain_…`, `ThrowTimeSnapshot_…`) |

`plang --test` (from `test/`, PlangConsole rebuilt): 303 total, 298 pass, 1 fail (`/app/type/item/BareLiteralJudgedByForm`), 3 stale
(`/plan/snapshot/module/callback/*`), 1 skipped (`DurationPartsAndCompare`): stage 0's names. `git status` clean after.
Beyond B1 there is no new red name.

## 6. The reviews, and what came after them

- **Codeanalyzer** (review6, PASS OBP at `6420e4894`): agree on its scope. After it, two production files changed
  (`5e4f2d949` identity export, `abedc1343` image size); I read both diffs: `Default.cs:203-210` checks the identity against `LoadAll` before consent
  and refuses `IdentityNotOwn` (403); `image/this.cs:308-325` tells the format by magic (`DetectFormat`) before any decoder runs and refuses
  TIFF and anything unlisted as `ImageSizeUnread` (415). Both correct and small. The `Sized` table is a read-only constant (decision 70's kind).
  A TIFF image's size is no longer readable: a behaviour change Ingi's 94 accepts. Wording nit: "it is the app's that made this one".
- **Tester** (line.md): agree on its findings (the false green, the unnamed tests, all fixed in 91a). Its numbers stop at the tip before 94.
- **Security** (v2 PASS; ICC probes green): agree. T1 stays latent behind Ingi's stores question; **but see B1**: the reds are in the gate.
- **Decision 95 vs the tree:** the one place the log's claim and the tree disagree (B1).
- ImageSharp 2.1.13: five NU1902/NU1903 warnings print on every build (`PLangConsole.csproj`). Unreached today (security: ICC probes,
  Identify gated by magic, plang decodes Bgra32 and saves PNG only). Ingi's question stands: 4.1.2+ under the split licence.

## 7. Next branch (each with file:line; none blocks the merge)

| id | item | where |
|---|---|---|
| N1 | Ingi: `module.Remove` answers `data` (Ok true/false, or Fail ModuleShared); today a public `ValueTask<bool>` and a refusal is a throw | `module/this.cs:172` (caller: the `module.remove` action) |
| N2 | Ingi: the public ctor's default-true `autoWireConsoleChannels` becomes a choice naming both behaviours (breaks "a flag is false by default"); 38 test uses plus Executor | `app/this.cs:294,301`; `Executor.cs:44` |
| N3 | Ingi: channels born with their list (every channel type's ctor), no late stamp | `channel/list/this.cs:164-166` (`Adopt`: `channel.Channels = this`) |
| N4 | Ingi: ImageSharp 4.1.2+ (split licence), or keep the Identify gate and live with five NU190x warnings | `PLang/PLang.csproj` (`SixLabors.ImageSharp` 2.1.13); `type/item/image/this.cs:308-325` |
| N5 | The stores question: where a sub app's stores live (317a); then the T1 probes come back to green and the skips go | `SubAppFullPassProbeTests.cs:350,364`; backlog item 10 |
| N6 | "Has code of its own" is two owners and a folder test: one owner answers | `app/this.cs:60` `HasCode`; `module/this.cs:90`, `type/list/Registry.cs:94` `HasOwn`; `actor/this.cs:202` |
| N7 | The origin's two refusal voices: `Hold` answers an error, `Register`/`Remove` throw | `actor/this.cs:207-218` |
| N8 | The registry's type switches (`ContainerFamily`, `PlangName`) belong on the element; `IsCondition`/`IsBranch`/`Link` by literal name; `set.cs`'s throwaway Make; frames as types; channel catches `when (is not …)`; data's "did it answer itself" (decision 78) | `type/list/this.cs:185-188,221`, `type/list/Registry.cs:311`; `goal/step/action/this.cs:117`, `goal/step/pick/list/this.cs:72` |
| N9 | The locked memory's verb+noun members (`GetNames`, `GetAll`, `GetChangedSince`, `ShallowCloneStore`) (78) | `type/item/variable/list/this.cs` |
| N10 | Late stamps and per-scope work in actor/context and actor/setting; the identity slot at six doors; two kinds of store instead of `bool memory` (82) | `actor/context/this.cs:175,184` (`error.Context ??= this`); `actor/setting/this.cs:173,315,380` (`Classes()`); `variable/list/system.cs:22,25`, `actor/this.cs:149,204` (identity ?? default); `IStore.Open(file, Func<bool> memory, context)` |
| N11 | Test-only members the 35 sweep missed (older than the branch): `ChannelNames` has no production caller | `channel/list/this.cs:200` |
| N12 | Files touched after review6 not read whole (rule 66): `identity/code/Default.cs` (the "identity slot" smell is in it), `image/this.cs` | `module/identity/code/Default.cs`, `type/item/image/this.cs` |
| N13 | `KindLookupTests` reads `_gate` by reflection: it breaks silently on a rename | `PLang.Tests/Types/App/Types/KindLookupTests.cs:33-34` |
| N14 | The plan files stop at stage 5; stage 6 and the tip's decisions are only in the log. Fold into the plan's record when it is moved to the concept tree | `test/plan/fast-apps/start.md`, `start.goal` |
| N15 | Re-run the bench's request and held rows at the tip as the new baseline (the last full one is review4) and add the rows; codeanalyzer's review6 row is missing | `Tools/Bench/record.md` (rows after `d1fb4dd29`) |
| N16 | Stage 6's `Sdk.Web` → `Sdk`: a future webserver module needs `Microsoft.AspNetCore.App` re-added; say so where the server is planned | `PLang/PLang.csproj:1`; backlog item 9 |
| N17 | `Class` pinning a DLL's types for good; the debug module's nine `item is goal` filters (a typed `Bind<T>`); the goal's stat stamped after its read (S1) | `module/debug/this.cs:132-140`; `goal/this.cs:353` (`Stamp`); `type/list/Code.cs` |
| N18 | 19 parts 2–3 (a kind on a parent's type through the asker; unloading a sub app's DLL) and 12–14 (`module.Action` as `list<action>`, `Alias`/`Internal` without `init`, path's schemes as kind classes) | backlog items 11, 12; log 42 |
| N19 | CLAUDE.md proposals: the file holds two (boy scout + no runtime code for tests; the speed record). A third belongs there: "a run never writes an object its app shares; a binding made while a program runs is held on its actor's context" (decisions 26, 29), which exists only in start.md | `.bot/fast-apps/claude-md-proposals.md` |
| N20 | Docs not touched on the branch (`git diff 98624ab6a..HEAD -- Documentation` is empty): the origin and layers, the hold, history closed, a birth that makes what's asked, `Tools/Bench`, the packages/SDK change, the rebase trap (log 85) go to good_to_know.md | `Documentation/v0.2/good_to_know.md` |

## Clean checks

- The surface golden moved eight times, each with a decision; no unapproved addition.
- No `caller == null`, no `_parent` split, no `Shared`, no new static of run-time state in `app/`.
- No run-time Bind onto a shared goal/step/action/type item; `Own()` private to the read-time path.
- CLI path unchanged in shape: `Executor.cs:44` → `new origin.code(false)`, `Load()` reads `.build/app.pr`.
- No removed package's namespace remains in source; `Microsoft.Data.Sqlite` 9.0.21 and `Newtonsoft.Json` 13.0.4 list no advisory.
- The tree is clean after the sweep, `plang --test` and the bench (I removed what the bench wrote; `record.md` restored).

## Update — B1 closed (0b770e18d, 2b685ce17)

The coder skipped the two T1 probes with backlog 10's reason (`SubAppFullPassProbeTests.cs`) and added a comment at `image/this.cs` `Sized`
(the ICC note, decision 96a); no behaviour changed. I re-ran `SubAppFullPassProbeTests`: 20 total, 0 failed. **Verdict now: PASS**, next: docs.
B2 stays as the docs pass's first item (backlog.md section and the N19/N20 docs); it blocks no code.
