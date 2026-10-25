# bugs

## Why

The last three audits left bugs that no branch owned: item-doors' auditor list (`.bot/item-doors/auditor/final.md` §Next-branch, N1–N18), the goal channel's race (proven red by plang-tester, 2026-10-25), and the Windows window's leftovers from packages. Ingi put them on one branch after packages ("1. yes", 2026-10-25), with his three yeses from fast-apps as its last stage. One more joins it because it costs money: `plang --test` calls OpenAI on every run for its six llm tests (Ingi: "not on that branch, I think it should be [with] all the fixes on item door").

None of it is new design. Each item is a fix whose direction is already decided; the decisions are in `.bot/bugs/architect/summary.md` and the backlog (`.bot/item-doors/architect/backlog.md`).

## Rules for the whole branch

- Every stage's tests go red without its change, and green with it.
- The boy scout rule: a file a stage touches is read whole and left better. The older smells already known in those files are named under each stage.
- No runtime code for tests. A run never writes an object its app shares.
- The error rule: a method that can answer, answers its Error; a constructor or a reader with no result throws an `AppException` carrying it; a catch answers `ex.Error` whole; `Error.FromException` is the one door from any other exception.
- LLM tests run only at the gate until stage 1 is done; after it, they run on every commit, from their saved answers.
- Locked surfaces: only stage 7 adds to one, and Ingi has said yes to each (fast-apps' N1–N3, 2026-10-25). The surface golden is re-pinned citing that.

## Stages

### 1. A test's LLM answers come from a file

Today `llm.query` caches by default (`module/llm/query.cs:95`, `Cache`), in `app.store`'s `LlmCache` table (`module/llm/code/OpenAi.cs:37,156,474`). A test's app has an in-memory store that starts empty (`test/list/this.cs:34`), so every run asks OpenAI again.

After: a test's requests to OpenAI and their answers are kept in a file beside the test (`LlmCache.test.goal` → `LlmCache.test.llm.json`) and committed. The file stands where OpenAI stands, not where the cache stands (decision 4): a request in the file is answered from it, and one that isn't goes live once and is added. `LlmCache` stays the one cache, in the test's in-memory store as now, so `LlmCache.test.goal` still shows a miss and then a hit. Tool rounds and retries are requests too, so they are recorded as well. The cache skips them (`OpenAi.cs:153`).

This is a feature of `plang --test` for every plang app's tests, not just ours.

- One sender for the request, chosen once for the app (decision 5): `module/llm/code/IAnswer`. Its shipped default is `Live` (OpenAI's endpoint and key; when llm-provider lands, each provider owns its own send). The other is `Saved`, one test's file. The test registers `Saved` on its own app when `test.setting.llm` is `saved`, as it already opens its own store in memory, and each app has its own code registry (`app/this.cs:336`). There's no `if testing` where the request is sent (`OpenAi.cs:228`). Two llm tests run in parallel are each answered from their own file only.
- The file is text a reviewer reads in a diff: one list of {request, response}, in first-asked order. It holds the request body only (one endpoint answers every request, so the body is the whole question), never a header, so the key never lands in it.
- A changed prompt is a different body, so it's a new entry; its old answer is never served for it.
- The key is asked only when a request goes out live.
- On a pass, the file is rewritten to the entries the run used.
- A streamed ask always goes live.
- `test.setting.llm`: `saved | live`, default `saved`. The gate runs the llm tests once with `live`, which ignores the file and writes nothing, to check that OpenAI still accepts the requests.

### 2. Consent answers what really happened

All in `actor/permission/this.cs`, `type/item/path/this.Authorize.cs` and `module/identity/code/Default.cs`.

- **N1.** `identity.export` refuses with a typed error: exporting a private key waits for critical actions (item-doors 307). Its teaching (`os/system/modules/identity/export.*.md`) says so. The D4-1 guard stays. `IdentityPrivateKeyLeakTests` stays the pin. `Export_WithTheActorsYes_HandsTheKey` and `TheExportQuestion_…` assert the refusal. (The architect's security call with plang-security, 2026-10-25.)
- **N3.** A failed "always" save answers its error. Today `Add` returns `Task` and drops `Save`'s answer (`:161`), and `Keep` answers Ok regardless (`this.Authorize.cs:59–60`).
- **N4.** A one-time "y" never replaces a standing grant, and a standing grant never replaces a one-time one. Both are kept. Today `Add` overwrites the same-path entry in memory (`:165–171`), including the exact grant `Stand` gives the os folder (`:37`), so {read, execute} becomes {write}; `Stand` replaces by path too (`:49`). The shape goes to the architect before it's coded.
- **N5.** `PersistedGrantCannotOverrideStoresTests` pins what its comment says. Its fixture overwrites the store with text, so the persist never saves and all 7 cases pass in any order. Make the persist really save, and assert `Find` is non-null after it.
- **N8.** A bug inside an ask is not a "no". Today the catch turns any exception into PermissionDenied (`:83`). Only "nobody could answer" (a cancelled or broken input) is a denial; anything else surfaces as itself.

Known older smells here: the identity slot in `identity/code/Default.cs` (fast-apps N10, N12).

### 3. Readers answer their errors

- **N10.** The json reader's `Number()` checks its token (`type/item/kind/json/reader.cs:59`), as `Float()`, `Double()` and `Decimal()` below it do. Today `"value": true` from a peer becomes a typed null number with no error.
- **N11.** Type readers throw an `AppException` carrying their Error, never a raw `FormatException` or `NotSupportedException`: size (`size/serializer/Reader.cs:24,27`), parallel (`parallel/serializer/Reader.cs:25`), choice (`choice/this.cs:89`), kind (`type/kind/this.cs:218,231,319`). Then a bad value from a peer lands as its own error, not as a generic 500. `choice/this.cs:85` and `kind/this.cs:345` stay plain: they guard against a C# caller's mistake, never a peer's value (decision 5).

### 4. A goal channel runs its goal like a call

Today `InvokeGoal` runs the channel's goal on `Actor.Context` (`channel/type/goal/this.cs:117`), the channel's actor's root context, not the context of the write. Its comment (`:119–123`) assumes a fork's overlay carries down, but variables are stored per context (`actor/context/this.cs:41,134`), so it doesn't. In the tester's red test, 19 of 20 concurrent writes got the last writer's value, and under `foreach … in parallel` 16 of 20 runs saw another iteration's message.

After: a write runs the goal the way `goal.call` runs one (`module/goal/call.cs:86–100`). The channel's actor runs it, in a context made from the writer's, so a fork's isolation holds and a plain write behaves like a plain call. No locked surface moves: the written value carries its writer's context (`data.Context`), and an ask carries its action's (`action.Context`).

Known older smell: the general catch at `:151` builds a `ServiceError` from `ex.Message`; it becomes `Error.FromException`.

### 5. The command line refuses what it doesn't understand

- **N16.** Each of these is refused with an error naming the argument, instead of being answered quietly:
  - a second positional argument (today it becomes `Other=true`, `app/start/this.cs:42–55`);
  - a dotted positional, read as a property (`:124`);
  - `--test=module/x`, whose value is dropped (`Executor.cs:74`, `On` reads only whether a mode word is on);
  - `--debug=false` together with `--debug.setting.goal=…`, which still turns debug on (`Executor.cs:74–75`);
  - repeats that differ only in case, which are both kept (`start/property/list/this.cs:7,16`, keyed case-sensitively).
- **N14.** `%path%` is seeded from `app.filesystem.root`, not the raw working folder (`Executor.cs:102,112`). The build compares it with the landed root (`module/build/code/Default.cs:39`), so on Windows a folder reached through a junction or an 8.3 name builds the wrong folder.

### 6. Paths and the Windows window

- **N15.** `path/file/this.Validate.cs` compares a path with the root by `StartsWith`, with no separator boundary (`/root2` starts with `/root`): at `:46` (culture-sensitive too) and `:55`. At `:81` and `:83` it compares case-blind on every OS. This is wrong on every OS. The whole file takes one boundary-aware compare with `RootComparison`. It's the same rule as path's `IsUnder` (`path/this.cs:173–181`), shared as one string-level check, never a second copy (`:243`: they must not drift apart).
- **N17.** `OsTrustTests.cs:54` compares with a `/` separator, so it can only pass on Linux.
- **The Windows window** (`module/screen/type/screen/window/code/Window.cs`, Windows only; Ingi's yes, 2026-10-25):
  - It repeats `Shown`'s delta logic (`ApplyBinary`, `Apply`, `Move`/`Copy`) and keeps its own QOI decoder beside `code/Qoi.cs` (stored twice). It uses the shared ones, so they're tested on Linux.
  - It multiplies a message's own `w*h` for QOI parts (`:236,305,320`), which is unbounded. The frame's bound (`screen.setting.max`) applies, as `Bitmap` does.
  - `Offer` keeps the first decoder, and a `Patch` before the first `Offer` installs a no-op decoder (`:110–116`, `:198–201`).
  - The `_max` mirror (`window/this.cs`) exists only because `Offer` keeps the first decoder. With the max passed with each offered frame, the mirror goes, and so does the setting read on every `Draw` (`window/this.cs:117`).
- **The Windows run** (backlog item 8) proves this stage on Windows: LinkSpotTests, StoresFolderTests, RealPathGateReCheckProbeTests, the sub app containment probes, and the window. It waits for room on the host's C: drive. Until then, the stage is proven on Linux plus a win-x64 build.

### 7. Ingi's three yeses (fast-apps N1–N3)

- **`module.Remove` answers data** (`module/this.cs:172`): Ok true or false, or a ModuleShared error. Today it's a public `ValueTask<bool>`, and a refusal is a throw.
- **The console flag becomes a choice** that names both behaviours (`app/this.cs:294,301`, `Executor.cs:44`). Today it's a default-true `autoWireConsoleChannels`, which breaks "a flag is false by default". It has 34 test uses.
- **A channel is born in its list** (Ingi: "yes on channel"): every channel type's constructor takes its list, and its actor is its list's. `Adopt`'s late stamp (`channel/list/this.cs:164–166`), the settable `Channels`/`Actor` and the `?.` chain go.

## Not on this branch

- N2 (archived identities) and N9 (egress re-signing): stores per actor and request apps.
- N7 (consent over a message channel): snapshot.
- N6, N12: they wait for their first user. N13 is the architect's ruling.
- `Publish/*.goal` naming the deleted projects: the 2.0b checklist (Ingi, 2026-10-25).

## For the coder

Read it, and push back on anything that's wrong in the code before you place it. Stage 1's shape is yours to propose. The other stages' directions are decided; their code is yours.
