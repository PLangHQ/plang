# fast-apps — coder handoff for the review line

The build is closed (decision 57). Branch `fast-apps`, cut at 013396728 from runtime2 98624ab6a. The decision log is
`.bot/fast-apps/architect/summary.md`; the plan is `test/plan/fast-apps/start.md` and `start.goal`.

**State at handoff (b8be3c7bb):**
- C# sweep green except Wire's six known snapshot reds (SerializedString_…, PlangPath_AsSnapshotConvert_…,
  TypedSnapshotString_…, MidStackChain_…, NavigateAndEditCapturedVariable_…, ThrowTimeSnapshot_…).
  `ErrorInPlay_DuringRecovery_IsTheTimeoutsOwnVerdict` is a known timing flake.
- `plang --test`: 303 total, 298 pass, 1 fail (BareLiteralJudgedByForm, known), 3 stale, 1 skipped.
- Surface golden: re-pinned only with decisions 40 (the reader's two kinds) and 52 (the call list's ctor). Its compare
  counts lines per class since 43.

## What it is

A sub app (a request app, a test's app) is made through `internal app.@this(path, actor)` from the actor that calls,
and costs what it uses: born in about 0.1 MB instead of about 5 MB, reading its maker's lists live.

## Built, by decision

| Decision | What | Commit | Files |
|---|---|---|---|
| stage 1 | an app's step cache is disposed with it; `ICache : IDisposable` with no default | 1bd1f63ae, 9c0a7d129 | `app/this.cs`, `module/cache/{ICache,Memory}.cs` |
| stage 2, 17 | the code's facts read once per code; a sub app bounded by its caller, its System as its caller; the runner and the suites make apps from the process parent | 090847b61, 854dd1fb2, 9a1d8b871, 388c667b5, 3312602c3 | `type/list/Code.cs`, `app/this.cs`, `test/list/this.cs`, `PLang.Tests/Shared/Parent.cs` |
| 18 | the in-process `app.parent` leaves the branch | 6a7e5a517 | plan, tests |
| 19, 20, 22 | a sub app's type list and module registry are layers over its maker's; a kind on a maker's type and an action into a maker's module are refused (TypesShared/ModuleShared) | b714ca12a, 558acbb70 | `type/list/{this,Registry}.cs`, `module/this.cs` |
| 26, 28 | the hold: a run-time binding is held on the context of the actor that made it; an event's places at a level are one walk | e16c2593f, 0c41bc585, 0a527526f | `event/{this,on/*,binding/*}`, `actor/context/this.cs` |
| stage 3 (3) | a sub app's goals come through its parent; one read in flight per .pr; freshness by the .pr's stat | 8b3798015 | `goal/{this,list/this}.cs`, `type/item/path/this.Operations.cs` |
| 30, 31 | a type's history takes no prior (`history.closed`) | 7bbd246b7, 5dcb5bcfd | `type/item/{this,history/*}.cs`, `type/this.cs` |
| 27 | a root birth makes less (one pass over the classes; kinds' tables on the next look) | a2ae81cb1, b67c9b782 | `type/list/*`, `type/kind/empty/this.cs` |
| 33, 34 | OBP findings 4, 5, 8, 9, 10; S2, S3, S7 | 07f35ed1e, 5691c6804 | `event/*`, `goal/this.cs`, `type/item/*` |
| 35 | no runtime code for tests (own's Bind overloads, the reader's ctor, `service.Identity`, the bare code registry, the actor's name ctor) | 91b5013fc, 97a2c22c2, 0363b1467, 9cefc6fa5, c1837a736 | `event/on/own.cs`, `type/reader/this.cs`, `service/this.cs`, `module/code/this.cs`, `actor/this.cs` |
| 36, 38, 39, 40, 41 | every app is made from an origin (`origin/this.cs`, the code `origin/code.cs`, or an actor answering as itself); its lists are layers over it; Shared goes; the reader has two kinds; a type list's unders are typed in one place | a5c8b29eb, 523300500, a9f9a431f, d93fec83a | `origin/*`, `app/this.cs`, `type/list/*`, `type/reader/{this,code,layer}.cs`, `module/this.cs`, `goal/list/this.cs`, `actor/{this,list/this}.cs`, `type/node/*`, `type/item/list/this.cs` (`Chunks<TList>()`) |
| 43 | the surface golden's compare counts each line within its class | a6f455abb | `PLang.Tests/Wire/App/Surface/SurfaceTests.cs` |
| 42, 44, 46, 52 — stage 4 | a birth makes only what's asked for (table below) | | |
| 49, 51 — security S-A, S4 | a sub app is made in its maker's folder or under it, never in its maker's stores folder (`OutsideItsMaker`) | a92ac889e, 394f8f43f | `actor/this.cs` (origin's `Filesystem`), `origin/*`, `app/this.cs`, `PLang.Tests/Shared/Parent.cs` (rooted at `/`) |
| 56 — stage 5 | (1) each store's in-memory database is its own, and one nothing wrote answers without opening SQLite; (3) a setting class's path and members are its code's facts | 343657480, bf538555f | `store/{code/*,sqlite/this.cs}`, `type/list/Code.cs`, `type/item/setting/this.cs`, `actor/setting/this.cs` |
| 57 | one door to a class's facts on a type, `type.Facts(clr)` | b8be3c7bb | `type/this.cs` and its callers |
| 58 | a goal's .pr made once from its Path; a goal list finds the goal it holds for a .pr in one look (a private index beside its rows). The runner's list is every test app's parent: each add had walked every held goal's .pr under the lock | c63628481 (red 38dc51689) | `goal/this.cs`, `goal/list/this.cs` — plang --test about 15 s → 5.5–5.9 s |
| 60 | discovery makes the tests at the runner's width, in the listing's order | 33b6d3698 (guard b4199cc15) | `test/list/this.cs` — plang --test 5.0–5.4 s at load 11–13 |

### Stage 4, a birth makes only what's asked for

Each change is pinned by a bound test in `PLang.Tests/Runtime/App/Context/BirthTests.cs` or
`PLang.Tests/Wire/App/ChannelsTests/ChannelBirthTests.cs`, red before. Numbers are Debug, bytes allocated.

| # | What | Commit | Measured |
|---|---|---|---|
| 1 | a stream channel's read-ahead made on its first read | 8d2d8e029 | about 64 KB a channel |
| 2, 44 | console channels wired at the actor's channels' first use; the list born knowing (`app.Console`) | b9cd4235d, 9db20ebce | the rest of six channels for an app that never writes |
| 3 | a channel's Direction/Buffer/Mime made when read | 7723316c8 | a console channel 10,976 → 2,096 B |
| 4 | an actor's task list and permission view on first ask | cb7a59983 | an actor 25,552 → 21,608 B |
| 4, 46 | an actor is born with its role (`actor/role/{this,system,user}.cs`), which stands its grants in its view at the view's first ask (passed by security) | c1837a736 (guard 5779eed98) | a sub app 108,608 → 86,712 B |
| 5, 52 | a context's calls find their frame types at the first push, their audit at the first error (golden re-pinned with 52) | 3e3ea8f0e | a context 10,416 → 9,072 B |
| 6 | a memory's system variables (`variable/list/system.cs`) under its own, each made on first ask | a6dd78a3f | a context 20,448 → 14,216 B |
| 7 | `!app`/`!context` are system variables; the memory's table made at its first write | 449c70c2e, 72c26225a | a context 14,216 → 10,416 B |
| 8, 44 | a node finds its type, list and setting class (a Class fact) when first used | 395dd34ff, 6b03a1d2f | a sub app about −7.6 KB |
| 10, 45 | the code registry's tables and built-ins at its first use | 4c58310e2, 9cefc6fa5 | a sub app about 152 KB → 108 KB |
| 11a | a value's kept list made when a program adds a member | 4505251d3 | a Data over a value 1,064 → 312 B |
| 11b, 52 | a value's attachment and properties bag made when asked (`HasProperties`, internal) | bfb445060 | 312 → 176 B |

**Also:** c98b7b1bc, the one read in flight for a .pr asks for the held goal first. This fixes a race the sweep
exposed.

**Reverted:** 9, the app's id/folder/start made when first read (28bca04d1, reverted by 33270727c, decision 45). It
saved about 300 B, inside the noise, with no red test.

## Deferred to the follow-up, with its decision

- **S1** (34): the goal's stat stamped after its read, a late stamp whose cure is in the serializer's path.
- **The Registry's statics; `type.list.Assemblies` and `reader.Assemblies` as naked public lists** (35).
- **12, 13, 14** (42), root birth only, with their test-speed numbers:
  - `module.Action` as `list<action>`: module 1.16 ms / 363 KB → 0.10 ms / 58 KB;
  - a type's facts read through, Alias/Internal without init: 60 → 54 KB;
  - path's schemes as kind classes.
- **A sub app's code providers as a layer over its origin's** (42): a provider holds its app's state, so sharing
  instances breaks 29's rule.
- **Only the shipped identity provider can ask the origin** (Default.cs): an IIdentity loaded from a DLL can't
  (41, with 266).
- **Stage 5 (2), a list's positional reads from its snapshot** (55, 56): measured inside the noise; not built.
- **The report's coverage walk in parallel** (59, 60): it fell away. After 58 it is 0.13–0.19 s, inside the run's
  noise; the 3.3 s was 58's lock.
- **19 parts 2 and 3**:
  - a kind a sub app adds to its maker's type, refused as TypesShared until the kind lookup starts from the asker's app;
  - a sub app's loaded code going away with it (a collectible load context, class-keyed caches held by their app).

## Where a reviewer looks first

- `origin/{this,code}.cs` and the actor's explicit `origin` members (`actor/this.cs`): every part a birth asks for,
  the refusals below a layer, a sub app's folder rule.
- `type/list/{this,Registry}.cs`: lookups walk `Under` (the chunk rows typed); a kind below the layer goes to the
  origin.
- `channel/list/this.cs` `Table`, `module/code/this.cs` `Tables`, `variable/list/this.cs` `Table` / `_variables`: each
  is one door that makes its table on first use.
- `store/sqlite/this.cs` `IsBlank`, and `IStore.Open(file, Func<bool> memory, context)`.
- Bound tests that count process-wide bytes run `[NotInParallel]`
  (`BirthTests.ARequestReadsItsSettingClassesFacts_OncePerCode`).
