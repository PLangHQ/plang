# get-builder-running — restore `plang build` end-to-end

**Branch off:** `module-discovery` (carries the two fixes already landed). **Merge back into:** `module-discovery` when `plang build` completes a real build.

## Goal

`plang build` is broken in a **chain** of pre-existing failures — each fix unmasks the next. Get it running end-to-end so the module-discovery Stage-4 work (the `.goal`→`.pr` rewire, 4e deletions, parity/sanity) can be validated live. This is build-infrastructure, **not** Stage-4 feature work — hence its own branch.

## Repro

```bash
cd Tests
BIN=../PlangConsole/bin/Debug/net10.0/plang
# clean rebuild first (stale-binary trap):
# rm -rf ../PlangConsole/bin ../PlangConsole/obj ../PLang/bin ../PLang/obj ... ; dotnet build ../PlangConsole
$BIN build '--build={"files":["BuilderSanity/AddItem.goal"],"cache":false}'
```
The `.pr` the runtime runs lives at `os/system/builder/.build/*.pr` (OsDirectory-redirected from `/system/builder/…`). Templates render live from disk; `.goal` edits need a rebuild to reach the `.pr` — which is exactly why this bootstrap chain matters.

## The chain — state

| # | Layer | Symptom | State |
|---|---|---|---|
| 1 | **CLI settings bind** | `--build={"files":[...]}` → `String cannot lower to this` at `setting/this.cs` | ✅ FIXED (`c13532536` on module-discovery) — `Build.Files` is now a plang `list<path>`; the walk stores it lazily, the consumer lifts each row via `row.Value<path>()` |
| 2 | **Bootstrap NRE** | `NullReferenceException` at `this.cs:566` (`goalResult.Value() as clr<Goal>`!) | ✅ FIXED (`a1908911f` on module-discovery) — `GoalCall.LoadFromFile` (the PrPath path) now rides back as `clr<goal>`, matching the in-memory `Found()`; the kind-redesign (`4cb19476b`) had updated the dispatcher + memory path but not this one |
| 3 | **`builder` channel not found** | `Channel 'builder' not found` at `EmitBuildEvent.goal:12` (`write out %msg% channel: "builder"`) | ❌ **NEXT — start here** |
| 4+ | unknown | — | each fix likely unmasks the next; expect more |

## Layer 3 — findings so far (the current front)

`Build.goal:9` sets the channel up **before** `EmitBuildEvent` runs:
```
- set channel "builder" call BuilderChannel        # Build.goal:9
- call EmitBuildEvent kind="build-path", …          # Build.goal:11  ← reached (trace confirms)
```
- The `.pr` is CORRECT — `build.pr` has `channel.set(Name="builder", Goal={name:"BuilderChannel", prPath:"/system/builder/.build/builderchannel.pr"})`. Not a stale-`.pr` problem.
- `channel.set` (Build.goal:9) **runs** (the build reaches `Build.goal:11`, past `:9` — so `:9` didn't error).
- Yet `EmitBuildEvent`'s `write out … channel:"builder"` → `ChannelNotFound`. `output.write` for a named channel uses `Channels.Resolve(name)` (returns null on miss → `ChannelNotFound`), NOT the no-op `Channel(name)` fallback.

**Hypotheses to check (unverified):**
1. `channel.set` (`app/module/action/channel/set.cs`) registers on a **different actor/context** than `EmitBuildEvent` reads. Build runs under `User.Context` (`build/this.cs:111`); check where `channel.set` registers vs where `output.write` resolves.
2. The registered `builder` channel is a **goal channel that's `IsExecuting`** — `Resolve` (`channel/list/this.cs:110`) returns `null` for a goal channel with `IsExecuting == true`. A goal-backed channel writing to itself / re-entrancy could trip this.
3. `BuilderChannel.pr` fails to load (another stale/missing system `.pr`), so `channel.set`'s goal binding is empty — but `:9` didn't error, so probably not.

### ROOT-CAUSED (2026-07-17): channel name resolves to the UNPARSED raw `"builder"` (quotes included)

The exact error is `Channel '"builder"' not found` — the lookup name carries the JSON quotes.
Not IsExecuting, not a wrong actor, not stale `.pr`:
- `.pr` param is clean: `emitbuildevent.pr` step[1] action[0] param[1] = `{name:"channel", type:{name:"text"}, value:"builder"}`.
- `channel.set` registers `builder` (clean — `Name.Value().Clr<string>()`).
- BUT the generated IChannel resolution (`app.module.action.output.Write.Action.g.cs:57`, emitted from
  `PLang.Generators/Emission/Action/this.cs:287`) reads the name via **`param.Peek()?.ToString()`**.
  `Peek()` returns the lazy source's RAW form — the wire slice `"builder"` WITH quotes, never parsed —
  so `Channel.Resolve("\"builder\"")` (line 58) misses the registered `builder` → ChannelNotFound.

**Candidate fix:** the generated `Resolve(...)` is **async** (`Task<(ICodeGenerated?, IError?)>`), so read the
PARSED value: `(await param.Value())?.ToString()` instead of `.Peek()?.ToString()`. VERIFY first that
`text.Value()` on this source yields clean `builder` (strips the wire quotes) — write a focused test that
loads `emitbuildevent.pr` and inspects the channel param's `Peek()` vs `await Value()`. If `Value()` is
clean, change the generator emission (netstandard2.0 — regenerate) at
`PLang.Generators/Emission/Action/this.cs:287`. Then rebuild + re-run the repro.

**Why it regressed:** the `Peek()` (raw, sync) approach assumed the param's raw IS the clean string; the
born-native/lazy-source wire model makes `Peek()` the unparsed JSON slice. A general IChannel-named-write
concern (any `write out %x% channel: "foo"`), not build-specific — check whether named-channel writes pass
anywhere in the suite to gauge blast radius.

(Earlier hypotheses — different actor, goal-channel IsExecuting, BuilderChannel.pr load — all traced and
RULED OUT: registration is on the same User actor, the first write can't be IsExecuting, and channel.set
reached `Build.goal:11` without erroring.)

## Discipline (do NOT skip)

- **Baseline = revert + `dev.sh build` + run N×.** `git stash` on a clean/committed tree stashes NOTHING; the test-runner's incremental build may not propagate a production-source revert. One run each side is not a baseline. (See `memory/feedback_baseline_rebuild_discipline.md`.)
- **Clean-rebuild before any `plang --test`/`plang build`** (stale-binary trap).
- Fix at the CAUSE, in C# where possible (runtime takes effect immediately; `.goal` edits need a `.pr` rebuild that this very chain blocks).

## Done =

`plang build '--build={"files":["BuilderSanity/AddItem.goal"]}'` runs to completion (writes a `.pr`, no crash), then merge back into `module-discovery`.
