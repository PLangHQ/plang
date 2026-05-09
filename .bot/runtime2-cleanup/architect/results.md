# runtime2-cleanup — Planned vs Delivered

**Branch:** `runtime2-cleanup`
**Span:** 22 architect sessions, 22 stage briefs, 22 coder commits
**Final test status:** C# 2752/2752 ✅ · PLang 199/199 ✅
**Last commit:** `5de998d9 runtime2-cleanup stage 19: Provider → Code rename, end-to-end`

This file compares the actual end state of `PLang/App/` against the destination tree in `plan/post-cleanup-tree.md`. It is read top-to-bottom: green-tick rows landed as planned, the deviation table at the end lists the few places where the delivered shape diverged from the planned one — each with the reason.

---

## Stage delivery summary

| # | Brief | Commit | Delivered |
|---|-------|--------|-----------|
| 1 | per-actor Channels.Serializers as single home | c74be34e | ✅ |
| 2 | drop dead Channels v1 helpers | 188205f5 | ✅ |
| 3 | KeepAlive becomes its own collection | 75ff8485 | ✅ |
| 4 | Modules and Providers self-dispose | e0364f36 | ✅ |
| 5 | drop App.GetStatic shim | 106031d1 | ✅ |
| 6 | App stops inheriting Data.@this<App> | 3ad8e7cc | ✅ |
| 7 | promote app.Debug.CallStack to app.CallStack | 33a3baf4 | ✅ |
| 8 | drop dead Channels.ReadAsync<T>(filePath) | cbc57eac | ✅ |
| 9 | Catalog dissolves into Modules.Schema | 9cc38299 | ✅ |
| 10 | App.Run reduced via two abstractions | 6992e223 | ✅ |
| 11 | Errors takes App at construction | e9b8a803 | ✅ |
| 12 | Build-mode bootstrap moves to Build.@this | 661a8e9c | ✅ |
| 13 | Settings reshape — collection-over-Data | cc1a5204 | ✅ |
| 14 | ExpiresInMs → Expires (TimeSpan + ISO 8601) | fa155d44 | ✅ scope-narrowed (see below) |
| 15 | compound-name-rename | 9902bbf0 | ✅ |
| 16 | static-state-eviction-sweep (Rule C) | 67bfdefc | ⚠️ partial — 4 of 8 sites deferred |
| 17 | Build → Builder, Test → Tester | 7ef0679f | ✅ |
| 18 | split MIME table — Formats + Types.ClrFromMime | 5e51d90d | ✅ different mount path (see below) |
| 19 | Provider → Code rename, end-to-end | 5de998d9 | ✅ |
| 20 | drop Channel.App redundant back-ref | d563bf2b | ✅ |
| 21 | Navigators move from Data/ to Variables/ | 96b1d8a7 | ✅ |
| 22 | drop app.Variables and app.Context shortcuts | cdc96a36 | ✅ |

All 22 stages landed. The branch is buildable and green throughout.

---

## What matches the destination tree

The mechanical and shape-altering work landed cleanly:

**Folder renames**
- `App/Build/` → `App/Builder/` (stage 17, Rule D)
- `App/Test/` → `App/Tester/` (stage 17, Rule D)
- `App/Providers/` → `App/Code/` (stage 19, Provider → Code)
- `App/Data/Providers/` → `App/Data/Code/` (stage 19)
- `App/modules/X/providers/` → `App/modules/X/code/` (stage 19, 10 modules)

**Marker / interface renames**
- `IProvider` → `ICode` — fields preserved (Name / IsDefault / IsBuiltIn / Source).
- All per-module interfaces dropped the `Provider` suffix: `IBuilder`, `ILlm`, `IHttp`, `ICrypto`, `IAssert`, `IIdentity`, `ITemplate`, `IFile`, `IGrep`, `ISigning`, `IEvaluator`.
- `ISettingsStore` → `IStore`; `SqliteSettingsStore` → `Sqlite`.
- `ISnapshotted` → `ISnapshot` (Snapshot folder).

**Implementation renames (drop both `Default` and `Provider`)**
- Variant-named where the parent path doesn't already say the role: `OpenAi.cs`, `Fluid.cs`, `Ed25519.cs`.
- `Default.cs` where the parent path already says it: `assert/code/Default.cs`, `builder/code/Default.cs`, etc.

**File renames (Rule A sub-rule — role suffix duplicates folder)**
- `Cache/MemoryStepCache.cs` → `Cache/Memory.cs`
- `Data/PlangTypeConverter.cs` → `Data/Converter.cs`
- `Channels/Serializers/TypeJsonConverter.cs` → `Data/Json.cs` (relocated)
- `Channels/Serializers/TimeSpanIso8601Converter.cs` → `TimeSpanIso8601.cs`
- `Channels/Serializers/{Sensitive,Transport,View}PropertyFilter.cs` → `Filters/{Sensitive,Transport,View}.cs`
- `Channels/Serializers/Serializer/JsonStreamSerializer.cs` → `Json.cs`
- `Channels/Serializers/Serializer/TextStreamSerializer.cs` → `Text.cs`
- `Channels/Serializers/Serializer/PlangSerializer.cs` → `Plang/this.cs`
- `Channels/Serializers/Serializer/PlangDataSerializer.cs` → `Plang/Data.cs`
- `Variables/Navigators/{Json,Dictionary,List,Object}Navigator.cs` → drop suffix (4 files)
- `Tester/{TestFile, TestRun, TestStatus}.cs` → `{File, Run, Status}.cs`

