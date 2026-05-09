# `PLang/App/` — Actual End-State Tree (post-cleanup, all 27 stages)

What `PLang/App/` actually looks like as of `runtime2-cleanup` HEAD (commit `6fa6dbda`, after **all 27 stages** landed — Tiers 1–4 (stages 1–22) + Tier 5 (stages 23–27)). This is a snapshot of the delivered shape, with annotations showing what each subtree looked like before cleanup.

**Update 2026-05-09 (Tier 5 close):** Tier 5 markers below have been resolved to their final landed shape. All deferred markers from the original Tier 1–4 audit are now closed.

Compare side-by-side with `plan/post-cleanup-tree.md` (the planned destination) and `results.md` (the deviations audit).

## Marker key

| Marker | Meaning |
|---|---|
| `(NEW)` | Folder or file created by the cleanup |
| `(MOVED ← X)` | Same content, relocated from X |
| `(RENAMED ← Y)` | Same content, renamed from Y |
| `(REWORKED)` | Same path, materially redesigned shape |
| `(★ deferred)` | Planned for cleanup, not done — see `results.md` |
| *(no marker)* | Unchanged from baseline |

## Tree

```
PLang/App/
├── Actor/
│   ├── Context/
│   │   ├── Trace/this.cs
│   │   └── this.cs
│   └── this.cs
│
├── Attributes/                                  (unchanged — pure attributes, no @this)
│   ├── Attributes.cs
│   ├── ChoicesAttribute.cs
│   ├── Class.cs
│   ├── ExampleAttribute.cs
│   ├── GreatLeapAttribute.cs
│   ├── IgnoreWhenInstructedAttribute.cs
│   ├── Init.cs
│   ├── IsBuiltParameter.cs
│   ├── LlmIgnoreAttribute.cs
│   ├── MethodAttribute.cs
│   ├── MethodSettingsAttribute.cs
│   ├── PlangTypeAttribute.cs
│   ├── PropertyAttribute.cs
│   ├── RequiresCapabilityAttribute.cs
│   └── ReturnRequired.cs
│
├── Builder/                                     (RENAMED ← Build/, stage 17; Rule D — gerund→noun)
│   ├── this.Snapshot.cs
│   └── this.cs                                  (gains build-mode bootstrap from App.Start, stage 12; Tier 5 stage 27: gains internal-static PrWrite + StoreOnlyModifier from Utils/Json.cs)
│
├── Cache/
│   ├── Memory.cs                                (RENAMED ← MemoryStepCache.cs, stage 15)
│   └── this.cs
│
├── CallStack/                                   (gains app-level mount app.CallStack, stage 7)
│   ├── Audit/this.cs
│   ├── Call/
│   │   ├── Children/this.cs
│   │   ├── Diffs/this.cs
│   │   ├── Errors/this.cs
│   │   ├── Position.cs                          (RENAMED ← RestoredFrame.cs, Tier 5 stage 23; namespace App.CallStack → App.CallStack.Call)
│   │   ├── Tags/this.cs
│   │   ├── this.Snapshot.cs
│   │   └── this.cs                              (gains Call.ExecuteAsync, stage 10)
│   ├── Diff.cs
│   ├── Flags.cs
│   │   (RestoredFrame.cs — DELETED, relocated to Call/Position.cs in Tier 5 stage 23)
│   ├── this.Snapshot.cs
│   └── this.cs
│
├── Callback/
│   ├── AskCallback.cs                           (Tier 5 stage 24: _options static evicted, navigates ctx.App.Callback.Wire.Options)
│   ├── ErrorCallback.cs                         (Tier 5 stage 24: _options static evicted; helpers thread options as parameter)
│   ├── ICallback.cs
│   ├── Signature/this.cs                        (kept — OBP-correct; navigation app.Callback.Signature.Expires is the right shape, see results.md correction 2026-05-09)
│   ├── Wire/                                    (NEW Tier 5 stage 24 — sub-@this for callback wire format)
│   │   └── this.cs                              (holds Options: JsonSerializerOptions; mounted as app.Callback.Wire)
│   └── this.cs                                  (ExpiresInMs → Expires/TimeSpan, stage 14; gains Wire property in Tier 5 stage 24)
│
├── Channels/
│   ├── Channel/
│   │   ├── Events/this.cs
│   │   ├── Goal/this.cs
│   │   ├── Message/this.cs
│   │   ├── Session/this.cs
│   │   ├── Stream/this.cs
│   │   └── this.cs                              (Channel.App back-ref dropped, stage 20)
│   ├── Serializers/                             (per-actor canonical home, stage 1)
│   │   ├── Filters/                             (NEW collection, stage 15 — Rule B)
│   │   │   ├── Sensitive.cs                     (RENAMED ← SensitivePropertyFilter.cs)
│   │   │   ├── Transport.cs                     (RENAMED ← TransportPropertyFilter.cs)
│   │   │   └── View.cs                          (RENAMED ← ViewPropertyFilter.cs)
│   │   ├── Serializer/
│   │   │   ├── Json.cs                          (RENAMED ← JsonStreamSerializer.cs)
│   │   │   ├── Plang/                           (NEW subfolder, stage 15)
│   │   │   │   ├── Data.cs                      (RENAMED ← PlangDataSerializer.cs)
│   │   │   │   └── this.cs                      (RENAMED ← PlangSerializer.cs)
│   │   │   ├── Text.cs                          (RENAMED ← TextStreamSerializer.cs)
│   │   │   └── this.cs
│   │   ├── TimeSpanIso8601.cs                   (RENAMED ← TimeSpanIso8601Converter.cs)
│   │   ├── UnregisteredMimeType.cs              (kept — typed exception)
│   │   └── this.cs
│   └── this.cs                                  (v1 helpers gone — stage 2; ReadAsync<T>(filePath) gone — stage 8)
│
│   (App/Choices/ — DELETED Tier 5 stage 26; relocated under Types/Choices/)
│
├── Code/                                        (RENAMED ← Providers/, stage 19)
│   ├── ICode.cs                                 (RENAMED ← IProvider.cs; Name/IsDefault/IsBuiltIn/Source preserved)
│   ├── this.Snapshot.cs
│   └── this.cs                                  (self-disposes, stage 4)
│
├── Config/
│   ├── IConfig.cs
│   ├── ModuleView.cs
│   ├── Scope.cs
│   └── this.cs
│
├── Data/
│   ├── Code/                                    (RENAMED ← Providers/, stage 19)
│   │   ├── Default.cs                           (RENAMED ← DefaultGrepProvider.cs; kept "Default" — IGrep.Grep() name collision)
│   │   └── IGrep.cs                             (RENAMED ← IGrepProvider.cs)
│   ├── Converter.cs                             (RENAMED ← PlangTypeConverter.cs, stage 15)
│   ├── Json.cs                                  (RELOCATED ← Channels/Serializers/TypeJsonConverter.cs, stage 15)
│   ├── JsonString.cs                            (NEW Tier 5 stage 27 — ToJson extension + FixJsonStringValues + EmptyStringToNullEnumConverter; relocated from Utils/Json.cs)
│   ├── Properties.cs
│   ├── TString.cs
│   ├── this.Compare.cs                          (Tier 5 stage 27: gains static-readonly _camelCaseIndented for Compare's serialization)
│   ├── this.Envelope.cs                         (_envelopeJsonOptions → instance, stage 16)
│   ├── this.Navigation.cs
│   ├── this.Result.cs
│   └── this.cs
│
├── Debug/
│   └── this.cs                                  (CallStack property moved out, stage 7)
│
├── Diagnostics/                                 (NEW Tier 5 stage 27 — static class)
│   └── this.cs                                  (Format(value) helper + Options: JsonSerializerOptions; consumers: Errors/AssertionError, modules/assert, modules/test/report)
│
├── Errors/                                      (App back-ref injection dropped, stage 11)
│   ├── ActionError.cs
│   ├── AskError.cs
│   ├── AssertionError.cs
│   ├── CallChainRenderer.cs
│   ├── CallbackGoalErrors.cs
│   ├── Error.cs
│   ├── ErrorCategory.cs
│   ├── Exceptions.cs
│   ├── GoalError.cs
│   ├── IError.cs
│   ├── ParamSnapshot.cs
│   ├── ProgramError.cs
│   ├── ServiceError.cs
│   ├── SettingsError.cs
│   ├── StepError.cs
│   ├── Trail/
│   │   ├── this.Snapshot.cs
│   │   └── this.cs
│   ├── ValidationError.cs
│   ├── this.Snapshot.cs
│   └── this.cs
│
├── Events/
│   ├── EventType.cs
│   ├── Lifecycle/                               (★ folded into Events three-tier todo 2026-05-09 — structure question deferred to that design pass; cleanup-pass discovered Lifecycle is a per-target view, not redundant nesting)
│   │   ├── Bindings/
│   │   │   ├── Binding/this.cs
│   │   │   └── this.cs
│   │   └── this.cs
│   └── this.cs
│
├── FileSystem/
│   ├── Default/
│   │   ├── PLangDirectoryInfoFactory.cs
│   │   ├── PLangDirectoryWrapper.cs
│   │   ├── PLangDriveInfoFactory.cs
│   │   ├── PLangFile.cs
│   │   ├── PLangFileInfo.cs
│   │   ├── PLangFileStreamFactory.cs
│   │   ├── PLangFileStreamWrapper.cs
│   │   ├── PLangFileSystem.cs
│   │   ├── PLangFileSystemWatcherFactory.cs
│   │   └── PLangPath.cs
│   ├── IPLangFileSystem.cs
│   └── Path.cs
│
├── Formats/                                     (NEW, stage 18 — mounts as app.Formats; placement different from plan, see results.md #1)
│   └── this.cs                                  (ext→MIME table, absorbed from Utils/MimeTypes.cs)
│
├── Goals/
│   ├── Goal/
│   │   ├── GoalCall.cs
│   │   ├── Methods.cs
│   │   ├── Steps/
│   │   │   ├── Step/
│   │   │   │   ├── Actions/
│   │   │   │   │   ├── Action/
│   │   │   │   │   │   ├── Modifiers/this.cs
│   │   │   │   │   │   └── this.cs
│   │   │   │   │   └── this.cs
│   │   │   │   ├── CacheSettings.cs
│   │   │   │   ├── ErrorOrder.cs
│   │   │   │   └── this.cs
│   │   │   └── this.cs
│   │   └── this.cs
│   ├── Setup/this.cs
│   └── this.cs
│
├── KeepAlive/                                   (NEW, stage 3 — replaces App._keepAlive private list)
│   └── this.cs
│
├── Modules/
│   ├── Schema/                                  (NEW, stage 9 — absorbs former App/Catalog/)
│   │   ├── Spec/                                (record family)
│   │   │   ├── Action.cs                        (RENAMED ← Catalog/ActionSpec.cs)
│   │   │   └── Example.cs                       (RENAMED ← Catalog/ExampleSpec.cs)
│   │   ├── Entry.cs                             (RENAMED ← Catalog/TypeEntry.cs)
│   │   ├── Render.cs                            (RENAMED ← Catalog/ExampleRenderer.cs; instance method)
│   │   └── this.cs                              (RENAMED ← Catalog/this.cs)
│   └── this.cs                                  (example-rendering becomes local navigation Schema.Render(spec); self-disposes — stage 4)
│
│   (App/Catalog/ — DELETED entirely, stage 9)
│
├── Services/
│   ├── Service/this.cs
│   └── this.cs
│
├── Settings/                                    (REWORKED, stage 13 — collection-over-Data; SettingsVariable absorbed)
│   ├── IStore.cs                                (RENAMED ← ISettingsStore.cs)
│   ├── Sqlite.cs                                (RENAMED ← SqliteSettingsStore.cs)
│   └── this.cs                                  (collection of Data values; Variables.RegisterNavigable wires %Settings.X%)
│
├── Snapshot/
│   ├── ISnapshot.cs                             (RENAMED ← ISnapshotted.cs, stage 15)
│   └── this.cs
│
├── Statics/                                     (App.GetStatic shim deleted — stage 5)
│   ├── this.Snapshot.cs
│   └── this.cs
│
├── Tester/                                      (RENAMED ← Test/, stage 17; Rule D — gerund→noun)
│   ├── Coverage.cs
│   ├── File.cs                                  (RENAMED ← TestFile.cs)
│   ├── Results.cs
│   ├── Run.cs                                   (RENAMED ← TestRun.cs)
│   ├── Status.cs                                (RENAMED ← TestStatus.cs)
│   ├── this.Snapshot.cs
│   └── this.cs
│
├── Types/                                       (Tier 5 stage 26 keystone: full type subsystem materialised)
│   ├── Choices/                                 (RELOCATED ← App/Choices/, Tier 5 stage 26; static class became instance @this; mounted as app.Types.Choices)
│   │   └── this.cs                              (_gate + _registry now instance fields)
│   ├── Conversion.cs                            (RENAMED ← Utils/TypeConverter.cs, Tier 5 stage 27; partial of Types.@this; pure-logic helpers stay static under Rule C exception)
│   ├── Registry.cs                              (RENAMED ← Utils/PlangTypeIndex.cs, Tier 5 stage 26; partial of Types.@this; static fields → instance state)
│   └── this.cs                                  (gains Clr(mimeType) overload stage 18; REWRITTEN Tier 5 stage 26 to absorb TypeMapping public API — state-touching methods instance, pure-logic helpers static)
│
├── Utils/                                       (Tier 5: empties out — destination tree achieved; exactly 4 files)
│   ├── CommandLineParser.cs
│   ├── PathExtension.cs
│   ├── RegisterStartupParameters.cs
│   └── StringDistance.cs
│   (Utils/Json.cs — DELETED Tier 5 stage 27; dispersed across http/Default, App.@this, Variables, Data, Builder, Diagnostics, Data/JsonString.cs)
│   (Utils/TypeMapping.cs — DELETED Tier 5 stage 26; public API absorbed into Types/this.cs)
│   (Utils/PlangTypeIndex.cs — RENAMED → Types/Registry.cs Tier 5 stage 26)
│   (Utils/TypeConverter.cs — RENAMED → Types/Conversion.cs Tier 5 stage 27)
│
│   (Utils/MimeTypes.cs — DELETED → split into Formats/this.cs + Types.Clr(mimeType), stage 18)
│   (Utils/ReservedKeywords.cs — DELETED → Variables/Reserved.cs, stage 16)
│
├── Variables/                                   (Navigators moved in from Data/, stage 21)
│   ├── Calls/
│   │   ├── Call/this.cs
│   │   └── this.cs
│   ├── Navigators/                              (MOVED ← App/Data/Navigators/, stage 21)
│   │   ├── Dictionary.cs                        (RENAMED ← DictionaryNavigator.cs)
│   │   ├── INavigator.cs                        (kept — interface)
│   │   ├── JsonString.cs                        (RENAMED ← JsonStringNavigator.cs)
│   │   ├── List.cs                              (RENAMED ← ListNavigator.cs)
│   │   ├── Object.cs                            (RENAMED ← ObjectNavigator.cs)
│   │   ├── ValueNavigators.cs                   (kept — plural; multiple value navigators in one file)
│   │   └── this.cs
│   ├── IRawNameResolvable.cs
│   ├── Reserved.cs                              (MOVED ← Utils/ReservedKeywords.cs, stage 16; const string)
│   ├── Variable.cs
│   ├── this.Snapshot.cs
│   ├── this.SnapshotAt.cs
│   └── this.cs                                  (gains RegisterNavigable mechanism — stage 13; Tier 5 stage 27: gains static-readonly _snapshotClone from Utils/Json.cs)
│
├── modules/                                     (per-handler files; only providers/→code/ relocation done in stage 19)
│   ├── Attributes.cs
│   ├── Events.cs
│   ├── IAction.cs
│   ├── IChannel.cs
│   ├── ICodeGenerated.cs
│   ├── IConfigure.cs
│   ├── IContext.cs
│   ├── IDataWrappable.cs
│   ├── IEvent.cs
│   ├── IModifier.cs
│   ├── IStatic.cs
│   ├── IStep.cs
│   ├── ModifierAttribute.cs
│   │
│   ├── app/run.cs
│   ├── assert/
│   │   ├── code/                                (RENAMED ← providers/, stage 19)
│   │   │   ├── Default.cs                       (RENAMED ← DefaultAssertProvider.cs)
│   │   │   └── IAssert.cs                       (RENAMED ← IAssertProvider.cs)
│   │   ├── AssertSnapshot.cs
│   │   └── {contains, equals, greaterThan, isFalse, isNotNull, isNull, isTrue, lessThan, notContains, notEquals}.cs
│   ├── builder/
│   │   ├── code/                                (RENAMED ← providers/)
│   │   │   ├── Default.cs                       (RENAMED ← DefaultBuilderProvider.cs)
│   │   │   └── IBuilder.cs                      (RENAMED ← IBuilderProvider.cs)
│   │   ├── BuildResponse.cs
│   │   └── {actions, app, appSave, enrichResponse, goals, goalsSave, merge, promoteGroups, types, validate, validateResponse}.cs
│   ├── cache/wrap.cs
│   ├── callback/run.cs
│   ├── channel/{remove, set}.cs
│   ├── code/{list, load, remove, setDefault}.cs       (NEW action handlers — register/load DLL flow)
│   ├── condition/
│   │   ├── code/
│   │   │   ├── Default.cs
│   │   │   └── IEvaluator.cs
│   │   ├── Operator.cs
│   │   └── {compare, else, elseif, if}.cs
│   ├── crypto/
│   │   ├── code/
│   │   │   ├── Default.cs                       (RENAMED ← DefaultProvider.cs)
│   │   │   └── ICrypto.cs                       (RENAMED ← ICryptoProvider.cs)
│   │   └── {decrypt, encrypt, hash, verify}.cs
│   ├── debug/tag.cs
│   ├── error/{handle, throw}.cs
│   ├── event/{on, remove, skipAction}.cs
│   ├── file/
│   │   ├── code/
│   │   │   ├── Default.cs
│   │   │   └── IFile.cs
│   │   └── {copy, delete, exists, list, move, read, save}.cs
│   ├── goal/{call, return}.cs
│   ├── http/
│   │   ├── code/                                (Tier 5 stages 25 + 27: statics evicted, instance fields _transportInOptions + _caseInsensitiveRead)
│   │   │   ├── Default.cs                       (RENAMED ← DefaultHttpProvider.cs; Tier 5 stage 25 _transportInOptions → instance, _jsonOptions deleted; Tier 5 stage 27 gains _caseInsensitiveRead instance from Utils/Json.cs)
│   │   │   └── IHttp.cs                         (RENAMED ← IHttpProvider.cs)
│   │   ├── Config.cs
│   │   └── {configure, download, request, types, upload}.cs
│   ├── identity/
│   │   ├── code/
│   │   │   ├── Default.cs
│   │   │   └── IIdentity.cs
│   │   └── {archive, create, export, get, list, rename, setDefault, types, unarchive}.cs
│   ├── list/{add, any, contains, count, first, flatten, get, group, indexof, join, last, range, remove, reverse, set, sort, split, types, unique}.cs
│   ├── llm/
│   │   ├── code/
│   │   │   ├── ILlm.cs                          (RENAMED ← ILlmProvider.cs)
│   │   │   └── OpenAi.cs                        (RENAMED ← OpenAiProvider.cs; _requestCount + cap DELETED, stage 16)
│   │   ├── LlmMessage.cs
│   │   ├── ToolCall.cs
│   │   └── query.cs
│   ├── loop/{foreach, types}.cs
│   ├── math/{MathHelper, abs, add, ceiling, divide, floor, max, min, modulo, multiply, power, random, round, sqrt, subtract, types}.cs
│   ├── mock/{action, reset, types, verify}.cs
│   ├── module/{add, remove, types}.cs
│   ├── output/{ask, types, write}.cs
│   ├── settings/{get, remove, set, types}.cs
│   ├── signing/
│   │   ├── code/
│   │   │   ├── Ed25519.cs                       (RENAMED ← Ed25519Provider.cs)
│   │   │   ├── IKey.cs
│   │   │   ├── ISigning.cs
│   │   │   └── KeyPair.cs
│   │   ├── Config.cs
│   │   ├── Signature.cs
│   │   └── {sign, verify}.cs
│   ├── test/{discover, report, run, tag}.cs
│   ├── timeout/after.cs
│   ├── timer/{end, sleep, start}.cs
│   ├── ui/
│   │   ├── code/
│   │   │   ├── Fluid.cs                         (RENAMED ← FluidProvider.cs)
│   │   │   └── ITemplate.cs                     (RENAMED ← ITemplateProvider.cs)
│   │   └── render.cs
│   └── variable/{clear, exists, get, remove, set}.cs
│
├── GlobalUsings.cs
├── Info.cs
├── View.cs
├── this.Snapshot.cs
└── this.cs                                      (681 → 596 lines; sum of stages 3, 4, 5, 6, 7, 10, 11, 12; Tier 5 stage 27 gains internal-static CamelCaseIndented from Utils/Json.cs)
```

