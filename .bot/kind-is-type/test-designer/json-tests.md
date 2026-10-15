# kind-is-type stage 4 — json tests classification (decision 792)

Rule: a test of something a developer sees stays, rewritten to the new shape. A test of the old mechanism (ClrJson/JsonElement carrier, text holding json, size/length aliases) goes.

The new shape: json is `{item, kind: json}`; it holds raw bytes/text until first use; navigation parses once into a plang dict or list; after the read the value narrows (decision 787 — a value's type is what it is now); a list's size word is `count`.

| test (file:method) | verdict | new assertion (if kept) |
|---|---|---|
| `ComputedAndClrStoreNoContextTests.cs:ClrJson_NavigatesWithTheAskersContext` | retire | — the clr(JsonElement) carrier itself is going |
| `ClrKindNavigationTests.cs:ClrJsonElement_DerivesKindJson` | retire | — a json value isn't a clr(JsonElement) any more |
| `ClrKindNavigationTests.cs:ClrJson_SerializesAsRawJson_NoValueKindLeak` | retire | — the "ValueKind leak" is a JsonElement artifact; new shape serializes a dict/list directly |
| `ClrKindNavigationTests.cs:ClrJsonElement_NavigatesObjectArrayScalar_AsJson` | retire | — navigation-parses-once covered by `Navigation_IsTheNeed_ThatParsesJson` after rewrite |
| `LazyMaterialisationTests.cs:Navigation_IsTheNeed_ThatParsesJson` | keep | A Data of `{item, kind: json}` holding raw `{"port":8080}`: before navigation it's an item; `.Get("port")` yields `8080` and after the read the value is a plang dict |
| `Stage0_DataMaterializationTests.cs:Data_PropertyAccess_UsesDeclaredTypeForMaterialization` | keep | A Data of `{item, kind: json}` holding raw `{"a":1}`: `.Get("a")` yields `"1"`; the parse fires once |
| `TextOfKindJsonTests.cs:AfterNavigation_ItIsStillText_AndWritesItsCharacters` | retire | — says the opposite of the ruling: after the read a json value is a dict (787), not text |
| `TextOfKindJsonTests.cs:NavigatingIt_ParsesOnce_WhenFirstNavigated` | keep | A Data of `{item, kind: json}`: two `.Get` calls parse once (one call to the kind's decoder, or identity on the materialized value) |
| `TextOfKindJsonTests.cs:NavigatingCharactersThatArentJson_AnswersMaterializeFailed` | keep | A Data of `{item, kind: json}` holding `"{\"a\": 1,"`: `.Get("a")` fails with key `MaterializeFailed`, message names the line |
| `TextOfKindJsonTests.cs:Foreach_OverAJsonArray_YieldsItsElements` | keep | A Data of `{item, kind: json}` holding `"[1, 2, 3]"`: `EnumerateItems()` yields `"1"`, `"2"`, `"3"` (and `type.name` is `list` after the read) |
| `ActionNameWireReadTests.cs:WireKey_parameter_PopulatesParameter` | keep | Build the actions carrier as a plang list of dicts (not clr(json)); the same assertions hold — `Step[0].Code[0].Property[0].Name == "Data"` |
| `ActionNameWireReadTests.cs:WireKey_name_PopulatesActionName` | keep | Build the actions carrier as a plang list of dicts; `Step[0].Code[0].Module.Name == "output"`, `.Name == "write"` |
| `ActionNameWireReadTests.cs:ActionTypedRow_ReadsAsHeldAction_BornWithTheStep` | keep | Build the carrier as a plang list of dicts; the held action still reads with `Module.Name == "goal"`, `Name == "call"`, same `Step` identity |
| `ClrJsonActionsWriteTests.cs:ClrJsonActionsArray_WritesOntoStepActionsSlot_AsActionHosts` | retire | — the "write a clr(json) onto a slot" is the mechanism itself; the dev-visible outcome is in `ActionNameWireReadTests` after rewrite |
| `RequestActionTests.cs:Post_TextOfKindJson_IsSentVerbatim` | keep | Body is a Data of `{item, kind: json}` holding raw bytes; the request sends those bytes verbatim (unread) |
| `RequestActionTests.cs:Get_JsonResponse_ParsedCorrectly` | keep | The response value is a plang dict after first touch; `.name` reads `"Alice"` through plang navigation (no `JsonSerializer.Serialize` dance) |
| `JsonArrayCountNavTests.cs:JsonArray_Size` | retire | — `size` alias goes; the word is `count` |
| `JsonArrayCountNavTests.cs:JsonArray_Length` | retire | — `length` alias goes; the word is `count` |
| `ReferenceTests.cs:AUrl_DecodesByItsContentType` | keep | A url served `application/json` decodes to a value whose `type.name` is `item` and `type.kind.name` is `json` |
| `LlmIntegrationTests.cs:Query_JsonSchema_ParsedCorrectly` | keep | The query result's value is a plang dict holding the `sentiment` key through plang navigation; no `JsonElement` fallback |
| `QueryFormatTests.cs:Query_SchemaSet_JsonResponseParsed` | keep | The query result's value is a plang dict; `.sentiment` reads `"positive"` through plang navigation |
| `UploadActionTests.cs:Upload_ResponseParsed_AsJson` | keep | The upload response's value is a plang dict; `.id` reads `42` through plang navigation (no `JsonSerializer.Serialize`) |
| `RenderTests.cs:Render_AJsonFile_IsTextOfKindJson` | keep | A rendered `.json` file's `Type.Name` is `item` and `Type.kind.Name` is `json` (rename the test — `IsItemOfKindJson`); the inline template is still kindless |
| `TypeOwnedReadParityTests.cs:ObjectJsonRead_ProducesNavigableClrJson` | retire | — the clr(json) carrier is going; the dev-visible outcome (lazy navigation) is in `Navigation_IsTheNeed_ThatParsesJson` after rewrite |

Totals: 14 keep · 10 retire · 24 total.
