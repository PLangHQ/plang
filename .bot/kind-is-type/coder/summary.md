# coder summary — kind-is-type

**Version:** v2 (stage 2)

## What this is
Decision 774: "a kind is a type." `kind<T> : kind : type`. A kind is a type under its parent, named uniquely only under its parent. Plan: `test/plan/kind-is-type/start.md`. Stages: 0 done, 1 done+accepted, **2 in progress (this version)**, 3–6 pending.

**Stage 2 — "settings are settings, not kinds":** a settings class is no longer a *kind of* `setting`; it is its **own top-level type**, named by its namespace (`app.module.llm.setting`, `app.goal.list.setting`, …). `setting.@this` drops `[Kinds]`. The option rows a builder/face reads live on `setting.@this` as `Rows(context)` (a method, not a stored field — item Context fields are forbidden). Each concrete settings subtype declares `public static bool Internal => true` (pin: `SettingsAreInternalTests.EverySettingsSubtype_IsInternal`); the base `setting` stays offered so the decider still has the concept. `actor/setting.Class(path)` is actor/setting's own path→class index; rows are read by KEY, not by a stored kind.

## What was done
- `PLang/app/type/item/setting/this.cs` — dropped `[Kinds]`; `Type` is own node; `Rows(context)` method; `Get` answers `Path` + `Rows(parent.Context)` beside options. Public `type` facts dropped `virtual`.
- `Internal => true` on ~20 concrete settings classes + `module/llm/decider.setting.cs` (also `[PlangType("app.module.llm.decider.setting")]`).
- `PLang/app/actor/setting/this.cs` — `Class`=index (keyed by Path; type list keyed by Name — they differ, 774A), `Make`, `Fresh`, `Own` reads `[Own]`.
- `PLang/app/type/item/setting/serializer/Reader.cs` — resolves the class from the type it reads as (new rows) or kind-as-path (old rows); per-type instances via an internal ctor. See RESOLVED below.
- `PLang/app/this.cs` — registers each settings type's reader at boot (option C). See RESOLVED below.
- Deleted `setting/kind/this.cs`. Surface golden re-pinned (diff only stage-2/settings/kind). New: `SettingsAreInternalTests.cs`; `KindsTests.AClassOfSettings_IsItsOwnType` → `"app.setting"`; `QueryConversationTests` → `Rows(ctx)`.

**Done:** all in-memory settings, decider offerings (Internal excludes bases), surface golden, PickList 8/8, QueryConversation, KindsTests 6/6, SettingsAreInternal pin, OldSettingRowStillReads, same-app save+read.

**RESOLVED — persisted settings read-back (decision 779, option C).** Architect ruled option C: a saved row is written as its own type and read back by that type; the setting reader, asked to read as a settings type, takes the class from that type (its C# class); an old `{setting, kind: path}` row still reads through the base `setting` reader by its kind. Implemented:
  - `setting/serializer/Reader.cs` — resolves the class from the type it reads as (`types[_type].ClrType` → fresh instance) for new rows, or `Fresh(kind)` for old rows. Per-type instances carry `_type` via an **`internal` ctor** (no locked-surface change to the reader's public surface).
  - `app/this.cs` (boot, beside the path schemes) — for each concrete settings type, `type.list.Reader.Register(type.Name, AnyKind, new setting.serializer.Reader(type.Name))`. Keeps both the reader registry and `Registry.Enlist` ignorant of settings (no settings clause in the registry).
  - Save unchanged — the `data` ctor already stores the row as the setting's own type name, matching the registration key.
  - **Surface golden re-pinned** (`AcceptTheSurface`, commit names 778/779): one line — `setting.@this.Rows` is `Rows(context)` method (the option rows are the setting's own member per 778; the no-Context-field rule makes it a method, not a property). This corrected a stale WIP re-pin.

**Gate (verified):**
  - C# full sweep → the **8 pre-existing baseline fails only** (`ATypedDictSlot_FirstReadOfTheDict`, `TheNestedWrittenForm_ReadsThroughTheTypeSerializer`, 6 snapshot tests). All settings C# green; surface golden green.
  - Plang suite → **254 pass / 24 fail / 12 stale / 3 skip** (baseline 253/25/12/3 — one more pass, one fewer fail, zero new reds). `SettingClassIsAType.test.goal` → **Pass**. The remaining `/plan/kind-is-type/...` json/separator/file/variable fails are **stage 3/4 targets** (format bytes, every family), not stage 2.

**Stage 2 is complete.** Next: architect acceptance → stage 3.

## Code example
A settings class is now its own type, Internal:
```csharp
[global::app.Attributes.PlangType("app.module.llm.setting")]   // its own namespace name
public sealed class @this : global::app.type.item.setting.@this
{
    public static bool Internal => true;     // not offered to the decider; base `setting` is
    // options as [Out, Store] properties …
}
```
The option rows read on the base as a context-taking method (no stored Context field):
```csharp
public global::app.type.item.list.@this Rows(global::app.actor.context.@this context) => /* options of this class */;
```

## For v2 (post stage-1 review)
Stage 1 accepted; see `v2/stage1-result.md`. No reviewer flags carried into stage 2 beyond the plan's stage boundary.

**Baseline (pre-existing C# reds, NOT mine):** `ATypedDictSlot_FirstReadOfTheDict`, `TheNestedWrittenForm_ReadsThroughTheTypeSerializer`, + 6 snapshot tests (`MidStackChain`, `NavigateAndEditCapturedVariable`, `PlangPath_AsSnapshotConvert`, `SerializedString_ConvertsToSnapshot`, `ThrowTimeSnapshot`, `TypedSnapshotString`).
