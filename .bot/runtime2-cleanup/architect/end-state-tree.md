# `PLang/App/` — Actual End-State Tree (post-cleanup)

What `PLang/App/` actually looks like as of `runtime2-cleanup` HEAD (commit `894a17ab`, after stage 19 landed). This is a snapshot of the delivered shape, with annotations showing what each subtree looked like before cleanup.

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
│   └── this.cs                                  (gains build-mode bootstrap from App.Start, stage 12)
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
│   │   ├── Tags/this.cs
│   │   ├── this.Snapshot.cs
│   │   └── this.cs                              (gains Call.ExecuteAsync, stage 10)
│   ├── Diff.cs
│   ├── Flags.cs
│   ├── RestoredFrame.cs                         (★ Tier 5 stage 23 — rename to Call/Position.cs)
│   ├── this.Snapshot.cs
│   └── this.cs
│
├── Callback/
│   ├── AskCallback.cs                           (★ Tier 5 stage 25 — _options static, Rule C)
│   ├── ErrorCallback.cs
│   ├── ICallback.cs
│   ├── Signature/this.cs                        (kept — OBP-correct; navigation app.Callback.Signature.Expires is the right shape, see results.md correction 2026-05-09)
│   └── this.cs                                  (ExpiresInMs → Expires/TimeSpan, stage 14)
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
├── Choices/                                     (★ tentative-move-not-done — planned ★ optional move to Builder/Choices/)
│   └── this.cs
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
│   ├── Properties.cs
│   ├── TString.cs
│   ├── this.Compare.cs
│   ├── this.Envelope.cs                         (_envelopeJsonOptions → instance, stage 16)
│   ├── this.Navigation.cs
│   ├── this.Result.cs
│   └── this.cs
│
├── Debug/
│   └── this.cs                                  (CallStack property moved out, stage 7)
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
│   ├── Lifecycle/                               (★ Tier 5 stage 24 — collapse: Lifecycle layer goes away)
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
├── Types/
│   └── this.cs                                  (gains Clr(mimeType) overload, stage 18; ★ Registry.cs/Conversion.cs partials deferred with stage 16)
│
├── Utils/                                       (★ Tier 5 — empties out across stages 28+29; 4 files still here)
│   ├── CommandLineParser.cs
│   ├── Json.cs                                  (★ Tier 5 stage 29 — DISPERSE to consumers)
│   ├── PathExtension.cs
│   ├── PlangTypeIndex.cs                        (★ Tier 5 stage 28 — absorb into Types/Registry.cs partial)
│   ├── RegisterStartupParameters.cs
│   ├── StringDistance.cs
│   ├── TypeConverter.cs                         (★ Tier 5 stage 29 — Types/Conversion.cs partial)
│   └── TypeMapping.cs                           (★ Tier 5 stage 28 — instance-bound; keystone)
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
│   └── this.cs                                  (gains RegisterNavigable mechanism — stage 13)
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
│   │   ├── code/                                (★ Tier 5 stage 26 — _jsonOptions, _transportInOptions still static, Rule C)
│   │   │   ├── Default.cs                       (RENAMED ← DefaultHttpProvider.cs)
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
└── this.cs                                      (681 → 596 lines; sum of stages 3, 4, 5, 6, 7, 10, 11, 12)
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
| Utils/ "nearly empty" | 4 files | 8 files (★ Tier 5 stages 28–29) |
| Types/ partials (Registry, Conversion) | ✅ | ★ Tier 5 stages 28–29 |
| Callback/Signature/ absorbed | ✅ | withdrawn — current shape is OBP-correct (Rule A would be violated by flattening) |
| Events/Lifecycle/ collapse | ✅ | ★ Tier 5 stage 24 |
| CallStack/RestoredFrame.cs → Call/Position.cs | ✅ | ★ Tier 5 stage 23 |
| Choices/ moved to Builder/Choices/ | ★ tentative | unchanged (correct call) |

Full deviation analysis with reasons in `results.md`.

---

**Stats:** 354 `.cs` files under `PLang/App/` · 9 new folders · 14 folder renames · 40+ file renames. C# 2752/2752 + PLang 199/199 throughout.
