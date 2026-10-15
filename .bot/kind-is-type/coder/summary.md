# coder summary — kind-is-type

**Version:** v4 (stage 4 — "a format owns its bytes"). v1–v3 = stages 1–3 (done + accepted). Decision 774 plan: `test/plan/kind-is-type/start.md`.

## What this is
Stage 4 of "a kind is a type" (774). Two threads landed here:
1. **Separator** (783/788) — `separator.@this : text.@this` is `{text, kind: separator}`; text unsealed + `[Kinds]`. DONE+green (earlier this version).
2. **Decision 789/790** — the item owns how it's made: a type with no reader reads its stored value as a *record* and makes itself from it; the start-up settings-reader loop + per-type setting reader are gone. DONE+green.
3. **Decision 792 json migration** — json is `{item, kind: json}` holding raw bytes until first use; navigation parses once into a plang dict/list; the value then narrows (787). **IN PROGRESS** — one producer per commit, the clr(json)-navigator + text-json-wrapper deleted in the LAST commit.

## 789/790 — DONE (HEAD has it)
- `setting.Create(raw, declared, data)` makes the setting from its stored record by `declared.ClrType` only (790: no old-row path). A dict record → fresh instance of the class + `Apply` its options. An older-shape `{setting, kind:<path>}` row → loud `SettingsUnreadable` (soft `data.Fail`, named by key). A bare base-`setting` node (empty kind) → a clean decline (null, no Fail) → reads as `@null` (the old base reader's behaviour).
- `app/type/reader/Record.cs` — the record-reader fallback. `Reader.Reader()` returns it instead of throwing when `Typed()` is null. It reads the value as a generic record (dict/list/scalar), then `type.Construct(record, data)`. A declined make → `DeclinedException` (which `source.Value` catches → soft failure on the binding); a clean decline (null, no error) → `@null`.
- **`type.Construct(raw, data)`** (`PLang/app/type/this.cs`) — the SECOND make door (beside `Make`). Make stays on the EXACT `Creatable` (its CLR-value fallback `Make(item.Create(raw))` only terminates because of that — broadening it caused a **stack overflow**, Ingi flagged it). Construct uses `CreatableFamily` = the MOST-DERIVED `ICreate<T>` the class implements (a settings class is both `ICreate<item>` and `ICreate<setting>` → pick `setting`, not the apex `item`). The closed family thunk is cached once (`_construct`, like `_lift`). "One make door" is on the architect's branch-A list.
- `item.Create(raw, declared, data)` (`PLang/app/type/item/this.cs`) DECLINES a declaration whose class is ANOTHER type (reached via the family because that type's most-derived ICreate is item — a `{table, xlsx}`): it does not pass a stray text through nor re-source it. Its lazy-kind rebirth is for item's OWN kinds (`{item, json}`).
- Gone: start-up loop (`app/this.cs`), `setting/serializer/Reader.cs`, `OldSettingRowStillReadsTests` + fixture. Pins: `SettingRowTests` 8/8 incl. the new `AppSetting_Culture_IsReadByTheNextApp`; `TableTypeTests` 8/8 (xlsx declines softly, CreateItemDeclined).

## json migration (792) — IN PROGRESS
test-designer's classification: `.bot/kind-is-type/test-designer/json-tests.md` (14 keep-and-rewrite, 10 retire). The 10 retired tests + the clr(json) navigator + text-json wrapper go in the FINAL commit. Navigator kept until then.

**Producers (one commit each, red→green against test-designer's new files):**
- ✅ **json-array foreach** — `data.EnumerateItems` materializes an unread `{item,json}` source before walking (parse once → list, narrow 787); a value already walkable (a `%var%`'s list) keeps its door shut. `JsonArrayForeachTests` green. (commit a0a686cbd)
- ⏳ **action-row carrier** (NEXT) — `ActionNameWireRead_PlangListTests` (`WireKey_*_FromAPlangList`, `ActionTypedRow_...FromAPlangList`). A plang `list<dict>` set onto `Step.Code` (a `step.action.list`) must read each dict as an action. **Trace so far:** the action reader (`goal/step/action/serializer/Reader.cs`) is TOKEN-and-step-bound (reads `module`/`name`/`property`/`child` off a json token stream, born with the step). The list reader (`list/serializer/Reader.cs`) is token-based too (per-element `reader.RawValue()`). A parsed plang `list<dict>` has no tokens. `Step.Code` setter takes a `step.action.list`; `variable.Set("goal.Step[0].Code", plangList)` navigates to the Step (a domain/clr object) and sets Code → the plang list<dict> → `step.action.list` conversion is the gap (Code.Count 0). **Open design q:** read each dict as an action by (a) re-serializing the dict to json tokens and feeding the existing action reader (bridge, a round-trip), or (b) a dict→action path on the action reader (bigger). Lean (a) as a bounded bridge; confirm with architect if it gets large.
- ⏳ **http body verbatim** — `PostItemOfKindJson_IsSentVerbatim_Unread`: an `{item,json}` body sends its raw bytes unread (raw-until-first-use); producer re-serializes today.
- ⏳ **url by content-type** — `UrlServedApplicationJson_DecodesAsItemOfKindJson`: a url served `application/json` decodes to `{item, kind: json}` (resting form), not straight to dict.
- ⏳ **render a .json file** — `RenderJsonFileIsItemOfKindJson`: `Type.Name` == item, kind json; producer narrows to dict today.
- (SKIP the 4 PlangDict producers — http GET/upload, llm query ±schema — already narrow to a plang dict via the json-core parse; test-designer tightened them green.)

**Final commit:** delete json's navigator (Descend/Data/Enumerate/IsSequence/Set/Clr/Output/Write/Scalar + ClrForm=JsonElement; keep Mime/Extension/Encode/Parse/Open/Read), the text `{text,json}` wrapper + `*format` coiner + Registry mint + one-arg `Kind(string)` (⚠️ it has ~25 TEST callers validating alias/MIME/ext/ambiguity resolution — move to a test extension OR rewrite them; flagged, NOT yet resolved), `serializer/item/json.cs` STAYS (many live callers — Q1 was wrong), the dict/list `clr{JsonElement}` Convert/Create branches, http outbound form (`http/code/Default.cs:784,845`); + retire the 10 old-design tests.

## Re-pins pending (the architect's single pass, step c — AFTER the json migration)
- `TheCoresSurface_IsThePinnedOne` (789 deleted the public setting serializer Reader → core surface moved).
- `TheSeparatorPage_IsTheSpecsGolden` + `StageOnesAnswer_*` (2) — separator left the top-level types.
- any json page golden the navigator deletion moves.
Read each diff before committing a moved pin (teaching-edit rule). Then send the architect the 2 list `.pr` paths for the builder bot.

## Baseline / gate
No stack overflow (the isolated Construct/`_construct` restored Make's termination). Wire baseline = 6 snapshot + 2 StageOnes (pre-existing). The json reds remaining are migration targets + old tests retiring. Run `./dev.sh test <Class>` (TableTypeTests + the new json test files live in the **Data** and **Modules** suites; `PLang.Tests.App.LazyDeserialize.OneBoundaryTests` etc.). Full parallel `./dev.sh test` can OOM-crash suites under load — if a suite shows "Stack overflow"/"NO SUMMARY", re-run it standalone to confirm (was a parallel flake for Generator once).

## Architect coordination
Peer architect at `uds:/tmp/cc-socks/266.sock`. Sequence agreed: 789 ✅ → json producers (one commit each) ⏳ → navigator deletion + retire 10 tests (final commit) → single re-pin pass → send 2 list `.pr` paths. Reply Done/HASH/CHECK/OPEN after each.