## Comparison snapshot

| Subtree | Planned | Delivered |
|---------|---------|-----------|
| Folder renames (4+10 modules) | ✅ | ✅ |
| Channels/Serializers/{Filters,Plang}/ new subfolders | ✅ | ✅ |
| Modules/Schema/ + Spec/ (Catalog dissolution) | ✅ | ✅ |
| KeepAlive/ collection | ✅ | ✅ |
| Builder/ ← Build/, Tester/ ← Test/ | ✅ | ✅ |
| Code/ ← Providers/ + ICode marker | ✅ | ✅ |
| Variables/Navigators/ moved from Data/ | ✅ | ✅ |
| Settings reshape (collection-over-Data) | ✅ | ✅ |
| Variables/Reserved.cs | ✅ | ✅ |
| `app.Formats` mount | `Channels/Serializers/Formats/` | `App/Formats/` (root) |
| Utils/ "nearly empty" | 4 files | ✅ 4 files (Tier 5 stages 26+27) |
| Types/ partials (Registry, Conversion) | ✅ | ✅ Tier 5 stages 26+27 |
| Choices/ moved under Types/ | (new direction — added 2026-05-09) | ✅ Tier 5 stage 26 |
| Callback/Signature/ absorbed | ✅ | withdrawn — current shape is OBP-correct (Rule A would be violated by flattening) |
| Events/Lifecycle/ collapse | ✅ | folded into Events three-tier todo 2026-05-09 — Lifecycle is per-target view, not redundant nesting; structure question deferred to that design pass |
| CallStack/RestoredFrame.cs → Call/Position.cs | ✅ | ✅ Tier 5 stage 23 |
| Choices/ moved to Builder/Choices/ | ★ tentative | unchanged → Choices/ ultimately moved to Types/Choices/ (Tier 5 stage 26) |
| Callback/Wire/ subfolder | (new — added Tier 5) | ✅ Tier 5 stage 24 |
| Diagnostics/ subsystem | (new — added Tier 5) | ✅ Tier 5 stage 27 |
| Data/JsonString.cs | (new — added Tier 5) | ✅ Tier 5 stage 27 |

Full deviation analysis with reasons in `results.md`.

---

**Stats (final, all 27 stages):** 358 `.cs` files under `PLang/App/` · 13 new folders · 14 folder renames · 40+ file renames · 4 Utils files deleted in Tier 5 (Json.cs, TypeMapping.cs, PlangTypeIndex.cs renamed away, TypeConverter.cs renamed away) · 1 root folder deleted (Choices/, relocated). C# 2752/2752 + PLang 199/199 maintained across all 27 stages.
