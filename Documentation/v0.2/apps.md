# Apps, origins and what they share

How an app is made, what a sub app reads from the app that made it, and what a run may write. Each rule is short and carries the file it lives in. A sub app costs what it uses: about 0.15 ms and 110 KB at birth, against about 19 ms and 2.4 MB for the command line's root app (`Tools/Bench/record.md`).

## An app is made from an origin

Every app is made from one origin (`app/origin/this.cs:9`, an internal interface), which answers each part the app asks for once: its shutdown, its folder, what its type list and module registry lie over, its goals, its System's identity, and whether it may add below its layer (`Shutdown`, `Filesystem`, `type`, `Module`, `Goal`, `Identity`, `Hold`, `Register`, `Remove`).

- **The code** (`origin/code.cs:8`) is the bottom. The public constructor (`app/this.cs:299`) hands it, and the command line's app is made so. Its lists hold what the code's classes give; it has no goals.
- **An actor** answers as an origin itself (`actor/this.cs:14`, members at `:166–216`). The internal door (`app/this.cs:306`, `new app.@this(path, actor)`) hands the calling actor, and the new app reads its lists live from that actor's app.

An app's type list, module registry and goal list are **layers over its origin's** (`type/list/Registry.cs:77` `Under`, `module/this.cs`, `goal/list/this.cs:70`), read through and never copied, so `text` is one object in every app made from the same code. What the origin loads after the sub app is made is the sub app's too. What a sub app loads goes into its own rows. A kind added to a type its origin holds, and an action added to its origin's module, are refused naming the type or module (`TypesShared` at `type/list/Registry.cs:234`, `ModuleShared` at `module/this.cs:136`), until a kind lookup starts from the asker's app.

The code's facts (choice sets, action properties, setting paths and defaults) are read once per code (`type/list/Code.cs`). They are the type list's own and private; no static holds them.

## Where a sub app lives, and who it tests as

A sub app is made in its maker's folder or under it (`actor/this.cs:177`) and never in its maker's stores folder (`:181`); either breach answers `OutsideItsMaker` (400) at birth, with nothing left behind. A sub app rooted in its maker's stores folder would read the maker's keys as its own.

A sub app of a testing app tests from birth (`actor/this.cs:192` `Testing`, read by `app/this.cs:241`), so its stores stay in memory whatever it reads before its session opens. A sub app of an app that is not testing is untouched.

The C# tests make their apps from one process-wide parent (`PLang.Tests/Shared/Parent.cs`), rooted at `/`, so a test's app is made under it (`/parent/child`). A test of a running app's disk behaviour makes its app from the code in a temp folder of its own.

## A run never writes an object its app shares

A sub app's types, actions and goals are the origin's, so a shared entry is every app's. What a run adds goes to its own layer or its own context, never onto the shared object.

- **A binding made while a program runs is held on its actor** (`actor/this.cs:68` `on`; the list at `event/on/list/this.cs`; made at `module/on/event.cs:73`). An event reads each level's own lists, then each actor's hold in the asker's app; the hold goes with the actor. `Own()` (`type/item/this.cs:483`, `private protected`) serves only the clauses a step is read with (`on.cache`, `on.error`, `on.timeout`; its one caller is `goal/step/action/this.cs:255`). An item's own `on`, written, no longer lists a run-time binding.
- **A type takes no history.** A type is what values are made as, never made from one, so its history is `history.closed` (`type/item/history/closed.cs:7`, asked for at `type/this.cs:701`). A read that finds a registered type adds nothing to it; otherwise every request would grow a shared entry for ever.
- **What may still write onto a shared object** is a cache that gives every app the same answer: a kinded type made once (`type/list/this.cs:218` `_full`), a member list made once (`:316` `_member`), a kind coined once (`type/item/list/kind/this.cs:11` `_coined`). The kind lookup reads without the type's lock.
- A write onto a shared object by a run is a bug even when a test passes; the guards are `PLang.Tests/Types/App/Types/SharedEntryWritesTests.cs` and `PLang.Tests/Modules/App/Modules/event/HeldBindingTests.cs`.

## A birth makes only what's asked

A part is made at its first use, never at birth. The doors are the same shape each time: a rows property that makes its rows on first ask, with a read door that answers empty without making them.

- Channels, console wiring and a stream's read-ahead: `channel/list/this.cs` `Rows`; the console is wired at the actor's channels' first use.
- The code registry's tables and built-ins: `module/code/this.cs` `Rows`. A memory's system variables, each made on first ask: `type/item/variable/list/this.cs` `Rows`, `type/item/variable/list/system.cs`.
- An actor's task list and permission view: first ask. An actor is born with its role (`actor/role/`), which stands its grants at the view's first ask.
- The store's file: `store/sqlite/this.cs` `Database` is blank or connected; a store nothing wrote answers empty without opening SQLite.
- A value's kept list and properties bag: made when a program adds a member (`HasProperties`, internal).
- What is made once and kept goes through `LazyInitializer`; a fact stays `??=`.

Each is pinned by a bound test of bytes allocated: `PLang.Tests/Runtime/App/Context/BirthTests.cs` and `PLang.Tests/Wire/App/ChannelsTests/ChannelBirthTests.cs`. A test that counts process-wide bytes runs `[NotInParallel]`.

A step cache belongs to its app and is disposed with it (`module/cache/ICache.cs:7`, `IDisposable`, no default).

## Tools/Bench and the speed record

`Tools/Bench/run.sh` measures a sub app's and a root app's birth (time, bytes), a request app (time, bytes) with its held memory after 3,000 requests and a rest, `plang --test`'s time, the C# sweep's time, and the C# build (clean and incremental). Each measurement waits for the one-minute load to fall under 3. Each run appends a row to `Tools/Bench/record.md` (date, branch, commit, load, each number); the noise band of each number is at the record's top. `BENCH_SKIP="build sweep ptest birth request"` leaves parts out. A number outside its band is a change, not noise; held memory that grows with the request count is always one. The rule for who runs it is in `CLAUDE.md`, "Speed record".

## The packages, the SDK and the build

`PLang/PLang.csproj` keeps the packages its code names (`:69–82`) and is a `Microsoft.NET.Sdk` project (`:1`), not `Sdk.Web`. A future web server module adds the `Microsoft.AspNetCore.App` framework reference back where it is planned (`Microsoft.AspNetCore.WebUtilities` stays as a package). Each test project carries only the build machine's native assets. A new package is added when code names it, and the build is the proof it is used: a search for a namespace misses a transitive one.

A no-change build takes about 4 s and one touched PLang file about 11 s; a change to a public surface recompiles every project that sees it. **The rebase trap:** a no-change build straight after `git rebase` takes 87–95 s, because the rebase rewrites the timestamps of the commits it re-applies and every project sees changed files. Rebase only when the remote has code changes. `dev.sh` keeps no stamp that skips a build, on purpose: a skipped rebuild runs a stale binary.
