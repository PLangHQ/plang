# Duplicate Execution Paths — Audit

## Why

`Data.Normalize` was found writing the `type` field itself instead of asking the type to render itself — a value bypassing the "objects are responsible for themselves" rule. That single bypass is fixed, but it raised a sharper question: how many *other* places implement the same logical operation through their own independent path. The suspicion was that `.pr` files in particular get read and materialized from several directions, and that serialization has more than one writer. This audit answers that across the whole tree.

The finding: yes, several. They cluster into five themes. The strongest — the `.pr` → Goal load fork — was surfaced independently by three separate sweeps, which is the confidence signal that it is real and not an artifact of one reading.

## Method

Five parallel read-only sweeps, one per axis where "same thing, two paths" hides: PR/goal loading, serialization/wire rendering, Data/value construction + type resolution, filesystem path resolution, and goal/action execution dispatch. Each sweep mapped the canonical path, then hunted for independent re-implementations of it. Line numbers were captured against the `context-never-null` tree (this branch's base, `b87965d44`) and spot-verified; they may drift a line or two as the tree moves.

## Scoreboard

| Axis | Verdict |
|---|---|
| `.pr` → Goal load + wire | **Forked 3×** — divergent flags, divergent registration |
| goal-name → `.pr` resolution | **Forked 2×** — two resolvers, one effectively dead-in-prod |
| `.pr` read orchestration | **Forked 2×** — eager `ReadText` vs lazy file-channel |
| value rendering (below the Data envelope) | **Forked** — 14 live STJ twins + vestigial renderer table + the same write-type-directly smell |
| CLR↔PLang type name | **Forked 2×** — instance vs static, already diverged |
| MIME → type | **Forked 2×** — two tables that disagree |
| resume loops (`RunFrom`) | **Forked** — copy of `RunAsync` that drops behavior |
| `app.pr` read/write | **Forked 3×** — Goal, raw JsonDocument, anonymous serialize |
| PrPath derivation | **Forked 3×** — hand-written in three places |
| Data envelope serialization | **Clean** — single-sourced through `data.Output` |
| action / module dispatch | **Clean** — single door (`app.Module.GetCodeGenerated`) |
| `.goal` source parse | **Clean** — single `Goal.Parse` |
| byte-level filesystem reads | **Clean** — every read through `FilePath` verbs + `AuthGate` |

---

## 1. `.pr` → Goal is loaded and wired three different ways

Three routines read a `.pr` and turn it into a wired Goal. Each sets a *different* set of flags and disagrees on whether the result is registered. This is OBP smell #4 — one logical operation's lifecycle split across three files — and the three have already drifted apart.

| Site | Reads via | Wires | Registers? |
|---|---|---|---|
| `goal/list/this.cs:364` `LoadFromFileAsync` | `prPath.ReadText()` | `step.Goal`, `action.Synthetic = false` | `Add()` ✓ |
| `goal/setup/this.cs:37` `DiscoverAsync` | `file.ReadText()` | `step.Goal`, `action.Synthetic = false` (inline copy) | `_goals.Add()` ✓ |
| `goal/GoalCall.cs:298` `LoadFromFile` | `module.file.Read` (channel) | `goal.App`, `action.StampTemplates()`, sub-goal back-refs, `goal.LoadedFromPrPath` | **no** |

They disagree on three axes that matter: registration (`Add` vs none), step wiring (`Synthetic = false` vs `StampTemplates`), and `LoadedFromPrPath` (set by GoalCall alone). `setup/this.cs:37` is not setup-specific work — its loop body (`ReadText` at `:59`, `Synthetic = false` at `:66`, `_goals.Add` at `:69`) is a line-for-line copy of `LoadFromFileAsync`. Only its discovery of the two candidate paths is setup's own concern.

### Relationship to the `remove-goalcall` branch — two gaps

The `remove-goalcall` plan folds GoalCall's loader into a single `app.Goal.Load`. Two things it does not account for:

- **`app.Goal` already carries a second, divergent loader.** `list/this.cs:126` `GetAsync` → `:175` `TryLoadPr` → `LoadFromFileAsync` resolves a goal name to a `.pr` through a *different* lookup (root `.build`, then `/system/*`) than GoalCall's four-tier walk (`GoalCall.cs:202` `GetGoalAsync`). The execution sweep found that `GetAsync` / `GetByPrPathAsync` / `LoadFromDirectoryAsync` have **zero production callers — only tests touch them**. So the node the plan wants to add `Load` to is already hosting an effectively-dead, divergent disk loader. `Load` must *replace* it, not sit beside it, or the duplication just relocates.
- **`setup.DiscoverAsync` is not mentioned.** It is an inline re-implementation of `LoadFromFileAsync`'s wire loop. It should call the canonical loader; only the candidate-path discovery stays.

---

## 2. The `Normalize` smell repeats one level down — in value rendering

The Data *envelope* is now single-sourced: every Data writes itself through `data.@this.Output` (`data/this.Normalize.cs`), delegating the value slot to the item's own `Output`. The STJ `Wire` converter is read-only (its `Write` throws). That layer is clean.

The duplication moved down into the **value slot** — and one of the duplicates does the exact thing that was just fixed in `Normalize`:

- **`channel/serializer/json/writer.cs:88`** — `JsonSerializer.Serialize(_writer, record.Type, _options)` writes the `type` field directly instead of letting the type render itself. Same bypass, different file. It sits on the vestigial sync `BeginRecord` path (`writer.cs:70`), but it is the literal smell.
- **14 per-type `Json.cs` STJ converters** — `type/{archive,binary,bool,choice,date,datetime,dict,duration,guid,list,null,number,text,time}/Json.cs`. Each re-renders its value a second way for the `application/json` channel (`channel/serializer/Json.cs:93` → `JsonSerializer.SerializeAsync(stream, value, value.GetType(), _options)` on `data.Peek()`, never touching `Output`). This is the "do NOT add a JsonConverter to a domain type" rule violated 14×. They are **live**, not dead. `json.Writer` with `emitsSchema:false` already produces the bare-json shape through `Output`, so `application/json` could drive `data.Output` and these could go — *except* internal round-trips (`dict/this.cs`, `catalog/Conversion.cs`, the eval diff) lean on them, so those sites must move onto `Output` in the same pass.
- **Already-diverged pair:** `error/serializer/Default.cs` (leaf renderer, Output path) vs `error/IError.Wire.cs` `ErrorWire : JsonConverter<IError>` (STJ, snapshot path) render the same error two ways and no longer agree on which fields cross the wire — `ErrorWire` emits `variables` / `params` / `details` / `permission`; the leaf renderer omits them. The drift has already happened.
- **Dead-but-driftable parallel writer:** `type/renderer/this.cs` `.Of()` has zero callers (verified), and `json.Writer._renderers` is stored but never read. Its write-side targets include `image/serializer/{Default,text,protobuf}.cs`, which duplicate line-for-line the text/base64/protobuf branches that actually run inline in `image/this.cs` `Write`. A whole second value-writer sits next to the live one; the `image` copy is a genuine logic twin that can drift. (The *read* half of these `serializer/Default.cs` files is live — only the write half + the table are vestigial.)

The unifying move: route `application/json` through `data.Output` with a non-schema writer, retire the `Json.cs` converters, delete the renderer table and the sync `BeginRecord` / value-switch apparatus.

---

## 3. CLR↔PLang type name and MIME→type each forked

- **`catalog/this.cs:359` `GetTypeName` (instance) vs `catalog/this.cs:129` `GetTypeNameStatic`** — ~60 lines of near-identical CLR→PLang-name logic maintained twice. The static copy exists only to dodge the per-App registry when there is no Context. They have already diverged: the instance version handles `ImmutableList` / `ConcurrentDictionary` / `ISet` families; the static one does not. The static copy also re-implements `Registry.InferName` inline.
- **Two MIME interpreters that disagree.** `catalog.ClrFromMime` (`catalog/this.cs`) says `image/png` → `byte[]`; `format.TypeFromMime` (`format/list/this.cs`) says `image/png` → `{binary, png}`. Same question, two hardcoded tables, two different answers.
- **Context-less static factories paralleling context-ful ones.** `data.@this.Ok` / `Null` / `NotFound` / `FromError` sit beside `context.Ok` / `Null` / `NotFound` / `Error`. Born-with-context is canonical; the statics are the legacy parallel — and `data.Ok(nonNullValue)` actually trips the "value cannot be born without a context" throw. 23 call sites still use the static `Ok`. (This one is directly in this branch's lineage — `context-never-null` is making born-with-context the invariant.)

Lower-priority construction forks the sweep noted: raw→item lifting happens via `type.Create` (canonical), the Data ctor's `json.Parse`-then-`type.Create` double-narrow, and `SetValueDirect`'s `new Clr(value)` which skips `type.Create` entirely.

---

## 4. Resume loops (`RunFrom`) are a copy of `RunAsync` that drops behavior

`goal/this.RunFrom.cs` and `goal/steps/step/this.RunFrom.cs` re-implement the step and action loops for snapshot resume. They are not just duplicated — they **omit** disabled-step skipping and condition `skipBelowIndent` that the canonical `Steps.RunAsync` / `Step.RunAsync` enforce. So a goal resumed mid-flight does not honor condition-gated sub-step skipping that a fresh run would. This is a latent divergence bug, not only a maintenance cost. Resume should be "start the same loop at an offset," not a parallel skeleton.

---

## 5. Smaller, same shape

- **`app.pr` materialized three incompatible ways.** `app.Load` resolves `/.build/app.pr`, calls `ReadText()` (which yields a *Goal*, so the identity fields come back empty), then falls back to `ReadBytes()` + `JsonDocument.Parse` to pull identity. `app.Save` writes a raw anonymous object through `JsonSerializer.Serialize` — never through goal serialization. Plus three independent `Resolve("/.build/app.pr")` sites (`app/this.cs` ×2, `module/builder/this.cs`) with no shared app-marker accessor.
- **PrPath derivation (`parent/.build/<stem>.pr`) hand-written three times** — `goal/this.cs` `PrPath` getter, `type/path/this.cs` `GoalCall` getter, and `goal/this.cs` `GetRuntimeDirectory` (the inverse). OBP smell #5 — one convention, three sites; one fix touches all three.
- **Exists-probe-then-read re-coded per subsystem.** `Resolve + ExistsAsync + (ReadText | LoadFromFileAsync)` appears separately in `list.TryLoadPr`, `list.GetByPrPathAsync`, `setup.Discover`, `goal.Methods.FormatForLlm`, and `builder.RunAsync`. No shared "load pr if present" helper.

---

## What is clean (don't go looking here)

- **Data envelope serialization** — single-sourced through `data.Output`; the STJ `Wire` converter is read-only.
- **Action / module dispatch** — everything funnels through `app.Module.GetCodeGenerated`. The execution sweep confirmed this is one door.
- **`.goal` source parsing** — one `Goal.Parse`; all callers route through it.
- **Byte-level filesystem reads** — every read goes through `FilePath` verbs + `AuthGate`; the System.IO ban holds outside the exempt `app/type/path/**` zone. (One nit inside the exempt zone: `ValidatePath` runs its own `System.IO.File.Exists` probe at resolve time, then the caller probes again with `ExistsAsync` — a double disk hit, not a security gap.)

## Stale docs found along the way

- `type/primitive/this.cs:14` — a `<see cref="MimeMap"/>` doc points at a property that no longer exists; `ClrFromMime` is now a hardcoded switch.
- `variable/IRawNameResolvable.cs` — prose describes an `AsT_Impl` / "%var% substitution branch in `As<T>`" that no longer exists; `As<T>` is a view and does not resolve.

---

## Cross-cutting read

There are two distinct shapes of duplication here, and they want different fixes.

**The goal/`.pr` cluster (#1, #4, #5)** is *entry-point sprawl*: many callers each resolve, read, wire, and run a goal their own way because there was never one owner of "load a goal from disk" and "run a goal." The `remove-goalcall` branch is already collapsing the *run* side into one door. The same consolidation should absorb the *load* side — but it has to replace the dead `app.Goal` loader and the inline `setup` copy, not leave them. Worth confirming the plan does.

**The serialization cluster (#2, part of #3)** is *the rule the `Normalize` fix just established, not yet applied below the envelope*. The envelope obeys "the object renders itself"; the value slot still has 14 hand-written renderers and a write-type-directly path. This is the natural next step after the `Normalize` fix and is the most direct continuation of what prompted this audit. It fits this branch's lineage (`context-never-null` is already binding serializers to context).

## Suggested next steps (not yet a plan)

- Fold the two `remove-goalcall` gaps (dead `app.Goal` loader replacement; `setup.DiscoverAsync` re-pointing) into that branch's plan before it ships, so the load fork collapses with the run fork.
- Treat value-rendering unification (#2) as its own plan: route `application/json` through `data.Output`, retire the per-type `Json.cs` twins and the vestigial renderer table, and reconcile the `error` renderer divergence — with the internal round-trip call sites moved onto `Output` in the same pass.
- The type-name / MIME forks (#3) and the resume-loop drop (#4) are smaller and can ride along or stand alone.