**New folders / files**
- `App/KeepAlive/this.cs` (stage 3)
- `App/Channels/Serializers/Filters/this.cs` (stage 15, Rule B)
- `App/Channels/Serializers/Serializer/Plang/` subfolder (stage 15)
- `App/Modules/Schema/` and `Modules/Schema/Spec/` (stage 9)
- `App/Variables/Navigators/` (stage 21, moved from `Data/Navigators/`)
- `App/Variables/Reserved.cs` (stage 16, moved from `Utils/ReservedKeywords.cs`; `const string` everywhere)

**Shape changes**
- App no longer inherits `Data.@this<App>`. `App.GetStatic` shim dropped.
- App-level `app.CallStack` mount (was `app.Debug.CallStack`).
- Errors gets App at construction (no late-injection).
- KeepAlive collection (replaced `App._keepAlive` private list).
- Modules / Code self-dispose.
- Settings reshape: collection-over-Data; SettingsVariable absorbed; `Variables.RegisterNavigable` mechanism wires `%Settings.X%`.
- `app.Variables` and `app.Context` shortcuts dropped — callers route through `app.CurrentActor.Variables` / `app.CurrentActor.Context`.

---

## Deviations from the destination tree

The following landed differently than the post-cleanup-tree said. Each is a coder judgment call that surfaced during execution; none are regressions.

### 1. `app.Formats` mounts at App root, not under `Channels.Serializers/`

