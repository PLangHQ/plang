# Handoff — delete `TryConvert` + the CLR `Convert` door + strip the 13 `Json.cs`; re-home conversion tests to `Types`

Branch: `navigation-driven-record-builder`. This is a fresh-context handoff — everything needed is below. Execute it as one Stage-2 close-out. **Watch over-building/testing** (Ingi): batch edits, build ONCE per batch, test only the ONE relevant suite (not all).

## READ FIRST (in this order)
1. `.bot/navigation-driven-record-builder/architect/stage2-tryconvert-json-sweep-answer.md` — the ruling (dispositions per `TryConvert` arm, the demolition list, order of work). **Authoritative.**
2. `.bot/navigation-driven-record-builder/coder/stage2-tryconvert-entangled-with-json-sweep.md` — the coder discovery that opened this.
3. `PLang/app/type/list/Conversion.cs` — the whole file (TryConvert `:131-518`, public `Convert` door `:74-79`, `ConvertElementsInto` `:96-123`, `GoalReadOptions`/`ContextualReadOptions`/`_caseInsensitiveRead`/`CaseInsensitiveRead` `:34-63`, message helpers `:527-599`, `GetListElementType`).
4. `PLang/app/type/item/kind/reflection/this.cs` — the `*` kind. The NEW `Read(dict slots, Type target, ctx)` door already lives here (~`:95`, beside `Read<TReader>`). It's the dict→record home.
5. `PLang/app/type/this.cs` — the entity `Create` doors: content `Create(object?, context)` `:247` (defers a raw string to a lazy source; eager-retypes an item), courier `Create(object?, data)` `:331`. `WireReader` `:458` (FromWire dispatch — kill conditional on feeder inventory).

