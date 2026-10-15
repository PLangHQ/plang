# coder summary — kind-is-type

**Version:** v2 (stage 2)

## What this is
Decision 774: "a kind is a type." `kind<T> : kind : type`. A kind is a type under its parent, named uniquely only under its parent. Plan: `test/plan/kind-is-type/start.md`. Stages: 0 done, 1 done+accepted, **2 in progress (this version)**, 3–6 pending.

**Stage 2 — "settings are settings, not kinds":** a settings class is no longer a *kind of* `setting`; it is its **own top-level type**, named by its namespace (`app.module.llm.setting`, `app.goal.list.setting`, …). `setting.@this` drops `[Kinds]`. The option rows a builder/face reads live on `setting.@this` as `Rows(context)` (a method, not a stored field — item Context fields are forbidden). Each concrete settings subtype declares `public static bool Internal => true` (pin: `SettingsAreInternalTests.EverySettingsSubtype_IsInternal`); the base `setting` stays offered so the decider still has the concept. `actor/setting.Class(path)` is actor/setting's own path→class index; rows are read by KEY, not by a stored kind.

## What was done
- `PLang/app/type/item/setting/this.cs` — dropped `[Kinds]`; `Type` is own node; `Rows(context)` method; `Get` answers `Path` + `Rows(parent.Context)` beside options. Public `type` facts dropped `virtual`.
- `Internal => true` on ~20 concrete settings classes + `module/llm/decider.setting.cs` (also `[PlangType("app.module.llm.decider.setting")]`).
- `PLang/app/actor/setting/this.cs` — `Class`=index, `Make`, `Fresh`, `Own` reads `[Own]`; `Read()` still routes via `row.Value()` (**this is the open issue**).
- `PLang/app/type/item/setting/serializer/Reader.cs` — reads by `kind`=key path via `ctx.Setting.Fresh(kind)`, ignores stored kind (old & new rows read the same way). **Ready for the fix; just not yet driven on the disk path.**
- Deleted `setting/kind/this.cs`. Surface golden re-pinned (diff only stage-2/settings/kind). New: `SettingsAreInternalTests.cs`; `KindsTests.AClassOfSettings_IsItsOwnType` → `"app.setting"`; `QueryConversationTests` → `Rows(ctx)`.

**Done:** all in-memory settings, decider offerings (Internal excludes bases), surface golden, PickList 8/8, QueryConversation, KindsTests 6/6, SettingsAreInternal pin, OldSettingRowStillReads, same-app save+read.

**Open issue (5 reds, ONE root cause — surfaced to architect, awaiting A/B):** persisted settings read-back across apps. `actor/setting.Read()` materializes each row via `row.Value()`, which routes by the row's STORED type. Stage 2 makes Save store `type`=concrete class name (no kind); the setting reader registers under `typeName="setting"` so the row never reaches it → defaults returned. The value is a `wire.@this` materializing through its held serializer by the stored (type,kind). The two clean fixes both touch the LOCKED core (store surface / serializer), so I surfaced the A/B choice rather than pick:
  - **(A)** Save tags the stored value slot `type="setting"+kind=classpath` (restores old wire shape; `row.Value()` then routes to the setting reader; old rows already match). My lean.
  - **(B)** `Read()` drives the setting reader with `kind=keyPath`, needing a raw-rows door on `app.store` or a wire re-read override.
  Failing tests: `SettingRowTests` (Row_IsReadByTheNextApp / Row_TypedListOfChoices_IsReadBack / Row_TypedListOfText_IsReadBack), `SettingOwnerTests.Rows_AreThereAfterTheAppLoads`, `SettingsDataTests.ASavedSetting_LivesInTheSettingsOwnStore`.

**Next:** architect picks A or B → implement → re-run the 5 + full C# gate (back to 8 pre-existing baseline fails) + plang suite (confirm `SettingClassIsAType` green) → commit + push → report Done/HASH/CHECK/OPEN.

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