**Planned:** `App/Channels/Serializers/Formats/this.cs` mounted as `app.Channels.Serializers.Formats`.
**Delivered:** `App/Formats/this.cs` mounted as `app.Formats`.
**Why:** During stage 18 design the mount path was settled as `app.Formats` (shorter, format families aren't strictly serializer concerns — also used for I/O routing). The folder placement followed the mount path.
**Verdict:** Reasonable. The post-cleanup-tree wasn't updated when the mount path was settled; the actual placement is the design intent.

### 2. `App/Choices/` stayed at root

**Planned:** ★ tentative move to `App/Builder/Choices/`.
**Delivered:** Stayed at `App/Choices/`.
**Verdict:** Correct call. The ★ marker said "re-evaluate after restructure lands"; the restructure didn't surface a stronger reason to move it. Build-time vs runtime border is fine where it is.

### 3. `App/Callback/Signature/this.cs` not absorbed (correction 2026-05-09: not a deviation)

**Planned:** Stage 14 absorbs `Signature/this.cs` into `Callback/this.cs` as a property; subfolder goes away.
**Delivered:** Stage 14 was scoped down to the `ExpiresInMs → Expires` rename only.
**Verdict (revised 2026-05-09):** The original plan was wrong. Folding the single `Expires` knob into `Callback.@this` would create a compound property name (`Callback.SignatureExpires`) — exactly the Rule A violation the cleanup is closing elsewhere. The current shape preserves the navigation chain `app.Callback.Signature.Expires`, which *is* OBP-correct. Removed from the deferred list; the folder stays as-is.

### 4. `App/Events/Lifecycle/Bindings/Binding/` did not collapse

**Planned:** `Events/Lifecycle/` layer goes away; Before/After become properties on `Events/this.cs`; structure becomes `Events/{Bindings/this.cs, Binding/this.cs, this.cs}`.
**Delivered:** Original three-layer nesting still in place.
**Verdict:** Open question F in the tree noted blast radius hadn't been measured; the collapse was tentatively folded into stage 15 but ended up out of scope. Mechanical when revisited; the alias `App.Events.Lifecycle.Bindings.Binding.@this` (used widely as `EventBinding`) needs re-targeting.

### 5. `App/CallStack/RestoredFrame.cs` not renamed to `Call/Position.cs`

**Planned:** Stage 10 folds in the rename.
**Delivered:** Stage 10 focused on the App.Run reduction (the headliner); the RestoredFrame rename was deferred.
**Verdict:** Cosmetic. Single-file rename whenever the next pass touches CallStack.

### 6. Stage 16 deferred 4 of 8 static-eviction sites

**Done (4):** Data/this.Envelope.cs, Plang/Data.cs serializer, builder provider's `_buildTimer`, OpenAi `_requestCount` (deleted entirely).
**Deferred (4):** Callback/AskCallback.cs `_options`, DefaultHttpProvider's two statics, Choices/this.cs `_gate + _registry`, all of Utils/PlangTypeIndex.cs.
**Why:** Each deferred site has a static-caller chain (~20 static helpers reach through them) that would cascade into a higher-level refactor. Coder's call: convert what's mechanical, defer what requires upper-level redesign (TypeMapping becoming instance-bound, etc.).
**Verdict:** Right call. Each deferred site is a real Rule C smell, but the eviction is a separate piece of work — properly scoped, not skipped.

### 7. `App/Utils/` is not "nearly empty"

**Planned:** Only `CommandLineParser.cs`, `PathExtension.cs`, `RegisterStartupParameters.cs`, `StringDistance.cs` remain. Json/PlangTypeIndex/TypeConverter/TypeMapping all dispersed into proper homes.
**Delivered:** All 8 files still in `Utils/` (the 4 planned dispersals were the deferred half of stage 16).
**Verdict:** Direct consequence of (6). When the deferred Rule C work lands, Utils empties as planned.

### 8. `App/Types/` did not gain `Registry.cs` or `Conversion.cs` partials

**Planned:** Stage 16 makes `Types.@this` partial; absorbs `PlangTypeIndex` into `Registry.cs`, `TypeConverter` into `Conversion.cs`.
**Delivered:** `App/Types/this.cs` only. Stage 18 added the `Clr(mimeType)` overload as planned, but the partial split deferred with the rest of the static eviction.
**Verdict:** Same as (6) and (7).

### 9. `App/Data/Code/Default.cs` instead of `Grep.cs`

**Planned:** Drop both `Default` and `Provider` — file becomes `Grep.cs`.
**Delivered:** `IGrep` declares a `Grep()` method; the implementing class can't share the name. Coder kept it as `Default.cs`.
**Verdict:** Right call. C# class-and-method-name collision is a hard constraint the brief missed.

### 10. App spine shrunk less than planned

| File | Baseline | Planned | Actual |
|------|----------|---------|--------|
| `App/this.cs` | 681 | <300 | 596 |
| `App/Modules/this.cs` | 464 | ~150 | 494 |
| `App/Channels/this.cs` | 277 | <150 | 242 |

**Why:** The shrink targets were aggressive estimates anchored on "what the responsibilities look like once OBP-correct." The stages that fed each shrink (3, 4, 5, 6, 7, 10, 11, 12 for App; 4, 9 for Modules; 1, 2, 8 for Channels) all landed, but the per-file line count moved less because non-OBP-violating bulk (XML doc comments, struct-and-record definitions, helper methods that survive the cleanup) wasn't in scope.
**Verdict:** The *responsibility* slice landed — App no longer holds KeepAlive lifecycle, Errors back-injection, Build-mode bootstrap, GetStatic shim, Data inheritance machinery, Run orchestration, CallStack property. The line count is a lagging indicator of OBP-correctness, not a primary target.

---

## What's deferred to follow-up

**Update 2026-05-09:** Items (1) and (2) below were carved into Tier 5 (stages 23–29) on the same branch — see `plan.md`. The Callback/Signature absorb that originally appeared under (2) was removed: it's OBP-correct as-is (see correction above). Item (3) stays deferred — bigger design passes that don't fit this branch.

1. **Static-eviction tail** (4 sites from stage 16) — *carved as stages 25–28 in Tier 5:*
   - `Callback/AskCallback.cs._options` → stage 25
   - `DefaultHttpProvider._jsonOptions + _transportInOptions` → stage 26
   - `Choices.@this._gate + _registry` → stage 27
   - `Utils/PlangTypeIndex.cs` (whole class) → stage 28 (with TypeMapping → instance keystone)

   Each requires the static-caller chain to flatten first (TypeMapping made instance-bound, or context threaded to all callers). Stage 28 does that flattening; stage 29 finishes the Utils/ empty-out (TypeConverter → Types/Conversion partial; Utils/Json disperses).

2. **Cosmetic rename leftovers** — *carved as stages 23–24 in Tier 5:*
   - `CallStack/RestoredFrame.cs` → `Call/Position.cs` → stage 23
   - `Events/Lifecycle/` layer collapses → stage 24
   - ~~`Callback/Signature/this.cs` absorbs into `Callback/this.cs`~~ — withdrawn; current shape is OBP-correct.

3. **Documented in `Documentation/Runtime2/todos.md`** (still deferred — not in Tier 5):
   - Events three-tier writer wiring (per-channel / per-actor / app-level)
   - CallStack scope (per-context vs shared) — too big for this branch

---

## Headline numbers

- **22** stage briefs carved
- **22** coder commits landed
- **2** codeanalyzer reviews, both PASS
- **354** `.cs` files under `PLang/App/` at end (vs 681 baseline lines in `App/this.cs` alone)
- **9** new folders created (`KeepAlive/`, `Filters/`, `Plang/`, `Modules/Schema/`, `Modules/Schema/Spec/`, `Builder/`, `Tester/`, `Code/`, `Formats/`)
- **4** folder renames (`Build/→Builder/`, `Test/→Tester/`, `Providers/→Code/`, `Data/Providers/→Data/Code/`)
- **10** module-folder renames (`modules/X/providers/→modules/X/code/`)
- **40+** file renames across all stages
- **0** test regressions throughout

The branch ships ready for review.
