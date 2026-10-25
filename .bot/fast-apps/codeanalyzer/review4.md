# fast-apps: fourth review (decisions 81–85, stage 6, tip 02aff152e) and the bench row

Scope: the 23 production `.cs` files and `PLang.csproj` changed since review3 (f4f754455..02aff152e), each read whole by one reader against review3's findings, plus the stage 6 package check. The other touched files were read whole in review3 and are carried; none of them has a changed hunk. Read only. I did not run tests or `Tools/ObpScan`; the bench built the tree, which is the build evidence. I re-grepped the namespace check and the `Add(kind)` order (verified); the rest is the reader's and may be a few lines off.

**Verdict: NEEDS WORK (FAIL, coder next) — small.** Both of review3's blocking findings are fixed at the root. Nothing new blocks by the branch's rules. One cost to settle (the root birth's +110 KB, a likely cause named below) and one number I could not settle (the clean build, measured only at load 13–16). The rest are small, non-blocking.

## review3's findings, one by one

| review3 | Verdict | Where |
|---|---|---|
| Blocking 1: reader `Shipped` null | Fixed at the root | `reader/this.cs` `private protected abstract shipped Shipped { get; }`; `reader/code.cs` holds its own `Lazy<shipped>`; `reader/layer.cs` answers `Shipped { get; } = new()`; `Find`/`Payload`/`Token`/`Owner` read `Shipped.Payloads` without `?.`; no other null or hidden protocol in `code.cs`/`layer.cs`. The layer's empty `shipped` allocates two dictionaries per layer: ruled (53) |
| Blocking 2: overflow check after the frame | Fixed at the root | `call/list/this.cs:218-221` `Deepen()` throws before `new call.@this(…)` in the three deepening `Push` overloads (`:185,204,212`); `binding` (`:192`) and `Isolate` (`:199`) unchecked as before; `MaxDepth` read once; `Enter` no longer throws, so no frame is built on the throw path and no diff scope leaks |
| 3 `GoalNotBuilt` at one door | Fixed | `goal/list/this.cs:52` `Fresh ?? Inherited ?? Built ?? Read`, `Built` (`:45-48`) the one check; crefs read `PrPath`; the `is file` test stays (`ILoad`'s `path` parameter forces it) |
| 4 one `Find` walk | Mostly | `variable/list/this.cs:457-468` `Peek` and `Get` through one `Find`; `Contains` (`:343-348`) still writes the chain itself because it must not make a system variable |
| 5 stamp, one way to be "not from a file" | Fixed, residue (S5) | `IsFresh(context) => Stamp.IsFresh(context)` (`goal/this.cs:381`); kinds `none`, `unknown`, `stat(Pr, Stat)` take no unused parameters |
| 6 `Registry.Add(kind)` via `Hold` + `Guard` | Fixed, with a regression (N1) | `Registry.cs:225-230` |
| 7 index key invariant | Stated, not enforced | `goal/list/this.cs:341-345` says nothing changes a held goal's `Path` or `Parent`; `goal.Path` (`goal/this.cs:92`) and `Parent { get; set; }` (`:185`) are still public setters |
| 8 one `Drop()`, `Row(key)` | Fixed | `item/list/this.cs:223-227`; `Indexed` is `Row`; the index still answers `object?`, so the goal list casts with `as goal.@this` |
| 9 `Has<T>` makes nothing; table named `table` | Fixed, leftover | `module/code/this.cs:182-185` `Rows.Shipped.ContainsKey(typeof(T)) || Providers.TryGetValue(…)`; the class is `table` but the field is still `_rows`, the property `Rows`, the parameters `rows` (`Put`, `Ship`, `Ship<T>`) |
| 10 names | Partly | `Subclass.Type`, `Indexed` → `Row` done; `event/this.cs` `Outward`/`Inward` not in this round |
| 11 `system.cs` `Row` allocates nothing | Fixed | a plain `for` loop (`system.cs:441-446`); the row is made through `EnsureInitialized(ref made[row], …)`, the lambda built only when the slot is empty |
| 12 `origin.Testing` (82–84) | Fixed, one answer | `origin/this.cs` `bool Testing`; `code` answers `false`; the actor answers `App.Mode.Value == Mode.Test` (`actor/this.cs:192`); `app.Mode` reads `test.list.Session != null \|\| origin.Testing` (`app/this.cs:241`); derived with no stored flag, read at one door; internal, no locked addition |
| 13 setup through the list's door; no text for "none" | Fixed, behaviour change (N2) | `goal/setup/this.cs:56` `_goals.Read(file, context)`; `test.Failure(sb, context)` writes into a `StringBuilder`, so the `""` is gone |

## New findings, ranked (none blocking by the branch's rules)

1. **N1. `Registry.Add(kind)` now refuses after it has held, and runs a full `Guard()` per add.** `Registry.cs:229` `if ((Hold(kind) ?? Guard()) is { } refused) throw new AppException(refused);` (verified). The old shape checked the collision and threw before `Hold`. Now a clashing kind is already on its type's empty kind (`Put` → `root.Add`) when the exception fires, so a refused kind stays held and the next `Guard()` re-reports the same clash. `Add(assembly)` (`:219`) has the same order, but it is a load that fails whole; `Add(kind)` is a single add. The three path-scheme adds in `origin/code.cs` each run `Guard()`, which scans every type and kind per add, so a root birth pays three full scans. **The bench shows the cost:** root birth bytes are +110 KB over stage 5 (below), which is the likely cause; I did not prove it with a mutation. Fix: `Guard` takes the incoming kind and checks its `Mime`/`Extension` against the held ones before `Hold`, or `Add` undoes the hold when `Guard` refuses; the origin's three schemes go in as one `Add(assembly)`-style batch under one `Guard()`.
2. **N2. Setup now adds every `.pr` it reads where it added only setup goals.** `goal/setup/this.cs:56` calls `_goals.Read(file, context)`, and `Read` (`goal/list/this.cs:58`, now `internal`) does `Add(goal)`. The old code ended `if (… || !goal.IsSetup) continue; _goals.Add(goal);`. A non-setup goal in `/Setup.goal` now lands in the app's goal list though `Goals` still filters by `IsSetup`; `Read` is also reached from outside the list. Fix: check `IsSetup` on the read goal and drop it from the list, or state the new behaviour at the site.
3. **N3. `test.Failure(StringBuilder sb, context)`** returns `Task` and appends: a noun doing a verb's work (`WriteFailure`).
4. **N4. `Built` (`goal/list/this.cs:45`)** is a participle that returns the refusal or null, in a `??` chain where null means "not mine" (the same protocol as `Inherited`). Name it for the refusal (`Unbuilt`) or fold it into `Read`.
5. **N5. `stat` copies the pr the goal already holds.** `goal/this.cs:183` `stat(Pr, Stat)` carries the `.pr` that `goal.PrPath` also holds; if `Path` changes, the stamp keeps the old one (the comment forbids it, nothing prevents it). The property `Stat` sits inside a record named `stat`.
6. **N6. The snapshot writes its two lists twice.** `module/code/this.Snapshot.cs:36-37` (the early return for an unused registry) repeats `s.Write("registrations", …)` and `s.Write("defaultOverrides", …)` from the tail (`:74-75`): write once after the loop, skipping the loop when `_rows` is null.
7. **N7. Names.** `call/list/this.cs` `Deepen()` is a verb that only checks (`RefuseBeyondMaxDepth`), and each deepening `Push` must remember to call it first; a fourth deepening overload would silently skip the check. The `module/code` table: class `table`, property `Rows`, field `_rows`, parameters `rows`.
8. **N8. `goal.Stamp` allocates a `none` per goal** (`goal/this.cs:353`, `new stamp.none()` is stateless): one shared instance.

## Stage 6 and the packages

- **Removed namespaces:** no `using` or qualified use of the 56 removed packages' namespaces in `PLang`, `PlangConsole`, `PLang.Generators`, `PLang.Tests` or `Tools` (verified by grep; the bench's clean build compiled the tree, exit 0). `Microsoft.AspNetCore.WebUtilities` (`type/item/status/this.cs:35,93` `ReasonPhrases`; `PLang.csproj:75`) and `Nethereum.Util` (`crypto/code/Default.cs`, `keccak256`, a test; `:77`) are kept.
- **A hidden dependency:** `QRCoder-ImageSharp` has no `QRCoder` reference in any source; it looks kept as the transitive source of `SixLabors.ImageSharp`, which `image/this.cs` and the screen types use. The commit says "PLang references the packages its code names, nothing else", so this one is not by that rule: reference `SixLabors.ImageSharp` directly and drop `QRCoder-ImageSharp`, or say why it stays. `Microsoft.Extensions.FileProviders` and `Primitives`, used in `ui/code/Fluid.cs`, are not explicit references now and must come through `Fluid.Core`; the build confirms it works.
- **Used:** `DeepCloner`, `Fluid.Core`, `Microsoft.Data.Sqlite`, `NSec.Cryptography`, `System.Runtime.Caching` each have a source user. `Sdk.Web` → `Sdk`: the only `Microsoft.AspNetCore` use is `WebUtilities`, covered by its package. `PLang.Tests.Bench` is a new `InternalsVisibleTo` for the bench tool.

## The bench row (`Tools/Bench/run.sh` on d1fb4dd29 = 02aff152e's source, appended to `Tools/Bench/record.md`)

Load: the row's load is the highest the run saw, **16.25**; the gate waited its 20 minutes at times and went on, because other sessions were building. Read every number with that in mind. The row against stage5 (cc78ec913), bands from the top of `record.md`:

| Number | Stage 5 | Now | Band | Reading |
|---|---|---|---|---|
| Sub birth, ms | 0.15 | 0.14 | ±0.02 | in band |
| Sub birth, KB | 122–128 (128) | 110 | ±5 | **−12 KB, outside the band, lower** |
| Root birth, ms | 18.4–20.2 | 18.48 | ±1.5 | in band |
| Root birth, KB | 2,357–2,377 | **2,488** | ±25 | **+110 KB, outside the band, higher** |
| Sub request, ms / KB each | 2.0–2.3 / 603–614 | 1.85 / 541 | ±0.15 / ±10 | **lower on both** |
| Root request, ms / KB each | 17.2–18.3 / 3,636–3,858 | 16.56 / 3,720 | ±1 / ±120 | ms lower, KB in band |
| Held, sub (MB) 0/1k/2k/3k/rest | 2.4 → 3.0 → 3.0 → 3.0 → 2.6 | 1.6 → 2.1 → 2.1 → 2.1 → 1.9 | ±0.1; flat = last three within 0.2 | **flat**; rest lower |
| Held, root (MB) | 0.9 → 2.8 → 2.7 → 2.7 → 1.7 | 0.1 → 2.0 → 1.9 → 1.9 → 1.1 | same | **flat**; rest lower |
| `plang --test` | 4.7–5.6 s (298 pass, 1 fail) | 5.6 s (298 pass, 1 fail) | ±0.5 s | at the band's top edge, same counts |
| C# sweep | 40.6–54.7 s (red: Wire) | 37.3 s (red: Wire) | ±7 s | in band, same red names |
| Build, clean | 45.9 s at stage6-before (load 9.9); band 48.7–53.5 when quiet | 90.5 s (load 16.25); rerun **74.9 s** (load 13.66) | ±3 s quiet | **not settled** |
| Build, incremental | 11.5 s (stage6-before) | 79.4 s; rerun **9.1 s** | ±1.2 s | the 79.4 was a neighbour's; the rerun is in band |

- **Held memory is flat in both modes and ends lower than stage 5:** no leak, nothing to block on.
- **Root birth +110 KB (4.6%) is the one change I cannot call noise:** not explained by a branch decision as a goal, but decision 81 (review3 6) changed `Registry.Add(kind)` to hold then run `Guard()`, and the origin adds three path schemes at every root birth (N1): three full scans. I did not prove the cause (no mutation run; the machine was busy). Treat as a finding for the coder to confirm with a before/after on `Add(kind)`; it is not a leak and not the request numbers. I mark it non-blocking only because it is bytes per root birth, a cost the command line pays once; the coder should explain it or fix N1 and re-measure.
- **Sub birth −12 KB and the request apps lower** fit the stage 4–5 and review fixes (lazy tables, a layer's `Shipped`, no throwaway reader); no decision contradicts them.
- **The clean build is not settled.** Stage 6's purpose was a faster build and a smaller checkout (3.3 → 1.5 GB); the rows show 45.9 s (before, load 9.9), 65.4 s (packages, load 20.9), 87.9 s (after, load 15.8), 90.5 s and 74.9 s (mine, load 16 and 13.7), against a quiet band of 48.7–53.5 s. Every post-stage-6 row was taken under load 13–21, and the gate could not get under 3 in 20 minutes; clean-build time is the number most hurt by a neighbour. **Ask: run the build row once on a quiet machine** (`BENCH_SKIP="sweep ptest birth request" Tools/Bench/run.sh`, load under 3). If a quiet clean build is above about 60 s, stage 6 made it slower and the cause to chase is the test projects' `RuntimeIdentifier` restore (`PLang.Tests/Directory.Build.props`), not the packages. The incremental build, 9.1 s, is below its band: that part of stage 6 holds.

Two rows are appended: `d1fb4dd29 (review4-02aff152e)` (everything) and `d1fb4dd29 (review4-build-rerun)` (build only). Compare tool rows with tool rows (the stage rows came from the earlier bench).

## Checked, clean (explicit no)
- No static added in the changed files; no `caller == null` or `_parent` null fork; no flag read at many doors (`Testing` is read at one door).
- No lock held across an await in the changed hunks; the `goal.list` index is dropped or kept under the gate by `Change` and `Append`; `_reading` still gives one read in flight per `.pr`.
- Locked core: no new public or protected member on `app`, `item`, `type`, `type.list`, `kind`, `property`, `reader`, `IWriter`, `data`, `call` or `event`. `origin.Testing`, `goal.list.Read`, `stamp`, `Key`, `Row`, `Drop` are internal or private. `test.Failure` changes a public signature on `test`, which is not on the locked list.
- No history or task comments added; the old stacked comments in the changed files are gone (`action/this.cs` `[JsonIgnore]` is back on its member). `actor/setting/this.cs` has only a class comment change. `Coverage.Site(goal, step)` has no sentinel now.
- Older smells still standing are as listed in review3 (they were not in this round's scope of fixes); no new "older" ones surfaced in the changed files.

## For the coder, in order
1. N1: `Add(kind)` checks before it holds (or undoes), and the three path schemes are one batch; re-measure the root birth against 2,357–2,377 KB.
2. N2 (decide the behaviour), N3–N8 (names, the doubled snapshot writes, one shared `none`).
3. `QRCoder-ImageSharp`: reference what the code names, or say why it stays.
4. The index key invariant (7): a setter that cannot be called on a held goal, or the list drops its index on a key change.
5. A quiet-machine clean-build row before stage 6 is called done.
