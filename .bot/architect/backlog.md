# The list

The one list of work after runtime2's merges (Ingi, 2026-10-29: "yes merge into one list"). It replaces `.bot/item-doors/architect/backlog.md` and `.bot/snapshot/architect/plan/later.md`, which now point here. Each item says where it came from (a branch's decision number) and whose call it is. A branch's own log keeps the full reasoning. Checked against runtime2 6cfe782d7 (snapshot's merge); an item marked *unverified* wasn't re-read in the code.

## Now

- **event-action**, cut 2026-10-29 from runtime2 6cfe782d7 (Ingi: "lets do the event-action"). What an event runs is an action: the `binding` folders become `event/action/`, `app.event` reached by path, each binding chooses whether it reaches sub apps, the C# handler its own kind, one event class. Plan: `.bot/event-action/architect/plan/start.md`.

## Ingi's order (2026-10-25: "I agree on the order")

1. ~~JSON Lines~~ (493b157df), ~~decider-key~~ (b4a549d1c), ~~bugs~~ (0a178687b), ~~windows~~ (d72538402), ~~snapshot~~ (6cfe782d7): merged.
2. **Stores per actor and an app per request.** Tabled (Ingi, 2026-10-26: "not ready for actor"). A webserver can't run without it. The structure (item-doors 317a): a webserver is a terminal app whose User starts the server; each request is an app whose System is that User and whose User is the caller (identity optional, never a private key on the server). Keys stay inside the app; no outside key store. The architect's open proposal: a store belongs to an actor and stores nest as actors do (`/.data/`, `/.data/user/`, `/.data/user/<caller>/`). What rides with it:
   - item-doors auditor N2: every actor's rows are judged against the system actor's identities (`module/identity/code/Default.cs:264–269`, `type/item/setting/this.cs:130–141`); with it, the core setting's reach into `module.identity.code.IIdentity` and archived identities' rows read as foreign until saved again (316b).
   - item-doors auditor N9: egress never re-signs a Data that holds a signature (latent; a ruling with a red test).
   - fast-apps N5 / item 10: a request app is rooted at the server's folder, so its file store would open the server's `.data/`. Security's T1 (`T_LATENT_*` in `SubAppFullPassProbeTests`, skipped with this reason: unskip them first). The SQLite pool: each store clears its database's pool on dispose (`store/sqlite/this.cs:310`), which drops other apps' connections once they share a file; Ingi's question from test-speed §29: "never clear a file database's pool per app".
   - fast-apps item 9: `app.parent` in the same process and one owner for the end that answers a call (PlangOS's display `Called`, `module/screen/type/screen/display/this.cs:255`, moves onto `parent/this.cs`). Test `TestAppHasItsRunnerAsParent` restorable from a9e513045.
   - The settings rule (windows, Finding H; Ingi: "if sub app ask for setting and is unset, then get it from parent"): only the root reads the environment, for every setting. Could also be its own small branch.
   - fast-apps N10: late stamps in actor/context and actor/setting, the identity slot at six doors, two kinds of store instead of `bool memory`.
3. **The LLM provider and writer** (parked branches `llm-provider`, `llm-writer`): the builder's quality. llm-provider's design is complete (its log 853–866; open note 866: a model picks its provider). llm-writer is parked after its stages 1–2; its stage 3 waits for llm-provider.
4. **Returns** (801, plan only).

## Written up, waiting for Ingi

- **module-list-type** (snapshot 231–232; `.bot/snapshot/architect/plan/branch-module-list-type.md`). A module's own list (a `list<T>` subclass) becomes a type of its own, so it can be a setting's or a parameter's slot read by its own reader; debug's `Watch` moves onto its variables list and the per-step parse in debug's `Variables(sb…)` goes. Locked type system. Open: (1) how a module's list shows in text, its own name or `list<variable>`; (2) whether a module's type may take a name a core type has.
- **service-actor** (843, plan only): the far end of an outbound call is a Service actor (reverses runtime2-channels stage 7). Order not set.
- **A goal as a code provider** (item-doors 266): any `[Code]` interface pluggable with a plang goal. Needs design; the recursion of a goal-backed setting store's own settings is the example to answer. The `Code.Get<IStore>()`-or-throw written twice (864a3d1df) goes with it.
- **The OBP-root follow-up** (item-doors 280; `.bot/item-doors/architect/v3-triage.md` §B and §D), each a locked question: the view answering "from our own store" (Window/Origin/RequiresSignature); `type.Make` answering value-or-error (retires ~29 throwaway carriers); a type answering its settings address (no `Activator.CreateInstance` per class); a typed `Bind<T>` on the event door; Data born with its context. Also 315c (the filesystem as a plang type), 315b (the terminal's ToString and an `IWriter` location primitive), E (a folder's `Absolute` in OS form), R5.
- **Critical actions** (item-doors 307): an action marked critical asks three scam-check questions an LLM judges; "scammed" blocks it 24 h (v0.1: `PLang/Errors/AskUser/AskUserPrivateKeyExport.cs`). Until then `identity.export` refuses (bugs N1). With it, the sensitive text kind (masks itself in every view except one reveal door; security's three proofs), which also closes Finding H's probe (`KeyNavigationProbeTests`).

## Before plang 2.0b is published (Ingi, 2026-10-25)

- **Brotli** ("keep eye on it before we release"): check the shipped .NET runtime carries Google's fixed decoder, else drop `archive/type/archive/kind/brotli/this.cs`.
- **`set %x% = %x% + 1` builds set-before-add 4 times in 5** (plang-builder, event-action 79, 2026-10-29; older than event-action, in runtime2 too: it happens the same with or without the writer's examples). The decider is sure of `variable.set` (0.98) and gives `math.add` only "possible" (module math 0.50), so the writer's starting line is `variable.set(Value, Name=%x%)`. The writer keeps that order and puts math.add after, and the set then stores the previous `%!data%`. Committed .prs built earlier are right; a fresh build of the most common step in plang is wrong. The builder bot traces it after event-action (5 whys: the module's score in stage 1, or the starting line's order). The same family as a set's verb pre-empting the module action. Proof: a goal of that one step builds add-then-set 5 of 5. Capture: the builder's scratchpad `noex.err`.
- **The release goals** (bugs 2): `Publish/Publish.goal:20` publishes the deleted PlangWindowForms; `Publish/Nuget.goal` packs the deleted PlangTests. Drop those steps, run the pipeline end to end once.

## Branches and items, no order yet

### Types, data and the serializer (locked: each addition is Ingi's)

- **`type.list.Of(name, kind, …)` by parts** (item-doors 198, Ingi: "sounds good"; never built): 96 sites still write `new global::app.type.@this("…")`. With it, F2.
- **A read's origin is lost below a container** (snapshot 28–29): a template inside a container read from a file with load vars comes back literal (`type/this.cs:469,475`, `type/item/wire/this.cs:49`, `type/item/serializer/json.cs:103,197`). Suggestion: carry the origin; proven on `read file`. Ingi's.
- **No module DLL outside PLang compiles** (snapshot): the generator emits `data.As<T>()`, internal on Data (`data/this.cs:629,656`). Find every internal a generated action reaches, then one public door. Snapshot's proof that a resumed frame calls a run-time module's action waits on it.
- **Typed lists (`list<on>`)** (snapshot 317, 321): the list says once what its rows are and the rows go bare; needs the core list reader to read records for every typed list.
- **An empty dict writes nothing**, as an empty list does; changes the Out view too (snapshot 208).
- **`data.Held()` gets a name** (snapshot 343, 348; `data/this.cs:711`): the value door as a question reads it; `Content()` is taken. **DynamicData's ctor parameter `declared` → `type`** (`data/this.cs:964`).
- **The bare-row rule's five type literals** (`View.Record`, snapshot 324–325): stay until a type can say "the type a bare token reads back as".
- **The `kind["*"]` recipe and `Kept<T>`** (snapshot P7-2, 205, 235): one record-reading recipe at several sites, a missing door.
- **A frame's place handed in at push** (snapshot 325): needs a parameter on `action.Start` and `call.list.Push` (locked call surface).
- **Only item's kinds are walkable at `app.type.kind`** (json-lines 12; `type/this.cs:905`): needed only when another type's kind has settings.
- **Record types holding C# values a template row can make** (windows 21, 25): `error/this.cs`, `warning/this.cs`, `module/llm/type/message/this.cs` keep C# strings, so a template's variable is lost; the fix makes them plang values rendered by the sending layer, and llm.query's `Peek() is source { HasVariable: true }` check goes.
- **Sub apps own their code** (fast-apps 19–20, item 11): (a) a kind a sub app's DLL adds to a parent's type lives in its layer (until then refused); (b) the DLL unloads with its app (collectible load context; class-keyed caches per app or weak: `type/list/Code.cs`, `type/item/this.cs:513`, `event/on/empty.cs:11`, the JSON serializer's metadata).
- **A sub app's own teaching** (fast-apps 31, item 12): the catalog's teaching items are made once with the module's app's context; nothing needs it until a sub app builds.
- **fast-apps auditor N6–N9, N17, N18** (`.bot/fast-apps/auditor/final.md` §7): "has code of its own" with one owner; the origin's two refusal voices; the registry's type switches (`ContainerFamily`, `PlangName`), `IsCondition`/`IsBranch`/`Link` by literal name, `set.cs`'s throwaway Make, frames as types, channel catches `when (is not …)`, data's "did it answer itself"; the locked memory's verb+noun members (`GetNames`, `GetAll`, `GetChangedSince`, `ShallowCloneStore`); a `Class` pinning a DLL's types, the debug module's `item is goal` filters; fast-apps 42's root-only items (`module.Action` as `list<action>`, `Alias`/`Internal` without `init`, path's schemes as kind classes).
- **`property.Read` doesn't answer an AppException a getter throws** (`type/property/this.cs:94`), where `Write` does (`:138`).

### Events

- **`on/empty.cs` and its static `Emptied` stayed** (event-action's demolition list, not done): a per-CLR-class static cache of an item's empty moments, used as the action's clause fallback and to answer "is this a moment's name". A static (Ingi: no static), and the class's moments belong on its type. Removing it means the item's type answers its moment names (`[Events(...)]`, the locked events surface: Ingi's yes), and the action makes its own `on` on its first ask.
- **on.event's `Event` parameter holds an `on`** (event-action 20, the coder's point 5): since decision 13 the moment is an `on`, and since 4 an `event` is what's bound, so the parameter's name says one thing and its type another. Kept by decision 12 (no `.pr` changes); renaming it is a teaching change and a rebuild of every on.event `.pr`.

- **Parsing a bare `%name%` costs 28.7 KB** (plang-tester, event-action 24, `.bot/event-action/tester/variable-events.md`): `Named(name)` (`type/item/variable/list/this.cs:213`) runs the full parser for a root name. The parser is the one definition of a reference, so the fix is the parser answering a bare root name cheaply, not a bypass. Anything under ~1 KB makes a type-level variable event on every set affordable.

### Cache

- **The default cache key is the step's place, not its parameters** (event-action 11, Ingi: "yes"): `module/on/cache.cs:66–71` keys by goal path and step index, so `call LoadData id=%id%, cache for 10 minutes` or `read file %name%, cache …` answers the first value for every id or name unless the step names a key. Fix: the default key also takes the action's parameters (as they resolve). A test per case goes red today.

### Second answers (OBP pattern 12)

- **Identity's `Held()` beside the gated `GetOrCreateDefault`** (`module/identity/code/Default.cs`, snapshot 335; plang-security, 2026-10-29): two doors for "the default identity", one gated, one not. Read-only and safe today, but a second accessor beside the door that checks, and `held` is a banned word. With `data.Held()` (below, "Types, data and the serializer") the same name shows up twice.

### The builder

- **on.event's build warns on a path that can never reach, and should refuse it** (event-action 81): `%!app.type.file.on.start%` (the app has no type `file`) is kept with an EventUnreached warning, so the writer's retry never sees it. A refusal needs navigation to tell "never there" (a member a fixed type doesn't have, a type or module name the app doesn't hold) from "not there yet" (a channel a step before creates). That's a locked-core design, not a wording fix. It wouldn't catch a wrong path that does reach (`%!app.type.step%` for "each file action").
- **Writer reliability on event paths, known** (event-action 67, 81, 87): AfterEachLlmQuery 2/3, EachFileAction 2/3 (with two module examples; 0/6 without), NamesAFolder (`before each goal in api/*, call Count`) path 6/6, whole step 4/6: twice the Action was a `loop.foreach` ("each goal … call" read as a loop). Each test commits a right build; the score is the writer's.

### Speed

- **HELD: the type list's tables made at build time** (was call-cost's stage 6; Ingi: "maybe wait with tables"; call-cost merged without it, 36c559831). The prototype: `typeof` tables cost 39–43 ms of JIT, so not that shape; name-only tables resolved at first ask would save ~85–95 ms of a ~210 ms cold root (CLI ~600 → ~510 ms). Open question: tables only (PLang's package ships its generator; six fixtures and `ChoiceSetNameTests` rebuilt) or a kept reflection scan (a fork). Also from the codeanalyzer: one scan shared per process was built and dropped (only tests called it, call-cost 20). Was: **the type list's tables made at build time** (the codeanalyzer's section H and `startup-tables.md`, on call-cost; Ingi to the codeanalyzer: "option 4 looks interesting"). A cold root app is ~210 ms, and ~105 ms of it is `origin.code` scanning the PLang assembly by reflection (`type/list/Code.cs:141–181`), again for every root app. The generator writes each assembly's tables. Generated tables are the only door: an assembly without them brings no plang types, with no reflection scan beside them (a fork). Expected ~60–90 ms off a cold root, CLI ~600 → ~520 ms (arithmetic). The prototype's JIT cost is measured first. Also one scan per process, for servers and the test runner. **And lazy** (Ingi to the codeanalyzer: "and lazy ofc"): each table is read at its first ask, and a type's entry is made when its name is first asked, not for every class at the scan (the ~38 ms of entry-making). Only an enumeration of all types makes them all, with a pin comparing a fresh list's enumeration with the eager one. The generator and the type system are locked: Ingi's yes.

- **Per-step plumbing, a later branch** (plang-codeanalyzer, 2026-10-30, after Ingi asked it how plang compares to Node warm; Ingi's go to cut). Steady state on a quiet machine: a one-`set` goal 11 µs, an if/else goal of 6 steps 117 µs, a loop of 3 with a goal call 225 µs, file ops 2.2 ms (Node 1.9 ms; Node's logic ~1–5 µs). A 1-step goal's 12 µs by layer: goal frame and events ~3, step frame ~2, the action's wrapper (call frame, clauses, the `%!data%` write 1.1, Settle) ~3, Resolve 1.5, the handler ~3.5 (its bind ~1.2). The work is ~1 µs; the rest is the pipeline, and a step that calls a goal pays it again. **Idea 1 (pattern 11): every call frame mints its id** (`call/this.cs:252`, `Guid.NewGuid().ToString("N")[..8]`, 2–3 frames per step), read only through its `[Out, Debug]` view (`:61–62`, which also makes a new text per read); the first asker mints it, a saved frame keeps its own. A mutation with a constant id: a repeated run 163 → 144 µs (~12%). A private change inside the locked call memory (`Id` stays). **Idea 2: what each frame, step or action makes that only a rare asker reads**, scanned by the architect when the branch is cut, each item measured on its own hash by the codeanalyzer: before/after clause and event lists for a step with none; Resolve's handler with every slot per run; the `%!data%` carrier on every step; a goal call's ~12 µs of frames (a loop of 3 pays 36 µs before its body). Not found: exceptions, thread hops, lock contention; allocation (61 KB per if/else run) isn't the main cost. Cold start (600 ms against Node's 37 ms) moves only with ReadyToRun or the tables, both held.
- **Branch `call-cost`: merged** (36c559831, 2026-10-30). Was: agreed (Ingi, 2026-10-29: "on the questions I agree new branch"). It is cut from runtime2 after event-action merges, because it touches the same files (goal.list's Find, the frames, the type minting). Its ideas, from `.bot/event-action/codeanalyzer/call-cost.md`: 1 (a shortcut keeps its name), 2 (a type found, not made; also event-action's +28 KB), 3 (a frame's store on its first write), 6 (a goal index), 7 (`Broken()` once). Then 4 (the generator writes a static `Resolve`, so the throwaway handler goes) and 5 (generated parameter binding), each measured on its own. Ingi's yes on 4 and 5 (2026-10-29: "1 yes on 4 and 5"), changes to the locked generator shape and the action's door: each is re-pinned with this decision. Idea 2's named site (second addendum, ebdc59f55): **about 55 types are made per goal run by `number`'s implicit operators** (`type/item/number/this.cs:36`, `new type("number", typeof(number)) { kind = kind }`, which also makes and throws away a `kind.empty`), called from `call.list.Push`, `call.this.Depth`, `list.Count` and `goal.step.list.Start`. A trial that kept the type per kind cut a steady run from 230 to 178 KB (−23%) and the first run from 300 to 246 KB. The fix is **not** the trial's static `ConcurrentDictionary` (Ingi: no static), and not a static key either: the callers make their numbers through their app's type, which the type list keeps since event-action 74, and the operator stops minting. They reach the type by reference, never by building a key `new type("number", "int")` to look it up: `App.type.list[typeof(number)]` (kept), then the type answers its kinded type, NEW `type.Kinded(kind)`, made once and kept by the type ("a kinded type made once", CLAUDE.md). Ingi: "Ok" (2026-10-30). Then the two `bool` readers that make a type per read (`type/item/bool/serializer/Reader.cs`, `type/serializer/Reader.cs`, ~3%). With it, the auditor's C1 (event-action re-check 084d73b49): goal.Load and Decode, step, type birth and item navigation let a failed events load's `AppException` out of methods that can return a result (the error rule: answer it). The Executor catches it at the entry (`Executor.cs:56`), so a run isn't broken. Also, from event-action 97: **six doors answer a failed events load with the same two catches** (goal.Start, action.Start, the variable write, channel write/read/ask: `AppException` → its Error; a cancelled wait → `GoalError.Cancelled`). That's choreography across files. A door answering `data<on>` would own it, but it allocates on every ask (three per action), so it's decided with this branch's measurements. Idea 12 (section G, c659a8b89): **an action asks the settings for every parameter the step didn't give, on every run** (generated `Resolve` → `context.Setting.Get(action, option)`, 1.5 µs and 1.0 KB each: two address strings, the scope walk, two awaited `Saved` rows, a `NotFound`). A `set` asks two, always "not set". The fix: the setting store (`actor/setting/this.cs:109`) keeps its answer per address by its change count (`Set` already bumps it with `Move()`), the way the type list keeps a class's answer by its version (event-action 74). The owner keeps it; the action doesn't cache the setting's job. About −3 µs and −2 KB per `set`; with idea 4, about −40% of a dispatch's ~12 µs. Also idea 10 (the codeanalyzer's addendum, 632a07139; pattern 11): **the step cache is made at every app's birth** (`app/this.cs:199`, a .NET MemoryCache, 47 KB and 29 µs), though only `on.cache` and the signing nonce ask for it; made on its first ask, a sub app's birth goes 101 → 55 KB and 0.48 → 0.14 ms (measured as a mutation, the suites not run). Idea 11, **ReadyToRun publish** (cold start ~460 ms, ~60% of it JIT; a trial ran the first goal in 0.35 s instead of 0.60–0.67 s), a build setting: **not yet** (Ingi, 2026-10-30: "no not yet"); not part of call-cost. Also (Ingi, 2026-10-29: "agree"): **a Data copy shares its source's property bag until its first write** (`data/this.cs:741`, `Copy`: today every goal-call parameter and every `set %y% = %x%` copies the bag; most variables never get a property). The codeanalyzer measures a copy's wrapper and bag first. The stale "0.03 ms" in `shortcut/this.code.md` is fixed with it. The plan is written when the branch is cut.
- **The cost of calling an action** (Ingi, 2026-10-29: "it would be interesting to analyze if we can speed that up … I dont think specifically shortcut, but just general when calling an action, but good to have focus"): queued for plang-codeanalyzer after its event-action review. Focus: a `%!goal%` read, ~0.2 ms against ~0.03 ms for a value in memory (`shortcut/this.code.md`). Also a step of one action, and a `call X` to a goal of one step. Each broken down by piece, with ranked ideas. Written up at `.bot/event-action/codeanalyzer/call-cost.md`. Ingi: "tricks such as code generator, aop, etc. is acceptable" (compile-time emission, weaving, work kept in the .pr), still one door.

### Formal's two directions disagree (locked serializer: behaviour fixes)

- **13 action examples don't read back from formal** (plang-coder, event-action 65, a3942896b): written to formal and read back, they don't give the same action, so they stay JSON in the writer's prompt. None reaches a committed .pr wrongly today (the coder checked each).
  - `Parallel=true` writes `{}` (parallel's form for "on, at the machine's cores", `type/item/parallel/this.cs:39–49`), which reads back as a different row. Risk: pick.list writes a chosen option through `formal.Writer.Option`, so a decider pick of Parallel would show the writer `Parallel={}`. (goal/call:28, :31; loop/foreach:20; snapshot/resume:8)
  - list.query's `order: [{field, desc}]` (list/query:14, :20, :23, :26, :29).
  - llm.query's Message: the examples write `role`/`content`, the .prs hold `Role`/`Content` (llm/query:2, :5).
  - archive.unpack's Value from a literal path writes `{"held": …}`: a value writing its insides, and `held` is a banned word (archive/unpack:5).
  - variable.set's `list<path>` Type (variable/set:29).

### Branch `tostring-sweep` (Ingi, 2026-10-29: "yeah backlog for sweep branch")

A value opened into its text with `ToString()`, where the value should answer. Counted 2026-10-29 in production C#: 387 calls in 157 files (335 bare `.ToString()`), and 66 types override it. Interpolation (`$"{value}"`) isn't counted.
- Fine, left: StringBuilder/writer results (~37), formatting at a real boundary (`Guid.ToString("N")`, dates `"O"`, numbers with `InvariantCulture`, ~30), and error messages and logs (~15).
- **First, compares and keys (~23):** a value's text compared or used as a key, where the value has its own `Equals`/`Is`. The same fix as event-action 72. E.g. `goal/step/action/keep/this.cs:17`, `goal/step/pick/question/this.cs:72`, `goal/step/pick/list/this.cs:242,616`, `goal/list/setting/this.cs:36`, `type/item/note/this.cs:53`. The untouched-file sites listed under "The value answers its own equality" below go in this branch.
- **Then, logic (~250):** a value opened into text to decide something, read one by one (some are leaves where the text is the answer), folder by folder: `type/item` (95), `module/http` (22), `goal/step` (18), `module/llm` (17), then the rest.
- The signature-identity compares (`type/item/setting/this.cs:139`, `module/identity/setting/this.cs:26`) and the sandbox's path regex (`module/terminal/code/Sandbox.cs:92`) go to security first.

### The value answers its own equality (Ingi, 2026-10-29: "check other places for this wrong pattern")

Equality asked from outside, as strings, where the value has its own `Equals` (event-action 71–72 fixed the files that branch touched). Files no branch has touched yet:
- `module/archive/type/archive/kind/this.cs:40`: `at.Absolute.TrimEnd('/') == into.Absolute.TrimEnd('/')`, two paths compared as trimmed strings.
- `module/terminal/code/Sandbox.cs:92`: a regex over `path.Absolute` compared to a real path. The sandbox: security reads it first.
- `type/item/setting/this.cs:139` and `module/identity/setting/this.cs:26`: `identity.PublicKey` against `signature.Identity.ToString()`, a signature's identity checked by text (the same code twice; security reads it).
- `module/archive/type/archive/held/this.cs:30`: `IsData => Of.ToString() == "data"`, a type check by its word (and `held` is banned).
- `goal/list/setting/this.cs:36`: a choice compared by `ToString()` on both sides.
- `type/item/note/this.cs:53`, `module/code/this.Snapshot.cs:104`, `serializer/Snapshot.cs:27`: a name's text compared outside its owner.
- `goal/step/pick/list/this.cs:242`, `module/build/code/Default.cs:263`: the builder comparing step text; check whether they are values or the step's own text.
- **`path.RootComparison` is a static** (`type/item/path/this.cs:32`; Ingi, 2026-10-29: "no static"). The OS's case rule is read from outside by `goal/pattern/this.cs:20` and `type/item/permission/this.cs:110` (each hands it to a Glob). Whose fact it is (the path's, or the file system's, `app.filesystem`) is the question; then the matchers ask that owner's instance.

### Sub apps and their makers (security, event-action 91)

- **A bare goal name falls through to any goal the app's list has read, anywhere** (F4, Low, older than event-action; `goal/list/this.cs:239`). A platform whose root holds an upload's folder has read the upload's goals as its own (`goal.App` = the platform), so a bare `call Record` written where no Record sits beside it can run the upload's. Probe `PlatformStoryProbeTests.E` (skipped on event-action, naming this). Fix options: the read-anywhere fallback leaves out goals under a sub app's root, or a maker's list never reads a folder it made a sub app in. Also covers security's Info: a `call %variable%` the upload set reaching its own goals under the maker's root. The question for Ingi: should a bare name fall through anywhere at all?
- **`%!app.actor.list[0].channel%` reaches another actor's channel list** (security Info, older than event-action). The same rule as 66's `actor.event`: what an actor holds is reached through the asker's own door only.

### From call-cost's security review (Info)

- **A C# handler's constructor now runs once with its module's System context** (security I2, call-cost: the catalog element's `Resolver`). Only third-party `module.add` handlers could do anything in a constructor. If one does work there as System, it does it once, at the class's first run. Worth a line in the module-authoring docs: a handler's constructor does nothing with its context.
- **A kept settings answer from a saved row is born with the keeper's context** (I1): it only matters for a saved row holding a `%variable%`, and no such shape was found.
- **A kept settings answer's properties are shared across runs** (security B1 follow-up, Low, latent; probe a3a916034 on `security-call-cost`). `As<T>()` of the kept answer shares its properties, so a property one run writes on it is read by another run of the same actor. No production handler writes onto a parameter it was given today. The fix with the next branch that touches settings: the keeper hands each ask its own copy (`answer.Copy(name)`, ~112 B), or keeps the value and each run makes its Data.

### From call-cost's audit (minor, deferred)

- The readers' null rows still build `list[new type(...)]`, and the `type` reader's null row makes a `{type}` with no list.
- `Broken`/`Changed`: a narrow race between a goal's judgment and a step's Error written during it.
- The `CompareExchange` lazy line is written seven times. One idiom, but no one owner.
- `call._depth` is reached from outside the frame (`call.list.Push`).

### Docs, a later pass

- `Documentation/v0.2/builder-runtime.md:164` names `IClass.SetAction`, which doesn't exist in PLang/ (plang-docs, at call-cost's merge).

### Lists' own doors

- **`goal.list` inherits the base list's internal `RemoveAt`, which doesn't let `_found` go** (plang-coder, call-cost 22). There's no production caller (only `Add` removes, under the gate, and lets it go). If a remover comes, it goes through the goal list's own door, which lets the kept candidates go.

### Read once, pass on

- **`actor.list.Use` reads the actor's name twice** (`actor/list/this.cs:39`; plang-coder at event-action 64, the same shape it fixed in `variable.set`): `named.Given() is null` reads the name to ask "none named?" and drops it, then `named.Use(...)` reads it again. Cheap for a choice, but it is a dropped answer: keep what `Given()` answered and use it.

### Lazy (OBP pattern 11)

- **The shortcut list is read at the app's start** (`app/this.cs:535–537`, found 2026-10-29 explaining shortcuts to Ingi): Launch reads every shortcut before anything runs, so a name clash (an app `/shortcut/x.goal` taking a system name) stops the start. That is work at start; the first `%!x%` read is the asker. Also `shortcut/this.cs:20` decides `IsSystem` from `path.ToString()`, while `this.code.md` says it asks its goal (`Goal.IsSystem`).

### Dead members

- **`output.ask`'s `Variable` parameter is declared and never read** (plang-docs, 2026-10-29, at the snapshot-doc merge 9ad73cb3f): `module/output/ask.cs:53`. A snapshot already holds the run's variables. Either give it its job or remove it with its teaching (a catalog change: re-pins).

### Snapshot's leftovers

- **The run's call at the snapshot's top** (`{app, call}`; snapshot 293–294). Ingi: "let's leave this long for now".
- **Finding a channel again after a resume** (snapshot 225): a binding on a channel refuses the save (NoWireContract).
- **The auditor's minors** (snapshot 341, `.bot/snapshot/auditor/final.md`): snapshot navigation can throw out of `View.Make`; `resume` creates a default identity as a side effect; late stamps in `call` (`call.Under`, accepted at 324) and in `step`; dropped answers at `call/action:79`, `Resume.cs:47`, `test/this.cs:144`; the code registry keeps partial state when a DLL is refused; the sign/verify view asymmetry (plausible, not reproduced).
- **Item types whose clone copies through a live or shared reference** (snapshot 39): channel list, module, type nodes, program nodes (action, shortcut, test, setting action), browser/cdp/screen types, llm conversation and exchange. Each a one-line Clone in a branch that touches it. (The event ones go into event-action.)
- **The three ask tests** (snapshot 35; text in 624eea3b3's parent): belong to the callback plan, which doesn't exist yet.
- **Callbacks** (`start %callback%`, an ask over a connection that isn't kept): later, by Ingi.
- **item-doors auditor N7** (the answer on resume): was placed with snapshot; *unverified* whether snapshot closed it.

### Settings, secrets, identity

- **Two concurrent asks for a missing key each prompt** (decider-key N1): `IKey.Asked` has no gate.
- **The build reads `.data/setting/data.sqlite` and is refused** (doc2): why a builder goal reads it is untraced. The builder bot's.
- **Ingi replaces the TypeSafe, OpenAI and GitHub keys** doc2 printed (windows, Finding H): "later".
- **Unused `app.KeepAlive` and `app.Services`** (snapshot inventory, `this.cs:289–291`): a removal pass; `app`'s members, so Ingi's.

### Code shape in files a branch may touch again (boy scout candidates)

- windows W5/X2: `goal/this.cs:184–205` (`FolderPath`/`Folder`, `FullPath`/`Address` stored twice); `goal/step/action/this.cs:116–132` (module/action string compares beside the `clause` subclass) and `:23,73` (a late stamp); `module/terminal/type/process/this.cs` (`_said`/`Heard`, `Lines` twice, `Write(string)`); `path/file/this.cs:137–154` (`Replace('\\','/')` ×3, `Canonicalize`/`Trimmed` statics); `permission/this.cs:55,74` (a string `Path`).
- json-lines X1, X2, C3 (`.bot/json-lines/auditor/final.md`): older smells in `type/item/list/this.cs` and `type/item/this.cs`; window's `Said`.
- bugs X1–X9 (`.bot/bugs/auditor/final.md`): duration/clipboard/input readers throw raw FormatException (`duration/serializer/Reader.cs:24,27`); containment copies outside `path.Holds` (`path/file/this.cs:132–140`, `build/code/Default.cs:39`); `Window.cs:203`'s stale comment; `module/remove.cs:16` a bare `Task<Data>`; `OsTrustTests`' `'/'`; stale "auto-wire" wording; docs `bans.md:35`, `good_to_know.md:110`, `filesystem-permission.md:71`; no test that a streamed ask goes live past saved answers.
- **`IsBound` in the doors that ask before making** (snapshot 254, 272): variable set/remove (`variable/list/this.cs`), type creation (`type/this.cs:275`).
- **The Linux view's message queue is unbounded** (security, bugs 32).
- **The image type's formats answering their own facts** (fast-apps 100), if a second fact needs it.

### The builder bot's

- doc2's build findings (json-lines): A (`call Early done=true` built as a name), B (two on-error clauses on one line fail StepsBroken), D (counts with `after each goal` + `before action output.write`, untraced), E (`ask the model …` built with the prompt as `Model`), F (`call tools/Shout` built as `Shout`), G (`drop the empty ones` built with no `Empty`), and `- make it better` building, then dying with NullReferenceException (the coder fixes the throw).
- The build's per-step `[✓]`/`[≡]` lines never print (`build-output.template`, `EmitBuildEvent.goal`).
- StepsBroken names a variable the step never says.
- The nothing-altered check misses a dict literal's keys (`build-checks.md`).
- A variable inside quoted text fails to build (`write out "decider key: %!llm.decider.setting.key%"`).
- **BareLiteralJudgedByForm** (the baseline's one plang fail; windows): no teaching edit gets it green. Ingi's question: a deterministic rule (each candidate type's strict parse judges a quoted literal) or drop the claim from the test and `set.notes.md:2`, `set.examples.md:7–8`.
- Debugging a system goal shows only that goal (windows 13): debug follows calls down when the builder bot needs it.

### Tests, tooling, runs

- **Done plans' tests move into the concept tree** (CLAUDE.md "Running plang Tests"): `test/plan/` still holds app-systems, bugs, decider-key, fast-apps, item-doors, json-lines, kind-is-type, snapshot, windows (and list-query, task). The builder rebuilds the moved `.pr`s.
- **The decision guard** (doc2): `scripts/verify-decisions.sh` fails `variable-ref-binds-instance` (it greps a moved file); `core.hooksPath` is unset so `.githooks/pre-commit` never runs. The architect's.
- **The Windows runs** (item-doors 8, bugs, windows): LinkSpotTests, StoresFolderTests, RealPathGateReCheckProbeTests, the sub app containment probes on a case-blind disk, APatchBeforeTheFirstFrame, a stores probe for a case variant (`.DATA\setting`). No full Windows sweep was run at windows' final tip.
- **The real window** (host): waits on Ingi's yes to touch data.vhd; the window's keyboard is Ingi's.
- **text-culture** (windows): numbers, held (IDecode handed its kind, or another route, plus a base Clr line).
- **Debug builds unchecked, Release checked** (`PLang.csproj:38–39`, bugs B1): whether Debug checks too is Ingi's.
- fast-apps N13 (`KindLookupTests` reads `_gate` by reflection), N15 (the bench's request and held rows at a new tip), N16 (a future webserver module needs `Microsoft.AspNetCore.App` back).
- `ProbeAnswerer.goal` (`test/app/permission/grantastorespersisted/`) saves its question into the source tree: move the write out, with the builder.
- `Documentation/Runtime2/todos.md`: plang-docs' triage (done and stale entries go, open ones grouped by theme) — *unverified* whether it ran.

## To discuss with Ingi

- **A new C# project** (item-doors, 2026-10-23: "I actually want to discuss new project, but that can be later"): the ABP idea (`app.snapshot.start() { .. }`), handlers as blocks with carriers resolved before Start, what fast-apps learned about a birth.
- **"get all files in /temp modified in the last 5 minutes"** builds to the right execution (held 2026-10-23): `modified` on a file path beside `size`, and datetime owning `Subtract(duration)` (or `age` on a path) rather than a parse that reads the clock; with it, `list.query`'s Where as separate actions (`list.where`, `list.order`, …). The build half is the decider's pick list.

## Brainstormed, not designed (item-doors 318)

- **Diff: a save writes only what changed**: a `patch` (pieces of `start`, `bytes`, plus `length`); the step chains `text.diff(…); file.save(Value=%!data%)`. Open: the patch's own write into a file.
- **An option that changes a value is its own action** (`file.read`'s `Template` → `template.fill(Value=%!data%)`); the same for diff, compress, encrypt.
- **Required carriers resolved before Start; handlers as a block** (`Resolve` answers the first that fails; optional ones stay lazy). Locked `data`: Ingi's yes and a re-pin.

## Future (Ingi said "future"; never offered as next work)

- A database module (2026-10-26: "3 future").
- **System events that run but print nothing** (event-action 15, 2026-10-29: "event.setting.system.output = false … or am I extending to much now and we can just have it future feature?"): with `event.setting.system` on, a second setting keeps the system events' output quiet.
- **`app.task.list`: every running task, with events and a look inside** (2026-10-29: "I want to add something as future feature, app.task.list, so everytime a goal runs with 'dont wait', the task is added … we should be able to set event on the add, both when it´s added and when done … reach into that task and ask it questions, get what has been written to the channel … maybe the task.list should be per context and app.task.list just groups all the actors together?").
  - **What exists:** "don't wait" builds to `goal.call`'s `Parallel=true` (`call.notes.md:4`), which answers a `task` at once. Each actor already lists its running tasks and drops each one when it ends: `actor.Task`, `%!app.actor.current.task%` (`task/list/this.cs`). A task answers `Id`, `Ended`, `Goal`, `Wait`, `Cancel`.
  - **New, 1:** `%!app.task.list%` reads every actor's running tasks through the actors' own lists. It is a view, nothing stored twice. The list stays per actor, not per context: the actor owns the cancellation the task is linked to.
  - **New, 2, events:** a task is an item, so "added" and "done" are its start's before and after: `%!app.type.task.on.start%` for each task (after: `%!event!data%` is its result), and one task by its path. No new moment is needed. The task list's `Start` then goes through the event door, as a goal's does.
  - **New, 3, the context:** a task answers what it runs in: its actor, its goal, and the frame that started it (`module/goal/call.cs:85`, `from`).
  - **Open, what it wrote, and asking it:** a task is not a new app. It runs in a child context on the same actor (`module/goal/call.cs:94–99`), so it writes to its caller's channels. Reading what one task wrote, or asking it a question, needs the task to have channels of its own (written to its own buffer and passed on, or an input the task reads). That is the design question for when this is picked up.

- **`app.jit`: compile code at run and start it** (Ingi, 2026-10-29: "app.jit, where we can insert c# code to run it (and compile it), that jit will be first c#, but who says it needs to be, so if we can generate some great code, send it to the jit to run, basically 'var data = app.jit.start(code);'"). Ties to the call-cost analysis (Speed): a goal the generator turns into C# is started through it.
  - **Today:** the `code` module loads compiled DLLs (`load code 'x.dll'`, `module/code/load.cs`) and registers providers. Nothing compiles at run, and the runtime holds no compiler.
  - **Shape (my suggestion):** code is a value whose language is its kind (C# first), and the value starts itself: compiled on its first start (lazy, pattern 11), kept by its source's hash, the same answer every app gets. `app.jit` is then the kept compiles, not a second door. A module owning it (what runs, runs in a module) keeps it off `app`'s locked members unless Ingi wants `app.jit` itself.
  - **Security:** compiled code has a DLL's power. The compile runs plang's own analyzers at error (PLNG002 no System.IO, no Console), so what it runs goes through path's verbs and AuthGate like any action. Code from a goal or an llm is the actor's to allow; code plang generates is plang's.
  - **Memory:** each compile is an assembly; it loads collectible and unloads with its app (the same need as "Sub apps own their code" (b)). Roslyn (`Microsoft.CodeAnalysis.CSharp`, large) loads only on the first compile.

## Very far backlog (never offered as next work)

Ingi, 2026-10-25: "very far into future (this is not something you should suggest taking when I ask)".

- Metadata built into plang (MetadataExtractor; `%photo.metadata.…%`, read lazily; owner image or content reference; security reads it first).
- Readers for formats Skia can't read, each a kind of image, added when a step needs one: TIFF, PDF pages, animated GIF writing, camera RAW, HEIC; Magick.NET under packages 6's four rules.
