# codeanalyzer — snapshot, 35c1b3377 (the snapshot is one write)

Scope: the 34 production files under `PLang/` that the commit touched, read whole where they changed (diff 35c1b3377~1..35c1b3377), against the OBP patterns and smells. Decisions 183–200 are taken as ruled (the app writes itself in the snapshot view; `Key` on item with `new` on dict and error; `row`; a restore binds without events; the error's callback stands at its frame): not re-raised.

## Bench (parts: birth, request, ptest, sweep; `BENCH_REST=300`; no OpenAI)
Row in `Tools/Bench/record.md` (35c1b3377, **load 9.08 at the start, 8–14 during**; the byte columns are load-proof, the time columns are not).

| Measure | runtime2 row d72538402 | 35c1b3377 | Band | |
|---|---|---|---|---|
| Sub birth KB | 109 | 115 | ±5 | **+6, just outside** |
| Root birth KB | 2387 | 2308 | ±25 | under |
| Sub request KB | 542 | 537 | ±10 | in |
| Root request KB | 3841 | 3563 | ±120 | under |
| Held MB (root) 0/1k/2k/3k/rest | 0.1→1.9→1.9→1.9 | 0.1→1.9→1.9→1.9→1.1 | flat | flat, no leak |
| Held MB (sub) | 1.6→2.1→2.1→2.1 | 1.6→2.0→2.0→2.0→1.8 | flat | flat |
| Times (sub/root birth, request) | | 0.15 / 19.7 ms, 2.7 / 19.3 ms | | void (load 9+) |
| `plang --test` | | 299 pass, 2 fail | | first run used a binary not yet rebuilt; after `./dev.sh build`: **307 pass, 0 fail** |
| C# sweep | | red: none | | pass (time void at load) |

Outside its band: **sub birth +6 KB** (band ±5, 109 base → 115). Low, tiny; the root path is under base, so it is the sub's. Not blocking by itself; the likely cause is the sub app now holding `Code`'s three `_*Saved` fields plus the new members' tagged tables. No held-memory growth. The times need a quiet re-run (`LOAD_MAX=3`) if the architect wants them.

## Findings, by pattern

### Pattern 2 — one class by case
**P2-1 `snapshot/this.cs`: one class, two cases (`_app` xor `_written`), and the taken case is unreachable after its write.** `IsTruthy` (:534), `Clone` (:531), `Write` (:538–558) branch on which field is set; `this.Variables.cs` `Get` (:13) and `Set` (:25) answer `NotFound` / `SnapshotTaken` when `_written` is null. But a taken snapshot lives only inside `wire.Of(new snapshot.@this(this, context), …)` (`app/this.Snapshot.cs:30`): `Of` writes it and returns the wire; nothing holds the `_app` form. So `Get`, `Set`, `Clone`, `IsTruthy` on the `_app` case are dead, and `SnapshotTaken` is in no test or goal (deletion test: delete the `_app` branches of those four and nothing fails). Fix: the taken form is a writer-only wrapper (a private nested type that writes `{"app": app}`), and `snapshot.@this` holds only `_written`; or `Snapshot()` returns the wire `Of` the app members and the class is born only from bytes.

### Pattern 3 — a choice belongs on the element
**P3-1 `View.cs` snapshot `Member` (:58–69): a five-clause type test in the view.** `declared != value.GetType() && IsAssignableFrom(declared) && declared.IsInstanceOfType(value) && Chooses(declared) && Registered(...) == null` decides whether a member is written with its type. The view asks the member's CLR types what the value should say about itself. It is read back by three more type tests: `View.Load` (`Row(member) != null ? Walk : …` :270), `Walk` (:289), `reflection.Made` (`clr != member && member.IsAssignableFrom(clr)` :954) and the new unread-source branch in the *shared* `View.Make` (:143–146). One polymorphic member (a frame's caller is a goal/step/action frame) costs four sites. Root: the member's value should always carry its own type when it is not the declared one, decided once on the writer's side by the value (`item.Output` knows its own class), with the reader's `Made` the only reader. Pattern 4.5 tell #1/#2: many branches for one trigger (a frame below its caller).

