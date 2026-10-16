# kind-is-type stage 4 — json tests classification (decision 792)

Rule: a test of something a developer sees stays, rewritten to the new shape. A test of the old mechanism (ClrJson/JsonElement carrier, text holding json, size/length aliases) goes.

The new shape: json is `{item, kind: json}`; it holds raw bytes/text until first use; navigation parses once into a plang dict or list; after the read the value narrows (decision 787 — a value's type is what it is now); a list's size word is `count`.

Rewrites: each kept test lives in its OWN new file; the retired tests are left for the coder to delete with the navigator.

| test (file:method) | verdict | new assertion (if kept) · new file | red/green today |
|---|---|---|---|
| `ComputedAndClrStoreNoContextTests.cs:ClrJson_NavigatesWithTheAskersContext` | retire | — the clr(JsonElement) carrier itself is going | — |
| `ClrKindNavigationTests.cs:ClrJsonElement_DerivesKindJson` | retire | — a json value isn't a clr(JsonElement) any more | — |
| `ClrKindNavigationTests.cs:ClrJson_SerializesAsRawJson_NoValueKindLeak` | retire | — the "ValueKind leak" is a JsonElement artifact; new shape serializes a dict/list directly | — |
| `ClrKindNavigationTests.cs:ClrJsonElement_NavigatesObjectArrayScalar_AsJson` | retire | — navigation-parses-once covered by `JsonLazyNavigationTests` | — |
| `LazyMaterialisationTests.cs:Navigation_IsTheNeed_ThatParsesJson` | keep | A Data of `{item, kind: json}` holding raw `{"port":8080}`: before navigation it's an item; `.Get("port")` yields `8080`; after the read the value is a plang dict. · `Data/App/LazyDeserialize/LazyDataTests/JsonLazyNavigationTests.cs` | **green** |
| `Stage0_DataMaterializationTests.cs:Data_PropertyAccess_UsesDeclaredTypeForMaterialization` | keep | A Data of `{item, kind: json}` holding raw `{"a":1}`: `.Get("a")` yields a plang number `1`; the parse fires once. · `Types/App/TypedReturnsTests/JsonPropertyAccessMaterializesTests.cs` | **green** |
| `TextOfKindJsonTests.cs:AfterNavigation_ItIsStillText_AndWritesItsCharacters` | retire | — says the opposite of the ruling: after the read a json value is a dict (787), not text | — |
| `TextOfKindJsonTests.cs:NavigatingIt_ParsesOnce_WhenFirstNavigated` | keep | A Data of `{item, kind: json}`: two `.Get` calls, MaterializeCount == 1. · `Types/App/TypeKindStrict/ItemTypeTests/JsonParsesOnceTests.cs` | **green** |
| `TextOfKindJsonTests.cs:NavigatingCharactersThatArentJson_AnswersMaterializeFailed` | keep | A Data of `{item, kind: json}` holding `"{\"a\": 1,"`: `.Get("a")` fails with key `MaterializeFailed`, message names the line. · `Types/App/TypeKindStrict/ItemTypeTests/JsonMalformedMaterializeFailedTests.cs` | **green** |
| `TextOfKindJsonTests.cs:Foreach_OverAJsonArray_YieldsItsElements` | keep | A Data of `{item, kind: json}` holding `"[1, 2, 3]"`: `EnumerateItems()` yields plang numbers `1, 2, 3`; after the read `type.name` is `list`. · `Types/App/TypeKindStrict/ItemTypeTests/JsonArrayForeachTests.cs` | **red** — Expected 3 items, Actual 1 (EnumerateItems still treats the raw as one text, no per-element parse) |
| `ActionNameWireReadTests.cs:WireKey_parameter_PopulatesParameter` | keep | Build the actions carrier as a plang list of dicts (not clr(json)); `Step[0].Code[0].Property[0].Name == "Data"`. · `Modules/App/Modules/builder/ActionNameWireRead_PlangListTests.cs` (shared) | **red** — Property.Count 0 (the Code slot reader doesn't yet recognize a plang list<dict> carrier) |
| `ActionNameWireReadTests.cs:WireKey_name_PopulatesActionName` | keep | Plang list of dicts; `Module.Name == "output"`, `Name == "write"`. · same file | **red** — Code.Count 0 (same producer-not-migrated reason) |
| `ActionNameWireReadTests.cs:ActionTypedRow_ReadsAsHeldAction_BornWithTheStep` | keep | Plang list of dicts; the held action reads with `Module.Name == "goal"`, `Name == "call"`, same `Step` identity. · same file | **red** — Code.Count 0 (same reason) |
| `ClrJsonActionsWriteTests.cs:ClrJsonActionsArray_WritesOntoStepActionsSlot_AsActionHosts` | retire | — the "write a clr(json) onto a slot" is the mechanism itself; the dev-visible outcome is in `ActionNameWireRead_PlangListTests` | — |
| `RequestActionTests.cs:Post_TextOfKindJson_IsSentVerbatim` | keep | Body is a Data of `{item, kind: json}` holding raw bytes; the request sends those bytes verbatim, unread (body stays an item until something reads it). · `Modules/App/Modules/http/PostItemOfKindJsonVerbatimTests.cs` | **red** — body re-serialized to compact form (producer still parses then re-encodes; raw-until-first-use not yet honoured) |
| `RequestActionTests.cs:Get_JsonResponse_ParsedCorrectly` | keep | The response value is a plang dict after first touch; `.name` reads `"Alice"` through plang navigation. · `Modules/App/Modules/http/GetJsonResponseParsesToPlangDictTests.cs` | **green** |
| `JsonArrayCountNavTests.cs:JsonArray_Size` | retire | — `size` alias goes; the word is `count` | — |
| `JsonArrayCountNavTests.cs:JsonArray_Length` | retire | — `length` alias goes; the word is `count` | — |
| `ReferenceTests.cs:AUrl_DecodesByItsContentType` | keep | A url served `application/json` decodes to a value whose `type.name` is `item` and `type.kind.name` is `json`. · `Types/App/Types/UrlDecodesByContentTypeJsonTests.cs` | **red** — Expected "item", Actual "dict" (producer narrows directly to dict today, bypassing the `{item, kind: json}` resting form) |
| `LlmIntegrationTests.cs:Query_JsonSchema_ParsedCorrectly` | keep | The query result's value is a plang dict holding `sentiment` through plang navigation; no `JsonElement` fallback. · `Modules/App/Modules/llm/QueryJsonSchemaPlangDictTests.cs` | **green** |
| `QueryFormatTests.cs:Query_SchemaSet_JsonResponseParsed` | keep | The query result's value is a plang dict; `.sentiment` reads `"positive"` through plang navigation. · `Modules/App/Modules/llm/QuerySchemaSetPlangDictTests.cs` | **green** |
| `UploadActionTests.cs:Upload_ResponseParsed_AsJson` | keep | The upload response's value is a plang dict; `.id` reads `42` (a plang number) through plang navigation. · `Modules/App/Modules/http/UploadResponseParsedPlangDictTests.cs` | **green** |
| `RenderTests.cs:Render_AJsonFile_IsTextOfKindJson` | keep | A rendered `.json` file's `Type.Name` is `item` and `Type.kind.Name` is `json` (concept renamed `IsItemOfKindJson`); the inline template stays kindless. · `Modules/App/Modules/ui/RenderJsonFileIsItemOfKindJsonTests.cs` | **red** — Expected rendered text, Actual "{deep: m-1}" (producer narrows to a dict + Type.Name "dict", bypassing `{item, kind: json}`) |
| `TypeOwnedReadParityTests.cs:ObjectJsonRead_ProducesNavigableClrJson` | retire | — the clr(json) carrier is going; the dev-visible outcome (lazy navigation) is in `JsonLazyNavigationTests` | — |

Totals: 14 keep · 10 retire · 24 total.
Rewrite status: 14 kept tests, 12 new files (3 of the ActionNameWireRead rewrites share one). 7 green, 7 red (each red for the producer-not-yet-migrated reason named above).
