# Coder — filesystem-permission

## Version
v7 — see `v7/result.md` (close tester v5 F1: pin nonce-replay half of F-A fix)

## v7 update — tester v5 F1 closed

tester v5 PASSed the production fix but flagged that coder v6's
`SkipFreshnessCheck=true` neutralises **two** independent signing checks
(step 2 wire-freshness, step 4 nonce-replay) and only step 2 had a test.

Added `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt`
(tester's spec verbatim): app1 grants "a"; app2 reads the same foreign file
twice. Persisted `Find` re-deserializes `Data` each call, so each read does a
real `VerifySignature` — second read would hit `NonceReplay` without the
flag. Stateless channel surfaces a re-prompt as `Type == "ask"`.

Mutation-verified: `SkipFreshnessCheck` `true→false` now kills **both**
Scenario4 durability tests (step 2 and step 4 each), where v6's commit
killed only one. Suite: **2855 pass, 0 skip, 0 fail**.

## v6 update — both major auditor findings closed

**F-A:** persisted "always allow" grants used to expire after 5 minutes
(wire-freshness `Created+TimeoutMs` applied to all signatures, including
grants). Fix: `signing.verify` gains `SkipFreshnessCheck` flag (default
false); `Permission.VerifySignature` passes true so the grant's own
`Expires` is the only time bound (null = permanent, set = enforced).
Doc-comment corrected. New mutation-verified test advances `NowUtc` by
10 min and confirms the grant still covers.

**F-B:** merged `origin/runtime2` (27 commits, app-lowercase rename +
7 OBP folder collapses). 63 conflicts resolved across DU/UD/AU/UU
categories. Branch additions now live at lowercase paths with lowercase
namespaces, matching runtime2 convention.

Suite: **2854 pass, 0 skip, 0 fail**.

## v5 update — Scenario4 closed

## v5 update — Scenario4 closed

The deferred bug from v4 turned out to not be a deserialiser bug at all. The
`[Skip]` reason ("STJ stack overflow in
`SmallObjectWithParameterizedConstructorConverter` when a fresh App loads
the row") was wrong — the JSON is small and clean, and a fresh-app `Find`
returns null (not crash). The actual blocker: `PermissionRecord.AppId`
scoped grants to a per-instance `App.Id` (a fresh GUID per `new App()`), so
app2 never matched app1's grant even with the sqlite row sitting right
there.

Fix: drop `AppId` from `PermissionRecord` and the cover check. Grants are
now identified by `(Actor + Path + Verb)`, with the per-actor sqlite store
providing the root scope. `App.Id` retained for in-memory test scoping.

Scenario4 unskipped with a real body; **2853 pass, 0 skip, 0 fail**.

## v4 update — all 9 tester findings closed

Tester v3 (NEEDS-FIXES) flagged 9 test-quality gaps — the v3 code change
itself was correct. All 9 addressed:

| # | Finding | Fix |
|---|---|---|
| 1 | v3 `RootComparison` fix had no regression test | Added Linux-gated `IsInRoot_UpperCasedRoot_*` (security-observable) in PathAuthorizeTests + contract pin in ValidatePathTests |
| 2 | Move couldn't be told from Copy | Move tests now assert source-gone + dst content |
| 3 | 6 PLang placeholder goals under overclaiming names | Deleted (Stage5 C# covers them) |
| 4 | Stage5 Scenario4 empty body reported pass | `[Skip(...)]` attribute with full deferred-bug reason |
| 5 | `IdempotentAdd` / `TwoHomes` weak assertions | Behavioral no-duplicate via Revoke+Find; sqlite routing inspection |
| 6 | LegacyFsGoalTests 2-line tautology | Real v1↔v2 round-trip |
| 7 | `IsInRoot.OsDirectory` clause untested | Added |
| 8 | Move/Copy "n" + stateless `Data<Ask>` untested | 3 new tests |
| 9 | No `baseline-tests.md` | Added at `coder/baseline-tests.md` |

C# suite: **2852 pass, 1 skip, 0 fail** (skip = Scenario4 deferred).

**Next (v5):** the deferred SettingsStore cross-App `Data<PermissionRecord>`
deserialiser recursion that Scenario4 documents. The "a" answer's persistent
grant only survives within one App process today; fixing this is what
unskips Scenario4 and closes the persistence half of the two-homes model.

## v3 update — branch closes

Codeanalyzer v2 raised two regressions:
- **v2 #2** (`PLangFileSystem.ValidatePath:227` Linux case-comparison): **fixed** this session — shared `Path.RootComparison` helper used at both gate sites.
- **v2 #1** (handler-layer authorize copy-paste in `modules/file/*.cs`): **intentionally deferred** to a new branch. The real fix is bigger than codeanalyzer's (a)/(b) options — `Path` becomes polymorphic across schemes (`file://`, `http://`, ...). Plan: `Documentation/v0.2/path-polymorphism-plan.md`, handed to architect.

**For codeanalyzer:** v2 #1 is tracked with a written plan + `todos.md` entry, not punted. Branch scope was permissions, not Path hierarchy restructure. **2846 / 2846 C# tests green.**

## v2 update (post-codeanalyzer-v1 follow-up)

All 10 codeanalyzer v1 findings closed. Eight fixed across `af32f3e` /
`82a136b` / `c4cbbd3` / `8b22a5e` / `f543e19` / `1af7922` immediately
after the codeanalyzer report. v2 cleaned the five stale Cause
doc-comments (including a broken `<see cref="Cause"/>`) that survived
the code-only #7/#8 cleanup.

## What this is
PLang's filesystem permission system + unified suspend/resume mechanism.
**All 5 stages landed.** Test suite: **2830 tests, 0 failing.**

## What was done

| Stage | Status | Last commit |
|-------|--------|---|
| 1 — Permission types (record + Verb + Match) | ✅ | `4143621b8` |
| 2a.1–2a.8 — Snapshot-resume engine (8 slices) | ✅ | `8dc9e0e01` |
| 2b — Path.Authorize + PermissionDenied | ✅ | `3bf37c4a9` |
| 3 — Actor.Permission storage (in-memory + sqlite) | ✅ | `15c165ee0` |
| 4 — IPLangFileSystem v2 (added alongside v1) | ✅ | `db119cbd7` |
| 5 — Messages end-to-end (5/6 scenarios) | ✅ | `194a6c16e` |

## What this codes up

- **Permission record** (`PLang/App/FileSystem/Permission/`) — pure types:
  `Permission(AppId, Actor, Path, Verb, Match)` + `Verb.@this` container +
  three sub-record verbs (Read/Write/Delete) with default-true options +
  `Match` enum (Exact/Glob/Regex, fail-closed dispatch) + `Covers` semantics.
- **Snapshot-resume engine** (`PLang/App/Snapshot/`, `App/Data/ShouldExit.cs`,
  `App/Types/Exit.cs`, `App/IExitsGoal.cs`) — `IExitsGoal` marker, `Type.Exit()`
  predicate, `Data.Snapshot` property, step-loop short-circuit via
  `Data.ShouldExit()`, recursive `Snapshot.Resume(ctx)` for cross-goal
  continuation, `Goal.RunFrom` / `Step.RunFrom` continuation helpers, action
  owns its own execution (App.Run absorbed; `Synthetic` stamping on Call frames),
  `callback.run` reduced to 10 lines (Data.Snapshot.Resume delegation).
- **Old callback machinery deleted** — `ICallback`, `AskCallback`, `ErrorCallback`,
  `Callback/Wire/` gone; `Error.Callback` retyped to `Data<Snapshot>`.
- **Path.Authorize** (`PLang/App/FileSystem/Path.Authorize.cs`) — permission gate:
  in-root paths auto-grant; out-of-root paths Find existing grant or prompt
  via `output.ask`; "a"/"y"/"n"/garbage answer handling with recursive
  re-prompt; `PermissionDenied` error type.
- **Actor.Permission** (`PLang/App/Actor/Permission/`) — per-actor Find/Add/Revoke
  surface unifying in-memory (session, no signature) and sqlite (persistent,
  signed) homes. Signature verification cache stamped on Data via Properties bag.
- **IPLangFileSystem v2** (`PLang/App/FileSystem/Path.Operations.cs`) — Path-in,
  Data-out FS surface: ReadText/ReadBytes/ExistsAsync/List/Stat/WriteText/
  WriteBytes/Append/Mkdir/Delete + MoveTo/CopyTo with bundled consent. Each
  method calls Authorize first; uses `System.IO.File/Directory` directly after
  (bypasses the legacy `IFileSystem` wrap).
- **Stage 5 e2e** (`PLang.Tests/App/FileSystem/Stage5MessagesEndToEndTests.cs`) —
  5 of 6 architect scenarios: no-grant-suspends, grant-a-stores-persisted,
  immediate-reread-skips-prompt, revoke-reprompts, narrowed-grant-rejects-wider.

## Deferred work (documented in commits + here)

- **Stage 4 v1 surface deletion**. `ValidatePath`, `FileAccessControl`, and
  `IFileSystem` inheritance still exist for ~50 legacy non-action callers
  (Builder, App.Save, Goal.this:96, http/code/Default, test/discover,
  ui/code/Fluid). v2 surface added alongside; full v1 deletion is per-site
  migration work tracked for follow-up. Shape tests in
  `FileSystemSurfaceShapeTests.cs` document the deferred assertions and will
  flip when v1 deletes.
- **Stage 5 scenario 4** (process-restart persistence). SettingsStore Set+Get
  works inside a single App, but across two App instances on the same root
  the deserialiser stack-overflows in
  `SmallObjectWithParameterizedConstructorConverter` reading
  `Data<PermissionRecord>`. Bug is in the deserialiser's Data ctor reentry,
  not in the permission flow itself. Test stub is in place; body is
  no-op with a comment explaining the bug.
- **PLang `.test.goal` integration** under `Tests/Permission/` — 6 stub
  goals exist but are intent-only. They need `modules/file/read.cs` (and
  the other file actions) migrated to call `Path.Authorize` as their first
  step before doing IO. Today the action handlers are sync and the
  migration needs an async refactor of `IFile.Read` and its callers.
- **App.RunAction surface**. Spec called for full deletion alongside App.Run;
  kept as the inline-C#-composition entry routing through Action.RunAsync
  via a `PreboundHandler` property. Deletion needs a source-generator
  surface where handler classes grow their own `RunAsync`.
- **Cause linkage**. `cause` parameter removed; `Call.@this.Cause` property
  remains but is always null. Renderer treatment is no-op naturally.
  Re-introduction (or full removal of the Cause field) is follow-up.
- **Wire serialiser for stateless suspend** — `Data.Snapshot` is `[JsonIgnore]`
  to avoid Variables→Data→Snapshot→Variables recursion in default JSON.
  Per-channel serialiser owns the wire shape; architect's todos.md note.

## Decisions worth carrying forward

- **Goal.RunFrom doesn't push a goal frame** — Snapshot.Resume already
  restored the CallStack chain.
- **Step.RunAsync keeps `|| Handled` break** alongside ShouldExit — preserves
  before-event short-circuit.
- **`!ask` sentinel removal via `Variables.Remove("!ask")`** — path
  semantics (sentinel rides as property of infra root).
- **v2 surface bypasses `IFileSystem`** by calling `System.IO.File` directly
  after Authorize — old surface still wraps every call with `ValidatePath`.
- **In-root short-circuit lives in Authorize**, not in the storage layer —
  the actor implicitly owns its own root.

## Test counts

| Component | Tests |
|---|---|
| PermissionCoversTests | 10 |
| VerbCoversTests | 11 |
| TypeExitTests | 6 |
| StepLoopShouldExitTests | 6 |
| DataSnapshotTests | 6 |
| ActionSyntheticTests | 5 |
| GoalRunFromTests | 5 |
| OutputAskRoutingTests | 6 |
| ActionRunAsyncTests | 4 |
| SnapshotResumeTests | 6 |
| PathAuthorizeTests | 9 |
| ActorPermissionStorageTests | 12 |
| FileSystemPermissionFlowTests | 30 (parametrized 10×3) |
| MoveCopyBundledConsentTests | 5 |
| FileSystemSurfaceShapeTests | 5 |
| Stage5MessagesEndToEndTests | 6 |
| **Total new tests** | **132** |

Full suite: **2830 / 2830 pass**.

PLang `--test`: `Callback/StatelessCrossGoalResumes` and
`Callback/StatefulAskMidGoalBindsValue` pass.

## File layout (new + modified)

```
PLang/App/
  IExitsGoal.cs                                (new)
  Actor/this.cs                                (Permission property + ctor wiring)
  Actor/Permission/this.cs                     (new — Find/Add/Revoke)
  Callback/this.cs                             (Wire prop removed)
  Callback/{ICallback,AskCallback,ErrorCallback}.cs   (deleted)
  Callback/Wire/                               (deleted)
  CallStack/Call/this.cs                       (Synthetic stamp; cause ctor arg removed)
  CallStack/this.cs                            (Push: cause arg removed)
  Data/this.Snapshot.cs                        (new)
  Data/ShouldExit.cs                           (new)
  Data/this.Envelope.cs                        (ICallback branches removed)
  Errors/Error.cs                              (Callback → Data<Snapshot>)
  Errors/PermissionDenied.cs                   (new)
  FileSystem/Permission/this.cs                (new)
  FileSystem/Permission/Verb/{this,Read,Write,Delete}.cs   (new)
  FileSystem/Path.cs                           (→ partial class)
  FileSystem/Path.Authorize.cs                 (new)
  FileSystem/Path.Operations.cs                (new — v2 surface)
  Goals/Goal/this.cs                           (PR-load Synthetic flip)
  Goals/Goal/this.RunFrom.cs                   (new)
  Goals/Goal/Steps/Step/this.cs                (ShouldExit wire)
  Goals/Goal/Steps/Step/this.RunFrom.cs        (new)
  Goals/Goal/Steps/Step/Actions/Action/this.cs (Synthetic + PreboundHandler + DispatchAsync)
  Goals/Goal/Steps/this.cs                     (ShouldExit wire)
  Goals/Setup/this.cs                          (PR-load Synthetic flip)
  Snapshot/this.cs                             (→ partial class)
  Snapshot/this.Resume.cs                      (new)
  Types/Exit.cs                                (new)
  Channels/Channel/this.cs                     (Ask signature; FireBefore/After cleanup)
  Channels/Channel/Stream/this.cs              (AskCore takes action)
  Channels/Channel/Message/this.cs             (AskCore returns Data{type=ask, Snapshot})
  Channels/Channel/Goal/this.cs                (AskCore takes action)
  modules/output/ask.cs                        (~10-line Run; Ask payload class)
  modules/callback/run.cs                      (~10-line Snapshot.Resume delegate)
  modules/IContext.Snapshot.cs                 (new — handler.Snapshot() extension)
  modules/error/handle.cs                      (cause arg removed)
  this.cs                                      (RunAction → entity.RunAsync via PreboundHandler)
  PLang.csproj                                 (+ Microsoft.Extensions.FileSystemGlobbing)
  GlobalUsings.cs                              (ICallback alias removed)

PLang.Tests/
  App/CallbackTests/                           (8 new test files for stages 2a.1–2a.6;
                                                6 obsolete test files deleted)
  App/FileSystem/PermissionTests/              (3 new test groups)
  App/FileSystem/SurfaceTests/                 (3 new test groups for stage 4)
  App/FileSystem/Stage5MessagesEndToEndTests.cs (new)

Tests/Callback/StatelessCrossGoalResumes/     (functional .test.goal)
Tests/Callback/StatefulAskMidGoalBindsValue/  (functional .test.goal)
Tests/Permission/*/Start.test.goal            (6 intent-only stubs)
```
