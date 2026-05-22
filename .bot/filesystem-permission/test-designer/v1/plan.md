# Test designer plan — filesystem-permission v1

Translation of the architect's 5-stage design into a test contract. Tests are
the spec — the coder makes them pass.

## Sources of truth

- `.bot/filesystem-permission/architect/summary.md` — cross-cutting decisions.
- `.bot/filesystem-permission/architect/stage-1-permission-types.md`
- `.bot/filesystem-permission/architect/stage-2a-snapshot-resume.md`
- `.bot/filesystem-permission/architect/stage-2b-path-authorize.md`
- `.bot/filesystem-permission/architect/stage-3-storage-binding.md`
- `.bot/filesystem-permission/architect/stage-4-filesystem-surface.md`
- `.bot/filesystem-permission/architect/stage-5-messages-end-to-end.md`
- `.bot/filesystem-permission/architect/plan-test-designer.md`

## Test layout

| Stage | Where | Runner |
|-------|-------|--------|
| 1 | `PLang.Tests/App/FileSystem/PermissionTests/` | `dotnet run --project PLang.Tests` |
| 2a | `PLang.Tests/App/CallbackTests/` (rewritten) + `Tests/Callback/` (extended) | C# + `plang --test` |
| 2b | `PLang.Tests/App/FileSystem/PermissionTests/AuthorizeTests/` | C# (mocks `actor.Permission`, `Channel.Ask`) |
| 3 | `PLang.Tests/App/FileSystem/PermissionTests/StorageTests/` | C# (real sqlite + in-memory) |
| 4 | `PLang.Tests/App/FileSystem/SurfaceTests/` + existing `Tests/App/modules/file/` | C# parametrized + `plang --test` |
| 5 | `Tests/Permission/` + fixtures under `Tests/Permission/_fixtures/apps/` | `plang --test` |

Folder convention: `*Tests` suffix on test mirrors of `PLang/App/X` (avoids the
`Data` / `Variables` alias clash from `PLang.Tests/GlobalUsings.cs`).

## Batches

Tests are presented in ~10-test batches. Each batch needs explicit approval
before I move to the next. I will not write any test files until every batch
is approved.

### Stage 1 — pure types (~20 tests)