## ALREADY DONE (committed + pushed, green vs nav baseline)
- **Step 1 — callers off the CLR `Convert` door:** `setting.Set` (lift-then-self-lower `App.Type.Create(kvp.Value,ctx).Clr(prop.PropertyType)` at the C# config edge), `ToGoalCall` (`goal.call` entity courier — carrier kept: callers skip on null, courier returns null without throwing), `build:985` param-normalize (`p.Type.Create(p.Peek(), context)` — target builds itself, right kind, throw-on-decline caught by the loop).
- **dict→record door built** on `reflection.@this` + `dict.Clr`'s STJ record fallback (`dict/this.cs:342` `Clr`) rerouted to it. `dict.Json` firing site dead. Consumers verified all settable-prop (`Goal`/`step`/`actions` hosts, `Identity` — parameterless ctor).
- **`type/this.cs:304` no-family-hook fallback throws** (was `TryConvert`) — this was `TryConvert`'s LAST production caller.

**Net state: `TryConvert` has 0 production callers.** Only 14 test call sites + the `TypeMappingTestFacade.ConvertTo` wrapper remain. The public `Convert` door is caller-less.

## DECISIONS LOCKED (Ingi)
- **Conversion/type tests → the `Types` project, consolidated.** *"It's the types that do the convert, not Data — data just forwards."* They currently scatter in **Runtime** (`App/Utility/`, `App/Engine/Utility/`) and **Modules** (`builder/`) — that's the smell. `Types` already has the right homes (`App/ConversionGapTests/`, `App/Types/PathTests/`).
- **Dedup dict→record.** It's tested in THREE places today — all now hit the one reflection door: `Types/App/ConversionGapTests/DictListToRecordTests.cs`, `Runtime/App/Engine/Utility/TypeMappingDictConversionTests.cs` (×9), `Modules/App/Modules/builder/StepFromDictConversionTests.cs` (×3). Consolidate to ONE dict→record suite in `Types` that tests `new reflection.@this().Read(dict, target, ctx)`.
- **CLR door dies WHOLE, not slimmed** (architect + Ingi: "everything is an item; kill the clr door"). No `Convert(value, clrTarget)` survives — values self-lower (`item.Clr`), types self-build (`Create`).
- **`TypeMappingTestFacade.ConvertTo` retires** — it only wrapped `TryConvert`. Its callers re-point to the real doors (see below) or the tests move/dedup.
- **`item/serializer/json.cs` STAYS** (ruling 8 — in-memory narrow). `object/serializer/json.cs` already deleted.

## THE WORK (order)

### A. Relocate the residue (so nothing needs `TryConvert`), then delete it
Per the architect's disposition table (§ "Residue dispositions"):
- **enum leaf** (`Conversion.cs:415-432`) → the value's own `Clr` (text→`Enum.Parse`, number→`Enum.ToObject`). `reflection.ReadValue` (`kind/reflection/this.cs:108-111`) already does this at the token level — mirror it value-side in `item.Clr`/`text.Clr`/`number.Clr` if not already covered.
- **CLR `List<T>` construction + scalar-wrap** (`:325-373` + `ConvertElementsInto :96-123`) → the **list kind** (it claims `IList`). ⚠ The comment at `:314-324` records that routing this through STJ **broke 9 tests** (scalar-wrap `5→[5]`, generic-only `IList<T>` sources) — those behaviors MOVE to the list kind, they don't die.
- **`FromWire` dispatch** (`:254-262` + `type/this.cs:458` `WireReader`) → feeders call the type's OWN static directly (`snapshot.FromWire` — `module/snapshot/resume.cs`; `crypto/type/hash/this.cs:80`; `signature/this.Wire.cs`). If the feeder inventory shows the only generic dispatch was via now-dead callers, `WireReader` dies too.
- **string→record arm** (`:269-309`, incl `:282`+`:293`) and **container→record arm** (`:463-497`): **DIE — nothing reroutes.** A bare undeclared string hitting a record target is a **producer bug** (no `{`/`[` sniffing). Inventory what still feeds them live (known feeders already dead: `.pr→Goal` went with the Stage-1 read path; `source`/`wire` go through their kinds). Fix any live birth site (declare the kind), not the door.
- **primitive `ChangeType`**, **string-ctor arm** (`:379-410`), **`data.@this` target** (`:153-159`), **string→`JsonNode`** (`:240-245`) → die / inventory per the table (string→JsonNode → the json kind's `Parse`).

Then delete: `TryConvert` (`:131-518`), the public `Convert` door (`:74-79`), `ConvertElementsInto`, `GoalReadOptions`/`ContextualReadOptions`/`_caseInsensitiveRead`/`CaseInsensitiveRead`, the message helpers (move the authoring — slot naming, plang type label, the `%var%` hint — to wherever the failure now reports, e.g. `item.Clr` failures; **don't lose the `%var%` hint**). Audit every remaining member of the `Conversion.cs` partial — nothing survives unaccounted.

### B. Re-home + dedup the tests into `Types`
The 14 direct `TryConvert` sites + the facade's ~34 `ConvertTo` sites, by what they test:
- **dict→record** — `Types/ConversionGapTests/DictListToRecordTests.cs:40`, `Runtime/App/Engine/Utility/TypeMappingDictConversionTests.cs` (9), `Modules/App/Modules/builder/StepFromDictConversionTests.cs` (3). → ONE consolidated suite in `Types`, testing the reflection door.
- **scalar (string→int)** — `Runtime/App/Types/RegistryFoldTests.cs:90` → the entity `Create`/`item.Clr` (`App.Type.Create("42",ctx).Clr<int>()`), in `Types`.
- **string→`PLangPath`** — `Types/App/Types/PathTests/PathTypeMapperTests.cs` (×5) → `path.Create`/`path.Resolve` (already in `Types` — just re-point off `TryConvert`).
- **error messages** — `Runtime/App/Utility/TypeMismatchMessageTests.cs` (7), `TypeMismatchExample.cs` → move to `Types`, re-point to wherever the message authoring lands (A).
- **type-NAME→CLR registry** — `Runtime/App/Utility/TypeMappingTests.cs` (~90 `GetType_*` tests — NOT conversion; they test `App.Type.Get(name)`). Move to `Types` too (a registry concern), but they don't touch `TryConvert` except a few — those few re-point.
- **test-shared** — `Shared/Support/TypeMappingTestFacade.cs` (`TypeMapping` + `TypeConverter` twins) + `Shared/DataReadExtensions.cs:19`. `ConvertTo`/`TryConvertTo` retire; `DataReadExtensions` uses the real door.

### C. Strip the converters
Delete the 13 `type/item/<name>/Json.cs` + their `[JsonConverter]` attribute lines + the type entity's own converter (`type/this.cs:32` — verify). `dict/Json.cs` only after A's firing sites are gone (already unhooked). NOT `item/serializer/json.cs`. Goal: zero `Json.cs` under `type/item/` except `kind/json/`.

## VERIFY
- Re-capture a nav baseline at start (`./dev.sh test <Suite>` per suite → fail-name lists). Current nav is green except the known parallel-flaky set (Wire ~17, Data ~19, Runtime ~1 — all verified flaky in the wire-source-split work; not regressions).
- Lean: build ONCE per batch; test the ONE relevant suite (`Types` for the conversion work; `Modules` only for the builder dict→step; a broad Types+Data+Modules+Runtime pass ONCE at the end).
- Success: `TryConvert`/`Convert` door/`ConvertElementsInto`/the options gone; 13 `Json.cs` gone; conversion tests consolidated in `Types`; zero new deterministic reds vs the re-captured baseline.
