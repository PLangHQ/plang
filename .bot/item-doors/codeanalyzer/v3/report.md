# v3 report: OBP scan before merge (decision 269)

Scope: `git diff 1adfb9555..HEAD -- PLang PLang.Generators PlangConsole` (431 files); weight on the 191 files changed since b96c60765. Read only, nothing built or run. Three region readers (type/item, type core + app root, goal/module/actor) scanned from my added-lines index; I spot-checked the top findings against the code (line numbers below were re-grepped, the rest are the readers' and marked "reader"). Accepted and skipped: Executor mode table, debug view's weak table, the two `Code.Get<IStore>()` sites, `Apply`/`Save` two input forms.

**Verdict: NEEDS WORK, FAIL (coder next).** The reshape from v2 held. Closed since v2: R3 (`Own` hook), R4 (`Face` virtual), R6 (`Tagged` asks `view.Tagged`), R8 (view default), R5a (`Of(kind, context, template)`, 211a). Still present: R1 (moved into the plang format), R2, R5b/c/d, R7. New weight is a throwaway-Data pattern and name words.

## Findings, ranked

### 1. "Kept or used?" asked at four holders (v2 R2, still present). fork + stored twice
- `data/this.Output.cs:55` `if (Error == null && HasVariable) { … writer.View.Write(this, writer, layer) … }`
- `type/property/this.cs:224` `if (Value is { HasVariable: true } template) { View.Write(template…); View.Variables(template…) }`
- `type/item/wire/kind/plang/writer.cs:59` `if (record.Peek() is { HasVariable: true } value) await View.Variables(value, this);`
- `View.cs:136` `Store.Variables` asks `!value.HasVariable` again; `View.Write(data,…)` (View.cs:62-67) repeats Data.Output's tail.
- **Root:** the view is always asked, unconditionally; a view writes a value with no variable as plain `value.Output`. `Variables` keeps its one guard; the writer and the row stop asking. The `_outputDepth` guard moves into `View.Out`, where the value is used.

### 2. The view is compared by constant in the plang format and the signature reader (v2 R1, moved). fork + one answer stored three times
- `wire/kind/plang/this.cs:44` `StoreView = new data<bool>("", view == View.Store, …)` (a flag in a Data property)
- `:81` `var window = view == View.Store ? null : (await …Expiry)`; `:99` `else if (view == View.Out)` refuses unsigned
- `data/schema/signature.cs:95-104` `var stored = ctx.View == View.Store; … stored ? Origin.Stored : Origin.Live, stored ? null : ctx.Window`
- "From our own store" lives as `view`, as `Origin`, and as `Window == null`.
- **Root:** the view answers `Window(context)`, `Origin`, `RequiresSignature`; the format and `signature` ask it and compare nothing. (`:62,106` also `catch … when (ex is JsonException or NotSupportedException)`, a type switch in a catch; the error rule wants `catch (AppException)`.)

### 3. Throwaway Data built to carry a refusal or read one member. Ingi's list; stray helper + missing owner
Reader counts: ~29 `new data("", …)` constructions, 22 under type/item.
- Decline carrier, read only for `.Error`: `duration/this.cs:37`, `duration/this.Parse.cs:16`, `size/this.Parse.cs:19`, `size/this.cs:31`, `duration/serializer/Reader.cs:22`, `size/serializer/Reader.cs:22`, `setting/this.cs:206,239`, `list/this.Generic.cs:92`, `type/this.cs:359,376,398,409,421`, `base64/this.cs:149`, `kind/json/source.cs:49`.
- Built to open/settle one value: `View.cs:163,218`; `item/source.cs:154` (`new data("", this)` for `.Value()`); `type/property/this.cs:191` (`IsDefault`).
- Other throwaways: `View.cs:97` `new VariableNotFoundException(name).Error`; `type/list/this.cs:46` a whole `new type(...)` for an empty kind.
- `list/this.Generic.cs:93-97` uses both failure channels for one call (`catch DeclinedException` beside Data `Fail`).
- **Root:** `type` already has a Data-returning door (`Made`, type/this.cs:314); callers use it and read the error from the answer. `error` owns `VariableNotFound(name)`; a value settles/opens itself. No locked-core addition needed for the Data cases; `Make` answering value-or-error is a `type` change, so a proposal.

### 4. Sentinels
- `new data("", …)`, name `""` = "unnamed", ~29 sites. Root: an unnamed constructor.
- `View.cs:220` `used.Error is { Key: "VariableNotFound" }`. Root: error answers `IsVariableNotFound`.
- `data/this.Navigation.cs:57` NotFound status doubles as "child cannot hold it, keep beside". Root: the target answers `Holds(key)`.
- `kind.IsEmpty => Name.Length == 0` with `string? kind` passed as null/`""` (type/list/this.cs:44-46, type/this.cs:116,151); `string name = ""` defaults (kind/this.cs:265,271). Root: the empty kind is an object callers pass.
- `base64/this.cs:153,156` `_value ?? ""`, null = "not encoded yet". `dict/key/this.cs:53` `First ?? ""` while `Write` writes null for the same case.
- Empty text = "not given": `module/debug/this.cs:61,188`, `list/type/query/group/this.cs:22`, `order/key/this.cs:47`. `"?"` = no goal/step at `debug/this.cs:202-218,334,349`. `test/this.cs:196 Failure` returns `""`. Root: the setting's `Goal` answers `Matches(name)`; goal/step answer a label.
- `size`/`duration` with an empty kind = "ask the setting" (reader, size/duration). Root: the value is born in a standard.
- `debug/this.cs:199 Watch(variable, string change, data? stored)` carries "SET"/"DELETED" as a string and null as the same fact.

### 5. Row key packed into a Data's name (second answer in `Name`) + throwaway instances for facts of the class. `actor/setting/this.cs`
- `:190,205` build `"<actor>!<path>"`; `:254,268-269` parse it back with `StartsWith` and a slice; `Name.ToLowerInvariant()` three times (raw hand-off). Root: the actor answers its row key and prefix, or the store takes a partition.
- `:171` `Make(path)!.Paths` and `:375,388` `Activator.CreateInstance(clr, …).Path` build an instance per settings class to read its address (reader; line numbers re-grepped). Same shape as v2 R5a. Root: the class/type entity answers its address and paths; locked-core proposal.
- `_classes` and `_addresses` mirror each other (`:350-351`), stored twice.

### 6. Type checks that should be behaviour on the type
- `ReferenceEquals` (each is "is this X?"): `number/this.Arithmetic.cs:43` "did the op return an operand?" (root: the operation answers its winning kind; the comment admits it); `number/this.cs:199`; `dict/this.cs:306`; `item/source.cs:145,155,224` (root: `Read` answers null when it parsed nothing); `type/this.cs:194` `ReferenceEquals(first.Copy(), first)` (root: item answers `IsShared`); `format/item/reader.cs:91`; `data/this.Navigation.cs:68`; `test/list/this.cs:43` (verified; root: `Actor()` answers the same actor, or actor has equality).
- `typeof` comparison "is the plain list?" at `list/this.cs:125`, `list/this.Generic.cs:52,139`; `list/this.cs:89` `value is clr or dict`. Root: one virtual on the node lists.
- `Registry.cs:234,258` `kind.Owner?.kind is empty root`; `:325` `if (t == typeof(item.list.@this)) Hold(...)`, a type-switch in a registry (root: the list declares its own kind class); `type/property/this.cs:92,119` `switch (Member)` with `IsMethod => Arguments != null` as a second answer; `type/this.cs:291-296` `Held` switches on `source` vs `leaf`.
- `module/debug/this.cs:76-153` nine `Bind` sites filter with `item is goal.@this` after `Own()` already limited the type, then cast again (root: a typed `Bind<T>`; event door is locked, proposal). `debug/this.cs:88` `Provider is OpenAi` (root: hooks on `ILlm`). `channel/format/this.cs:22 IsPlang => Kind is plang` (root: kind answers). `actor/setting/this.cs:239-247 Option` type-switch; `:338 option.GetValue(held) is setting owned` (root: the option copies itself). `module/variable/set.cs:73`, `error/throw.cs:90` (dead if the typed list refuses non-errors: deletion test), `archive/type/archive/this.cs:38,53,75`, `crypto/type/hash/this.cs:113` + `hash/serializer/Default.cs:16-18` (copied kind lookup; root: typed kind lookup on the type).
- `variable/code/Hop.cs`: `Entry.Step` switches on the strings `"variable"/"property"/"index"/"method"`; `:47 root is not code.Variable`. Root: each hop kind reads itself.
- `item/source.cs:231` special-cases by literal name (`Type.Name.ToLowerInvariant() is "dict" or "list"`), throws `InvalidOperationException` at `:233` to be caught at `:247` by its own `catch … when (ex is …)`: exception as control flow. The 95-line `Value` is a fork with workarounds. Root: the type answers whether the item fits.

### 7. Copied shapes (fork / flat copy)
- `size`/`duration`: R5b (`as <standard>` fork, `duration/this.cs:89`, `size/this.cs:71`), R5c (`iec`/`si` the same class twice), R5d (`duration/this.cs:101` implicit operator names `@short`; branch-narrating comments at `short/this.cs:11-13`, `item/this.cs:395-397`). New: `duration.Resolve` (`this.Parse.cs:15`) loops `Make` over every standard (a fork); the unwrap `(raw is item leaf ? leaf.Clr<object>() : raw) is string text` is copied at `duration/this.cs:48,59,75`, `size/this.cs:53,84,96`, `list/this.cs:85`, `image/this.cs:153` (root: item answers it once); R7 CLR-number switch copied again at `size/this.cs:54-61`.
- `Executor.cs:61-79` and `module/terminal/type/program/this.cs:175-180` repeat the same command-line walk (Find → Apply → Entries → Set). Root: `actor.setting` takes the parsed flags (`child.Apply(parameters)`).
- `goal/step/action/this.Item.cs:38-43` and `module/this.cs:203-209` build the same settings-or-fallback Data. Root: `actor.setting.Get(address)` answers.
- `dict/key/this.cs` and `input/modifier/list/this.cs` are the same type (first-of-collection value). Missing base.
- `wire/kind/plang/writer.cs` is ~20 one-line forwards to the json writer it holds (middleman). `kind/json/source.cs:53-63` and `wire/this.cs:58-67` repeat four identical `writer.Raw` bodies.
- The open-generic interface probe (`GetInterfaces()…GetGenericTypeDefinition() == typeof(X<>)`) is copied ~11 times: `Registry.cs:273,284`, `type/this.cs:551,569`, `property/this.cs:43`, `list/this.cs:225,254,316,320`, `node/this.cs:41`, `actor/setting/this.cs:302`, `test/format/this.cs:15`. Root: one helper on the type side.
- `View.Variables` holder: `Hop.cs` threads `bool own` through `Write`/`Set` (dropped on the next hop; abstract `IsOwn` is a role word); `Variable.Read` (Variable.cs:52-57) duplicates `Hop.Read` and fails the deletion test.

### 8. Raw hand-off
- Call settings loaded by every caller: `goal/this.cs:414`, `step/this.cs:130`, `action/this.cs:198`, `step/list/this.cs:176-178`, `OpenAi.cs:417,528,540,564`, then `Push(x, setting)`. Root: `call.list` reads them synchronously from the built cache, or one `Scope(...)` entry reads them. `call/list/this.cs:177` passes positional nulls through a private overload that builds three kinds of frame; each kind should build itself. `Timing()/Tags()/History()` at `:81-88` are middlemen over `Setting()` (`call` is locked).
- `Executor.cs:118,149` `TrimStart('/', '\\')` at two consumers.

### 9. Locked-core additions without a decision I could find
- `item` has five per-writer virtuals now (`IWriter`, text, formal, json, plang); the json and plang overloads came after v2. Item's base virtuals are locked: please confirm a decision. (Reader.)
- Decisions found for: `kind.View/Make`, `type.Face` (194); `type.list.Of` (198/211a); `Data.Settle(View)` (254a/256/257); `store.code.IStore` (268/270). `Data.Kept`/`Carried`: only list-row decisions 267/274/275, both internal.

### 10. Names (Ingi's list), by file
Role/origin words: `View.cs` `Declares/Undeclared/Written/Settled/Used/Opened/Unread/Settle`; `Sensitive(string)` (adjective as verb, ignores its parameter at `formal/writer.cs:90`); `Variables(value, writer)` (noun that writes). `type/this.cs` `Held/Made/Born/BornKind/Taught/Teaching/Claims/Identify/Construct`; `type/list` `Held(string)` shares a name with `type.Held`, `Full`. `Registry.cs` `Admit/Hold/Enlist/Guard/Clash/Taken/Bound/Reading`; locals `found`, `held` (`Registry.cs:155`, `node/this.cs:78`). `node` `Own` vs `kind.Own` (unrelated). `property` `Holding/Noted`. `data/carried.cs` (participle) and `Kept`. `shortcut/list` `Shortcut` returns the type entity. `actor/setting` `_held/_unread/Saved/Own/Owner/Run/Under` and locals `held/kept/saved/own/made/built`, `isOwnPath`. `call` `held` parameter (`list:181,188`, `binding`, `isolated`, `this.cs:213`). `Held` property (`action/this.Build.cs:11`), `Held` goal lookup (`goal/list:142`), local function (`goal/this.cs:259`), bool method (`step/this.Validate.cs:105`). `archive/type/archive/held`. `formal/reader.cs` `declared/written/holdsRecords`. `variable/set.cs:73`, `ui/code/Fluid.cs:124` `held`. `null/this.cs:27 declared`, `parser/this.cs:39 written`, `json/source.cs:15 built`, `list.Holding`. `choice/set/family` and `setting/this.cs:28,237 declared`, `:296 own`, `:150 written`.
Verb+noun/noun+verb/coined: `SpeaksPlang` (`program/this.cs:169`), `AppendStepVariables`, `FormatValue`, `WriteFiltered`, `WriteLlmBlock` (`debug/this.cs`), `Fresh` (middleman for `Make`, `actor/setting:395`), `Mark(goal)`/`Mark(step)` (two meanings), `Settle` beside `Settled`, `ValidateKind` returning an `(ok, actual)` tuple. `Enabled`/`Disabled` as two members of one fact with a reflective `Switch(key)`.

### 11. Smaller
- `debug/this.cs:216-359` eight methods are `static` and fetch `context.App?.Debug` to touch instance state (two ways in). Make them instance methods. `:38` `Write(object?)` wraps `message is Data d ? d : Ok(message)`, local `envelope`.
- `goal/this.cs:71 File` is `[Store, Out]` and mirrors `Path.Name` (flat copy). `Executor.cs:133-135` copies the build setting's `Cache` into the llm setting.
- `terminal/type/process/this.cs:103`, `program/this.Run.cs:85` wrap a value in a throwaway Data and pass a bare `null`.
- `goal/step/list/this.cs:176-178` `goal != null ? Push(…) : null` three times (null check on a reference).
- `formal/reader.cs:488-499` builds JSON by string concatenation (translates one shape to another).
- `type/this.cs:783` and `type/clr/this.cs` choose an overload with a dummy bool (`known`, `bool _`).
- `Data.Context` is nullable and re-checked ~14 times (`dict/key/this.cs:36,41`, `dict/this.cs:131`, `modifier/list:53`, `number/this.cs:166,207`, `text/this.cs:255`, `directory:39`, `parallel:62`, `prose:43`, `sensitive:37`, `variable/serializer/Entry.cs:49,80`, `source.cs:163`). Root: Data is born with its context.
- The "born without a type, type rebuilt from its class" door is open at 10+ sites (`item/this.cs:398`, `text:326`, `bool:31`, `choice:43`, `number:40`, `separator:39`, `short:13`, `hash:47`, `kind/reflection:16`), plus `error.Type`/`event.Type` mint a type per call (declared debt).
- Doc comments stacked: `data/this.cs:128-135`, `property/this.cs:181-185`, `modifier/list:57-58`; orphaned at `type/list/this.cs:281-285`.
- History/task comments: `actor/setting` ("774 A", "790", "789"), `debug/this.cs:41` ("Stage 6: was …"), `step/this.cs:169`, `family/this.cs:15,39` ("stage 6"), `source.cs` ("decision 818/758", "ruling 6"), `setting/this.cs:52,81`.
- `Apply(object node,…)` (`setting/this.cs:164-253`) does two jobs (set, then fill defaults by reflection) with `catch … when (ex is InvalidCastException or …)` at `:217`.
- `Plng005.cs` copies Plng004's shape; sibling-consistent, tolerated.

## Checked, clean (explicit no)
Pairs, `IOutput`, `WriteReflected`, PLNG006: gone. `lock (other.X)`: none; the `dict`/`list` locks are their own. `Context ??=`/`new X { Context = }`: none (only `_answers ??=`, `_note ??=` lazy caches). `.Value is/as` mid-flight: none in the added lines; `.Clr` stays at real boundaries (`Fluid.cs:362`, headless browser, `program/this.cs:140`). `Console.*`: none. Envelope (`["Value"]`): none. Naked public collections: none (`kept.list` is typed). `actor.setting` throws carry `AppException` and `Get` catches `AppException`. `PlangConsole`: no change since b96c60765. Registry `Admit`/`_named` snapshot replaced whole. `Data` carried copy takes both bags. `warning` is a real item with a typed list.

## Not verified by me
Whole-file reads I did myself: none beyond the grep spot-checks above; the readers read `View.cs`, `type/this.cs`, `type/kind`, `type/list`, `Registry.cs`, `data/this.Output.cs`, `property`, plang format, `actor/setting`, `debug`, `call/list`, `Executor.cs` whole. Findings marked "reader" without a re-grep may carry a line off by a few; the shape holds. I did not build or run tests.