**Batch 1 — Verb sub-records and their Covers** (≈10 C# tests)
- `Read`/`Write`/`Delete` records — default-true sub-options surface correctly.
- `Read.Covers(Read)` matrix: full-covers-full, narrowed-fails-full,
  full-covers-narrowed, narrowed-covers-narrower.
- Same per `Write.Covers(Write)` and `Delete.Covers(Delete)`.
- `Verb.@this.Covers(Verb.@this)` — null sub-verb means "no grant for that
  verb" (so `Verb { Read = null }.Covers(Verb { Read = new Read() })` is false).
- `new Verb.@this()` covers `new Verb.@this()`.

**Batch 2 — Permission.Covers + Match dispatch + JSON** (≈10 C# tests)
- Exact match — equal paths cover, different paths don't.
- Glob match — pattern path covers concrete request; non-matching pattern doesn't.
- Regex match — pattern covers concrete request; non-matching regex doesn't.
- Unknown `Match` enum value → `Covers` returns false (fail-closed, no throw).
- `Permission.Covers` combines path match AND verb cover: path-yes + verb-no = false.
- `Permission.Covers` same-record both sides reads naturally
  (`grant.Covers(request)` AND `request.Covers(grant)` makes sense per shape).
- JSON round-trip: serialize → deserialize → equality holds.

### Stage 2a — snapshot/resume (~30 tests)

**Batch 3 — Markers, Type.Exit, Synthetic, Data.Snapshot** (≈10 C# tests)
- `Type.Exit()` true for `Ask`, false for `string`, `byte[]`, plain class with
  no marker, generic `Data<T>` where T does NOT implement `IExitsGoal`.
- `Ask` is `IExitsGoal`.
- `Action.@this.Synthetic` defaults to `true` on inline-C# construction.
- Source-generator-emitted PR action has `Synthetic = false` (test against a
  representative generated action).
- `Data.@this.Snapshot` property exists and round-trips through `Data.Ok`/
  copy constructors / set-after-construct.
- `action.Snapshot()` helper returns non-null `Snapshot.@this` and matches
  `Context.App.Snapshot()` for the same call frame.
- Contract: an action returning Exit-typed Data MUST have a non-null Snapshot
  (asserted via a generic invariant test that scans built-in Exit-typed
  actions).

**Batch 4 — Step loop, Goal.RunFrom, action.RunAsync** (≈10 C# tests)
- `Data.ShouldExit` true for: unhandled failure (Success=false, Handled=false),
  Returned=true, Type.Exit()=true.
- `Data.ShouldExit` false when: Success=true, Returned=false, non-Exit Type.
- `Step.RunFrom(ctx, 0)` runs all actions in step from index 0.
- `Step.RunFrom(ctx, N)` runs actions from index N to end.
- `Goal.RunFrom(ctx, stepIdx, actionIdx)` runs from action in step, then
  remaining steps.
- `Goal.RunFrom` short-circuits if the resumed action returns Exit-typed Data.
- `action.RunAsync(ctx)` is the single entry; `App.Run` / `App.RunAction`
  symbols are absent from production code (codebase-scan test).
- `CallStack.Push` stamps `Synthetic` from the pushed action.
- Snapshot wire-serializer drops Synthetic frames (in-memory keeps them).

**Batch 5 — output.ask + Channel.Ask + Resume integration** (≈10 tests; mix of C# and PLang)
- C#: `output.ask` returns `Data.Ok(answer)` when `!ask.answer` is set (sentinel
  consumed and removed from Variables).
- C#: `output.ask` delegates to `Channel.Ask` when no answer is staged.
- C#: Stream channel `Ask` writes prompt + reads stdin line + returns `Data.Ok(line)`.
- C#: Stream channel `Ask` returns the same Data shape on cancellation/timeout
  (existing behavior preserved through the rename from `AskCore` → `Ask`).
- C#: Message channel `Ask` returns `Data<Ask>` with `Snapshot` attached and
  question as `Value`.
- C#: `callback.run` requires `Data.Snapshot != null` (error otherwise).
- C#: `callback.run` delegates to `Data.Snapshot.Resume(Context)`.
- C#: `Snapshot.Resume` with empty chain returns error.
- PLang (`Tests/Callback/`): single-goal mid-goal `ask` against a stateful
  driver completes and binds the variable. (Adapt existing `AskWithVars`.)
- PLang (`Tests/Callback/`): cross-goal `Start` → `AskAQuestion` against a
  stateless driver suspends, resumes with `!ask.answer="Alice"`, output is
  exactly `"Hello\nAsking\nAlice"`. (Canonical nested scenario from the design.)

### Stage 2b — Path.Authorize (~10 tests)

**Batch 6 — Authorize behavior + e2e** (≈10 C# tests + 2 PLang)
- Grant exists → `Path.Authorize` returns `Data.Ok` immediately; channel's `Ask`
  never observed.
- Stateful `"a"` answer → signs with `AlwaysExpiry`, `actor.Permission.Add`
  called with persistent-signed Data, returns Ok.
- Stateful `"y"` answer → signs without expiry, `Add` called with session Data,
  returns Ok.
- Stateful `"n"` answer → returns `Data.Fail(PermissionDenied)` carrying the
  constructed `Permission`.
- Stateful `"garbage"` answer → recurses, prompt prefix is
  `"Invalid answer 'garbage'. "`.
- Stateless `Ask` channel → bubbles `Data<Ask>` unchanged (Type.Exit()=true).
- Constructed `Permission` shape on miss path:
  `AppId=Context.App.Id`, `Actor=Context.Actor.Name`, `Path=this.Absolute`,
  `Verb=requested`, `Match=Match.Exact`.
- `PermissionDenied` error round-trips through `Error.cs` shape.
- PLang (`Tests/Permission/Authorize/`): stateful 2-step goal whose first step
  reads an ungranted path; driver answers `"a"`; both steps complete.
- PLang (`Tests/Permission/Authorize/`): stateless variant of the same — first
  run suspends, resume with `"a"` completes both steps.

### Stage 3 — storage (~10 tests)

**Batch 7 — Actor.Permission Find / Add / Revoke** (≈10 C# tests)
- Round-trip: `Add` a signed "a" grant → `Find` returns it; signature validates.
- Per-actor isolation: user grant + system grant → `user.Permission.Find`
  doesn't surface the system one.
- Two-home unification: in-memory "y" grant + persisted "a" grant → `Find`
  returns the right home depending on query.
- Verb narrowing in storage: full-allow grant covers narrowed Read request;
  Read-only grant does NOT cover Delete request.
- Glob match in storage: glob grant matches exact-path request; non-matching
  glob doesn't.
- Revoke in-memory: in-memory list grant removable.
- Revoke persisted: sqlite row removable.
- Signature failure: corrupted signature on a persisted grant → `Find` skips it.
- Idempotent Add: granting same path twice overwrites (no duplicate rows).
- Signature-verification caching: `Find` walking the same Data multiple times
  validates signature only once (flagged on the Data instance).

### Stage 4 — FS surface (~15 tests)

**Batch 8 — Parametrized per-method permission flow** (≈8 C# tests)

One parametrized fixture across the 11 FS methods (Read variants, Write variants,
Mkdir, Delete, Stat, List, Exists, Move, Copy). Each method gets:
- In-root path → `Data.Ok` with expected value, no `Ask` issued.
- Out-of-root path against Stream → blocking prompt → grant stored → success.
- Out-of-root path against Message → returns `Data<Ask>`, Snapshot attached.

Encoded as 3 parametrized C# tests over the 11-method list (not 33 distinct
tests). Plus:

- Handler shells: every `modules/file/*` action returns the FS method's Data
  unchanged (no per-handler short-circuit logic).
- `ValidatePath(string)` symbol absent from production code (codebase-scan).
- `FileAccessControl` and `fileAccesses` list absent from production code.
- `System.IO.Abstractions.IFileSystem` no longer in `IPLangFileSystem` base list.

**Batch 9 — Move/Copy bundled consent + legacy smoke** (≈5 tests)
- `Move` with one missing grant produces a single-path `Ask`.
- `Move` with both source and dest missing produces a bundled `Ask` whose
  question covers both paths.
- `Copy` mirrors `Move`'s bundled behavior.
- Bundled `Ask` on `"a"` answer stores BOTH grants (source Read, dest Write).
- Legacy goal-test smoke (`Tests/App/modules/file/`): existing FS goal tests
  green against v2 with in-root paths.

### Stage 5 — Messages end-to-end (~6 PLang tests)

**Batch 10 — Six-step integration** (6 PLang test goals)
- `TestNoGrantSuspends` — first read of `/apps/Email/system.sqlite` returns
  Exit-typed Data, channel renders prompt, goal suspends.
- `TestGrantAStoresPersisted` — driver `"a"` → resume → grant lands in
  `permission` table with `AlwaysExpiry`.
- `TestImmediateRereadSkipsPrompt` — same goal rerun in same process, no prompt.
- `TestRestartStillNoPrompt` — process restart, same goal, no prompt
  (sqlite-persisted).
- `TestRevokeReprompts` — revoke the grant, rerun → prompt fires again.
- `TestNarrowedGrantRejectsWiderRequest` — Read grant with `Metadata: false`,
  a read that asks for metadata surfaces a fresh `Data<Ask>`.

## Open questions / risks

1. Stage 2b mocks `actor.Permission.Find/Add`. Some "stateful e2e" tests in
   Batch 6 may actually need stage 3 storage real, not mocked. I'll resolve
   this when sizing each test in Batch 6.
2. Batch 8 parametrization mechanics — TUnit's `[Arguments]` per method is
   straightforward; `[Test]` over an enumerable of `(method, root-status, channel-kind)`
   tuples is the shape. Confirm with coder before pick.
3. The "no `App.Run` / `App.RunAction` symbols" test is a static-survey test
   (Roslyn over PLang/, excluding tests). If this style isn't already in the
   suite I'll propose how to write it before adding it.

## Work order

1. Get high-level batch plan approved (this file).
2. Walk through batches 1 → 10. Per batch: present signatures + one-line intent;
   refine until accept; move on. No file writes mid-walkthrough.
3. After all 10 approved: write `.cs` and `.goal` stubs in one pass.
4. Commit, push, hand off to coder.
