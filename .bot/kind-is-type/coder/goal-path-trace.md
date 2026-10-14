# A goal's Path and PrPath: who sets them, who reads them

Trace on `kind-is-type` at 51558b38c (forked from runtime2 cf66e2d01). No code changed.

## The two members

- `goal.Path` — `PLang/app/goal/this.cs:76–77`, `[Store, Debug]`, a settable `path?`.
- `goal.PrPath` — `goal/this.cs:84–89`, `[Store, Debug]`, **derived**: `Pr(Path)` when Path has an absolute form; its
  `init` is a no-op.
- `goal.Pr(source)` — `goal/this.cs:94–95`: `X.goal` → `<X's folder>/.build/<stem, lower-cased>.pr`. The lower-casing
  is why a `.pr`'s own location can't give back its `.goal` file's name.

## Setters (where a goal gets its Path)

| # | file:line | from |
|---|---|---|
| S1 | `goal/this.cs:533`, `:606` (`Parse`) | the `.goal` path the caller hands in |
| S2 | `module/build/code/Default.cs:110` → `Goal.Parse(text, file, …)` | the build: the `.goal` it read (S1) |
| S3 | `goal/serializer/Reader.cs:68` | **the `.pr`'s stored `"path"`**, `path.Resolve(stored, ctx)`; `"prPath"` is skipped (`:70`) |
| S4 | `test/list/this.cs:118`, `:121` | a Stale test's stand-in goal: the `.test.goal` file found |
| S5 | `test/list/this.cs:120` → `Parse(…, file, …)` | the `.test.goal` found (S1) |