**P3-2 `View.Make` (:143–146) is a fork in a view shared by Store/Out/Debug.** The new first branch reads an unread `source` through `unread.Read(context)` before the reader lookup, beside the registered-reader path. It changes `Make` for every view, not just the snapshot, to serve one member (a frame's caller). Two paths for one operation ("new beside old"); belongs in the snapshot's own `Make` override, or in the reader, not in the base.

### Pattern 5 — a birth fact, not a search
**P5-1 `call/list/this.cs:106–110` + `this.Snapshot.cs:9, 58, 61, 66–73`: `Current` has a getter and setter on different fields, and the resume is held in two fields read through `??`.** `get => _current.Value; private set => _resumed = value` — reading back what was set gives something else; the setter exists as a door for the reflection read (`SetValue`). Then `_resumed` (set by the read) becomes `_entered` (by `Take()`), and `Resumed => (_resumed ?? _entered?.Stopped)?.Chain[^1]` is a `new ?? old` over the two states. One resume state with one name and a setter that does what its name says (a `Resume`/`Held` member) removes the `??`.

### Pattern 7 — one door
**P7-1 split lifecycle: the read is not one door; two objects need a second call after it.** `snapshot/this.Resume.cs:24–25` reads the app (`Read`), then calls `app.Code.Take(run)` by hand; `call.list.Resume` calls `Take()` first. `module/code/this.Snapshot.cs` stores the read in `_dllSaved/_providerSaved/_defaultSaved` (setters at :217, :252, :295) and applies them in `Take` (:310). That is "allocate here / hold there / apply elsewhere" (split lifecycle) for two classes, and the three `_xSaved` names are glued (`saved.dll`). It is a stated decision (the DLL load is async), but the cost is a caller who must know which objects need a `Take`: add a third such object and Resume has a third hand call. Ask the reader for the list of what holds a pending read, or make `Take` the item base's second phase.

**P7-2 the reader recipe `((reflection.@this)context.App.type.list["item"].kind["*"]!).Read(...)` is now written at `snapshot/this.cs:62`, `call/this.cs:186`, `app/this.cs:449`, `dict/this.cs:368`.** A CLR cast and `!` at four sites to reach one reader; `kind["*"]` should answer a typed `reflection` (or the `ReadContext` should carry it). Same family as the six `ctx.View.Reader(...)` recipe already on Ingi's list.

### Pattern 9 — a reader refuses another type / silent inconsistency
**P9-1 order of declaration is behavior.** `actor/this.cs` (`on` "declared before the context") and `reflection.Read` ("in turn and in the order declared"): a resume works only because a property sits above another in the source file, and `Type.GetProperties` order is not guaranteed by the language (it is stable on CoreCLR today). Moving `on` below `Context`, or a partial class splitting them, breaks resume silently. A pin test exists (decision 185 names CallStackEventTests), but the fix should be explicit: the owner reads its held members in a stated order (a small `Order` the tags carry, or `Context` reading `on` itself), not source position.

**P9-2 `snapshot/this.Variables.cs:13–19` `Get` writes.** A read replaces the held entry with its opened dict (`written.Set(...)`), so a `%snap.app…%` read mutates the snapshot (and its text on the next write is the opened form, not the signed bytes). Documented ("opened once, kept open"), and the signed bytes are the original `Raw` until opened, so only an *edited* snapshot is affected; but a plain navigation changes what `Write` relays. Low: do the open in the navigation door once at read-in, or keep both (`wire` and opened) so `Write` relays the bytes whenever unedited.

### Pattern — locked core (checked, not a code finding)
- `item.Key` is a new internal virtual on `item` (approved: 195 "yes", 197 name); `View.Member` is a new internal virtual on `View`, and `[Snapshot]` is added to ~20 locked members (`app.Code`, `app.actor`, `node.list`, `call.Current/Caller/Error/Parameter/Tags/property`, `variable.list.row`). `View.Member` is **not** named in decisions 183–200 that I read; ask that it is on the Ingi-approved list (a virtual on the serializer's `View`). The golden carries the re-pin; its commit message names 183/187/190/191/195/197/198/200.
- `code.default` and `code.registration` types are deleted (plang-visible type names in the golden): not an addition, but a surface change; confirm 191 covers it.

### Simplifications / readability (low)
- `app/this.Snapshot.cs:28` the loop variable `actor` hides the member `this.actor`, so the list is spelled `this.actor.list` beside a local `actor`; the check over every actor's `call.Unsaved` belongs on `actor.list` (it owns the actors).
- `snapshot/this.cs:63` and `:71` `held.Peek()!` null-forgives then `Make`s; the null case returns "reads as a nothing" one line later. One test, not `!` + test.
- `module/code/this.Snapshot.cs:215`: `dll`'s getter builds a new list over the live `_dll` on every read (a copy per `Output`); fine for a rare write, but a getter that allocates is a property that lies as a member.
- `View.cs` comment `// a birth that declines says why…` is untouched context, fine. No history/task comments added by this commit.

### Deletion test
Not mutated (the bench held the machine and a rebuild would void it). By reading: the `_app` Get/Set/Clone/IsTruthy branches (P2-1) and `SnapshotTaken` have no caller and no test; the `Row(member)`-walk and `Made` own-type branch are the only readers of the `Member` own-type write, and `ResumeInAnAppTests` + `SnapshotWireTests` reach them (frame below caller). Worth one announced mutation on the next pass: delete P3-1's own-type branch and see which of `SnapshotResumeTests` goes red.

## Verdict
**FAIL, coder next** (architect's call on the ranking): no blocker in behavior (sweep 0 red, `plang --test` 0 red after rebuild, memory flat, bytes under base but sub birth +6 KB). Findings that change structure: P2-1 (dead second case, one class), P3-1 and P3-2 (the own-type write is four type tests, one in the shared `View.Make`), P5-1 (`Current`'s asymmetric setter and the `??`), P7-1 (the second hand call). P9-1, P9-2 and P7-2 can ride Ingi's list.

## Recheck at ab239e127 (HEAD, after add1a0e4c) — quiet bench, P2-1, sub birth
**Bench** (row `snapshot-quiet-HEAD`, load 2.61, no rest-skip, `BENCH_REST=300`): sub birth 0.15 ms, **115 KB**; root birth 18.89 ms, 2309 KB; sub request 1.95 ms, 537 KB; root request 23.13 ms, 3555 KB; held root 0.1→1.9→1.9→1.9→1.1, sub 1.6→2.0→2.0→2.0→1.8 (flat); `plang --test` 4.7 s, 301 pass, 0 fail; sweep 43.2 s, red none. Against the runtime2 row (d72538402) and runtime2's tip b283811f6 re-measured now (sub birth 109 KB, three runs): **outside band: sub birth +6 KB (band ±5); root request 23.13 ms vs 21.81 (band ±1 ms, +1.3)**, the base row's own root request is high against the 17–18 ms the band was set on, so the time is noise-adjacent; nothing else outside. Bytes: root birth and both request columns are under base.

**P2-1 at HEAD:** fixed. `snapshot/this.cs` holds one `_app`; `Clone`, `IsTruthy`, `Write` no longer branch. Nothing dead remains in the class. One thing untested: `Set`'s refusal `SnapshotHoldsTheApp` (`this.Variables.cs:27`) is reachable (`set %snap.x% = …`) but no test or goal names it (deletion test: deleting the refusal fails nothing). A pin for `set %snap.other% = 1` is the coder's.

**Where the sub-birth bytes go:** bisected by the Bench birth tool (3 runs each, same load): runtime2 tip b283811f6 109 KB; 23d61537b-era 107 KB; `9d69072d4` 107 KB; **`9e7966cb5` 115 KB** (coder: a resume goes on in an app of its own, snapshot 160/166–173/177); 4ad31c7be 115; 35c1b3377 115. So the +6 KB is not the one-write commit, it arrived with `9e7966cb5`. That commit changes what a sub app's birth does in: `module/code/this.cs` (the registry is a layer over its maker's: `Get<T>` now goes through `Default()` and `Find()`, each a `Table` access that makes the layer's own `table` (four dictionaries) and walks up through `_under`), `origin` (`Code`, `Depth` on the interface, `Depth > MaxDepth` check at app birth) and `app/this.cs` (`Depth`). A sub app's birth calls `Code.Get<IIdentity>` (`actor/this.cs:164`) and `Code.Get<IStore>` (`this.cs:383`): the layered `Get` is where the allocation is most likely (a layer table plus the `Default → Find` walk), not the `Depth` int. Not isolated to a line: no allocation-by-type probe was run (the Bench tool counts totals). If the architect wants it exact, the next step is one announced throwaway probe around `Code.Get<IIdentity>()` on a sub app at 9d69072d4 and 9e7966cb5.

## Sub birth +6 KB: named (snapshot 211) — a bench artifact, not a birth cost
Probe in `Tools/Bench/Program.cs` (modes `alloc`, `steps`, and `BENCH_NOTEST=1`); nothing in production touched.
- Birth with no `Testing()` (the app as a program makes it): **85 KB at 9d69072d4, 9e7966cb5, 1314cd233 and 4da6a1e58**. Root 2285–2302 KB at all. So no commit on the branch changed what a sub app's birth allocates.
- The bench's birth calls `app.Testing()` (the test session and the signing mock) inside the measured loop. Step-by-step (`steps`, this thread's bytes per birth): `new app` 85,087 → 85,055 → 84,835 B (flat); **`Register ISigning` 6,896 → 9,380 B and `Register IKey` 1,256 → 7,050 B** (+8.2 KB); `SetDefault` ×2 +0.2 KB; dispose −0.3 KB; the rest equal. That is the +6 KB.
- Cause (`module/code/this.cs:235`): `Put` now asks `_under?.Find(providerType, provider.Name)` first, and the bench's maker (the server app) was itself `Testing()`'d, so it already holds `test-signing`: the sub app's `Register` is refused and builds a `ProviderExists` `ActionError` (`:227–228`), which `Testing()` discards. The error is what allocates (2.5 and 5.8 KB). Same cause before and after the lazy layer (940a66b42) and the bottom table (8e0a4ee9d): it is a write, so the table is made on it by design.
- What a fix takes away: nothing in the runtime. It is the tool: a sub app of a maker that already has the mock should not register it again (`Testing()` skips a provider the registry holds, or the bench's child birth is measured without `Testing()`, 85 KB). Or accept the error: a refused register that nobody reads is a test-helper cost. I left the script as it is (the row stays comparable to the base rows); `BENCH_NOTEST=1` gives the program's own figure.
- Side note, low: `AppTesting.TestSigning` ignores the two refused `Register` results (a sub app of a maker with the mock silently uses the maker's), by design now.

## Birth at 4da6a1e58 (bottom registry's table made in its constructor), quiet (load 2–3)
Row `snapshot-4da6a1e58-birth`: sub birth 0.17 ms / **115 KB** (with `Testing()`; 85 KB without), root birth 19.03 ms / **2309 KB** (2292 KB without `Testing()`). Root: band ±25 around base 2387, **in band** (and unchanged from ab239e127's 2309, so the table in the constructor costs the root nothing measurable). Sub: the 115 is the artifact above; 85 KB is the program's.
