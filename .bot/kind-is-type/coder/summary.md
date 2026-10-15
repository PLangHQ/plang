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
- ✅ **json-array foreach** — `data.EnumerateItems` materializes an unread `{item,json}` source before walking (parse once → list, narrow 787); a value already walkable keeps its door shut. `JsonArrayForeachTests` green. (a0a686cbd)
- ✅ **action-row carrier** — (A, architect-ruled) `Step.Set("code")`, given a parsed plang `list<dict>`, writes each dict as json (`FromJson`, async) and the ONE action serializer reader (born with the step) reads those tokens — symmetric with the clr(json) serialize+feed. `ActionNameWireRead_PlangListTests` 3/3. (11cf77300) Branch A will give the reader a structural reader over a plang value, dropping the round-trip.
- ✅ **http body verbatim** — the body's presence is read by Peek (`HasValue`), never Value; an unread `{item,json}` body stays unread until written, where json relays its bytes verbatim; only a form body is materialized. `PostItemOfKindJsonVerbatimTests`. (3a901ff05)
- ⏳ **url by content-type** (`UrlServedApplicationJson_DecodesAsItemOfKindJson`) — a json url must REST at `{item,json}` not narrow to dict. ⚠️ **LEARNING:** I tried making `content.@this.Value` return the RESTING decoded value (`read.Peek()`) for plain content — it GREEN'd url but BROKE ~40 file-content tests (file reads, prose, page goldens, GetGoals): `read.Peek()` is the unread source, wrong for non-json files (text/image must resolve). **REVERTED.** The right fix is json/rests-on-navigate-SPECIFIC (not a blanket content.Value change): only a kind that parses-on-navigate (json) should rest; text/image resolve. Needs care.
- ⏳ **render a .json file** (`RenderJsonFileIsItemOfKindJson`) — a rendered .json file must be `{item,json}` AND render its template (`{{model}}`→m-1). SEPARATE path (the ui render module, not content.Value) — today it returns unrendered binary ("(23 bytes)"). Needs a json-template rest that renders but keeps `{item,json}` type.
- (SKIP the 4 PlangDict producers — http GET/upload, llm query ±schema — already narrow to a plang dict; test-designer tightened them green.)

**789 key consequence (done):** 790 dropped the decider's old-shape key row → empty key → TypeSafe 403. Re-saved the key (C# one-off harness, deleted; both System+User actors; root `./.data`; key never printed/committed). Real build of a fresh if/call/on.error app SUCCEEDED, code rows correct (nested if-child + on.error recovery `list<action>` intact). Make-loud (62f08e5e6) + record-read kind-pass (7fc410084): an unreadable `{setting,kind:<path>}` row is now loud (named key + how-to-fix), not silently skipped. ⚠️ The **pin** ("a store with one old-shape row → the message") needs a genuine old-shape WIRE fixture (the pre-779 writer is gone) — flagged to test-designer.

**Scope (architect):** plang-builder bot takes the 2 list `.pr` rebuilds (separator) + stage 5 (builder offers kinds). I keep stage-4 producers + navigator deletion + the single re-pin pass, minus those. If I need `os/system/builder/**` or pick/decider code, tell the architect first.

**Final commit (navigator deletion):** delete json's navigator (Descend/Data/Enumerate/IsSequence/Set/Clr/Output/Write/Scalar + ClrForm=JsonElement; keep Mime/Extension/Encode/Parse/Open/Read), the text `{text,json}` wrapper + `*format` coiner + Registry mint + one-arg `Kind(string)` (⚠️ ~25 TEST callers validate alias/MIME/ext/ambiguity — move to a test extension OR rewrite; NOT yet resolved), `serializer/item/json.cs` STAYS (many live callers), the dict/list `clr{JsonElement}` Convert/Create branches, http outbound form (`http/code/Default.cs:784,845`); + retire the 10 old-design tests.

## Re-pins pending (the architect's single pass, step c — AFTER the json migration)
- `TheCoresSurface_IsThePinnedOne` (789 deleted the public setting serializer Reader → core surface moved).
- `TheSeparatorPage_IsTheSpecsGolden` + `StageOnesAnswer_*` (2) — separator left the top-level types.
- any json page golden the navigator deletion moves.
Read each diff before committing a moved pin (teaching-edit rule). Then send the architect the 2 list `.pr` paths for the builder bot.

## Baseline / gate
No stack overflow (the isolated Construct/`_construct` restored Make's termination). Wire baseline = 6 snapshot + 2 StageOnes (pre-existing). The json reds remaining are migration targets + old tests retiring. Run `./dev.sh test <Class>` (TableTypeTests + the new json test files live in the **Data** and **Modules** suites; `PLang.Tests.App.LazyDeserialize.OneBoundaryTests` etc.). Full parallel `./dev.sh test` can OOM-crash suites under load — if a suite shows "Stack overflow"/"NO SUMMARY", re-run it standalone to confirm (was a parallel flake for Generator once).

## Architect coordination
Peer architect at `uds:/tmp/cc-socks/266.sock`. Sequence agreed: 789 ✅ → json producers (one commit each) ⏳ → navigator deletion + retire 10 tests (final commit) → single re-pin pass → send 2 list `.pr` paths. Reply Done/HASH/CHECK/OPEN after each.