Every goal that **runs** comes through S3: a `.pr` read by `goal.Load` (`goal/this.cs:319–356`), setup discovery
(`goal/setup/this.cs:41–59`), the goal list's walk (`goal/list/this.cs:241`), test discovery's built goal
(`test/list/this.cs:130–138`), or a plain `read 'x.pr'` (the goal format's `Decode`, `goal/this.Item.cs:49–72`). The
reader is handed the file it reads (`ReadContext.Origin = origin`, `this.Item.cs:59`), but takes Path from the
stored key instead.

## Writers (what a .pr stores)

| file:line | writes |
|---|---|
| `goal/this.Item.cs:148` | `"path"`: Path's own output, its root-relative plang form (`/plan/app-systems/app/AppFactsArePlangValues.test.goal`) |
| `goal/this.Item.cs:149` | `"prPath"`: the derived PrPath (`/plan/app-systems/app/.build/appfactsareplangvalues.test.pr`), read by nothing |
| `module/build/code/Default.cs:201`, `:211` | the build saves the goal at `goal.PrPath` (from S2) |
| `module/build/code/Default.cs:336–339` | the build reads the existing `.pr` at `goal.PrPath` to merge |

The stored path is relative to the root the build ran in (`test/` for the test tree). Sub-goals store the same path.

## Readers

**Loading and keying**

- `goal/list/this.cs:41–44` `Add`: keys a held goal by `PrPath` (derived from the stored path).
- `goal/list/this.cs:266` `this[pr]`: finds a held goal by `PrPath == pr`. `goal.Load` asks it first (`goal/this.cs:323`).
  With a stale stored path the key isn't the `.pr` it was read from: a moved goal is never found held, so it is
  re-read on every `Load`.
- `goal/list/this.cs:240` `Walk`: skips a `.goal` already held, by `g.Path == source`.
- `goal/list/this.cs:187–188` `Held`: a call's name matched against `Path` / `PrPath` strings.
- `goal/list/this.cs:196` `Held`: a qualified name matched against `Address` (from Path, `goal/this.cs:188–197`).
- `goal/this.Item.cs:76–82` `Match`: a key matched against `Address`.
- `call/list/this.Snapshot.cs:214`: a resumed snapshot finds its goal by `PrPath`; `call/this.Snapshot.cs:27` writes it.

**Resolving names, files and trust from the goal's folder** (`Folder => Path?.Parent`, `goal/this.cs:81`)

- `goal/list/this.cs:70`: a bare call name looks in the caller's folder first.
- `goal/list/this.cs:145`: (the step's goal folder, the same lookup for a step).
- `type/item/path/file/this.cs:79–81`: **a relative file path resolves against the running goal's folder.** A moved
  goal reads and writes relative files in its *old* folder.
- `type/item/path/this.Authorize.cs:95–101` `AskedByOs`: **a step is trusted as the os's when its goal's folder is
  under the os folder.** The folder comes from the stored path. A `.pr` anywhere whose stored path says
  `/system/…` resolves (`path.Resolve`: the app's own `/system/` first, then the os's) to a folder under the os, so
  it would be trusted as os code (it then takes none of the actor's settings that change what runs, the terminal's
  environment). Not tried; read from the code.

**Facts the goal answers from its path** (`Where`, `goal/this.cs:146`)

- `IsSetup` (`:133`): read by `goal/list/this.cs:176`, `:228`, `:247`; `goal/setup/this.cs:26`, `:59`;
  `goal/this.cs:324`; `call/list/this.Snapshot.cs:210`.
- `IsSystem` (`:139`): read by `goal/list/this.cs:228`; `shortcut/this.cs:27` → `shortcut/list/this.cs:44`.
- `IsTest` (`:143`): read by `test/report/this.cs:65`.
- `Reference(caller)` (`:202`): `Path == caller.Path`, else `Address`; read by `module/goal/call.cs:47`.

**Tests** (`test/this.cs`)

- `:140` **`app.goal.Load(Goal.Path)`**: the run loads the goal again by its *stored* path. The 24 moved tests failed
  here: discovery reads the moved `.pr` fine (it finds it beside the `.test.goal`, `test/list/this.cs:123–130`), but
  the goal it holds says `/plan/app-systems/…`, so the run's Load looks for `Pr(/plan/app-systems/…)`: Not found.
- `:87` `test.Path => Goal.Path`; `:172–173` timings match steps by `Goal.Path`; `:194` the FAIL line;
  `test/junit/this.cs:44`, `:54`; `test/report/this.cs:58`; `test/Coverage.cs:66` (a coverage site's key).

**Messages and diagnostics** (shows the path, no behaviour)

- `call/this.cs:382`; `module/build/unreached.cs:20–21`; `module/on/cache.cs:69` (a cache key's goal part!);
  `module/ui/code/Fluid.cs:304`; `goal/setup/this.cs:130`; `module/screen/type/screen/display/this.cs:200`;
  `shortcut/this.cs:20` (a shortcut's name is its goal file's stem); `shortcut/list/this.cs:46`.

**os/ and test/ plang code:** nothing reads a goal's stored path (`grep` for `goal….path%`, `!goal.path`, `prPath` in
`os/**/*.goal` and `test/**/*.goal`, `.build` excluded: one comment, `test/app/callstack/CycleB.goal:1`).

**C# tests that hand-write a `.pr` with `"path"`:** 6 files (e.g. `Data/App/Core/GoalsTests.cs:154–256`,
`Types/App/Types/PathTests/GoalMimeDeserializationTests.cs`, `Wire/App/Serialization/PrEnvelopeTests.cs`).

## Shape: a goal's path is where its .pr is found

The `.pr` reader already knows the file it reads (`ReadContext.Origin`). The goal's folder is that file's folder's
parent (`<folder>/.build/x.pr` → `<folder>`), in the plang form the `.pr` was found by (an os goal found under the
os's `/system/` is `/system/…`, as the list's walk and `Load` already name it). What the location can't give back is
the `.goal` file's own name, because `Pr` lower-cases it. So the `.pr` stores **only that name**
(`"file": "AppFactsArePlangValues.test.goal"`), never a root-relative path, and the reader makes
`Path = folder + file`. `"prPath"` goes (it is derived, and read by nothing).

Then a goal moved with its `.build/`, or read under another root, has the path of where it is. `test.Start`'s Load,
`goal.list`'s keys (`PrPath` is then the real `.pr`, so a held goal is found), relative files, `Folder`, `IsSystem`
and the os trust all follow the real location. A `.pr` can no longer claim another place.

**What it changes or breaks**

- Every tracked `.pr` (489: 424 under `test/`, 21 under `os/`) changes two keys. Either all are rebuilt, or the
  reader reads an old `"path"` as its last segment (the file name) and skips `"prPath"`, so no `.pr` is outdated.
- A `.pr` read with no origin (bytes decoded from memory: some C# tests, a `.pr` arriving as data) has no folder, so
  no Path and no PrPath. `goal.list.Add` refuses a goal without one (`goal/list/this.cs:41–42`). Such a read needs
  an origin, or that goal is held by name only. The 6 C# tests that write `"path"` JSON change.
- An os goal: `IsSystem`/`IsSetup` read `Path.Raw` (`goal/this.cs:146`). The origin must be the plang form
  (`/system/…`), not an OS-absolute path. To verify: what `Raw` a `.pr` found under the os folder carries when read
  by a plang path versus an absolute one.
- The os trust (`AskedByOs`) then follows where the `.pr` is. That is the intent, but it changes behaviour for a
  `.pr` that today claims `/system/…` from elsewhere.
- A snapshot taken before a move names the old `.pr` (`goalPrPath`); resume won't find it after the move (as today).
- `on.cache`'s key includes the goal path (`module/on/cache.cs:69`): after a move, entries are keyed anew (right).
- Hashes don't change (`Hash` doesn't include the path).
- The build is unchanged: it parses the `.goal` it found (S2) and saves at that PrPath.

The alternative without storing a name: find the `.goal` beside the `.pr` by a case-insensitive stem match (a
listing per load, and nothing for an app shipped as `.pr` only). Not recommended.
