# kind-is-type — tests

| test | stage | why it's red today (quoted) | plang/C# |
|---|---|---|---|
| [ShortNameFindsTheType](../../../test/plan/kind-is-type/app/type/ShortNameFindsTheType.test.goal) | 1 | guard — green before and after; `as text` yields `text`, `as list` yields `list` | plang |
| [SettingClassIsAType](../../../test/plan/kind-is-type/app/type/SettingClassIsAType.test.goal) | 1 | `no type 'llm.setting'` — today the LLM settings class is a kind under `[PlangType("setting")]` (decision 48), not a type named by its namespace | plang |
| [ModuleTypeIsNamedByItsModule](../../../test/plan/kind-is-type/app/type/ModuleTypeIsNamedByItsModule.test.goal) | 1 | `no type 'screen.window'` — the screen module's window kind isn't a plang-named type today; also `page` must be nobody's name (post-761 the window module's page is `window.page`) | plang |
| [DecimalIsANumber](../../../test/plan/kind-is-type/app/type/number/DecimalIsANumber.test.goal) | 2 | `Expected "decimal", Actual (null)` for `%n!type.kind.name%` — today the kind of a number is lost at run; `kind.name` reads null because kind isn't a navigable type (asserts held — Ingi's answer still pending) | plang |
| [JsonIsAText](../../../test/plan/kind-is-type/app/type/text/JsonIsAText.test.goal) | 2 | `Expected "json", Actual (null)` for `%x!type.kind.name%` — same shape (held) | plang |
| [ListKnowsItsElement](../../../test/plan/kind-is-type/app/type/list/ListKnowsItsElement.test.goal) | 2 | `Expected "path", Actual (null)` for `%files!type.kind.name%` — the typed list's element kind isn't a navigable type at run | plang |
| [KindNestsTests](../../../PLang.Tests/Types/App/Types/KindNestsTests.cs) | 2 | `TheNestedWrittenForm_RoundTripsThroughTheTypeSerializer`: `InvalidOperationException: Cannot get the value of a token type 'StartObject' as a string` at `type/serializer/Reader.cs:33` — reads `kind` as a bare string, so a nested-kind JSON `{"name":"list","kind":{"name":"list","kind":"path"}}` trips at the inner `{`. Round-trip proof, no `kind.Name` compare. | C# |
| [NamedValuesBelongToTheType](../../../test/plan/kind-is-type/app/type/separator/NamedValuesBelongToTheType.test.goal) | 2 | `Expected true, Actual (null)` for `%isText%` — `if %line% is text` never fires because today separator is a top-level type (deriving from `item`, not `text`) | plang |
| [SetAsImageGifStrictMismatch](../../../test/app/type/kind/strict/SetAsImageGifStrictMismatch.test.goal) | 2 | stage 2 adopts this and the three siblings in `test/app/type/kind/strict/` as its proof per architect; red today per the kind/strict baseline | plang |
| [PassedOnUnread](../../../test/plan/kind-is-type/app/type/json/PassedOnUnread.test.goal) | 3 | `Expected "sQxdP9oGInw4lELvwytAm4IhJxqrY8OHtPs0XxWmtwQ=", Actual "DRAkfY2nEmb9WeRqzeyroU0c4xU3TXPmxq13XMSOyHs="` — read-then-write reformats the file (whitespace lost, order collapsed), so sha256 hashes diverge. config.json is spelled with unusual whitespace and keys out of order to catch any reformat. Copy deleted at end. | plang |
| [ReadIntoIsADict](../../../test/plan/kind-is-type/app/type/json/ReadIntoIsADict.test.goal) | 3 | `Expected true, Actual (null)` for `%isDict%` — `%cfg% is dict` is false today because parsed json is a C# JsonElement, not a plang dict (Cut2 / Narrow baseline) | plang |
| [ArrayIsAList](../../../test/plan/kind-is-type/app/type/json/ArrayIsAList.test.goal) | 3 | `Expected true, Actual (null)` for `%isList%` — same shape as above; parsed json array isn't a plang list | plang |
| [ReadIntoIsATable](../../../test/plan/kind-is-type/app/type/csv/ReadIntoIsATable.test.goal) | 3 | **guard** — green today (csv already parses to a plang table); stage 3 must keep it green | plang |
| [TypedSlotJsonReadTests](../../../PLang.Tests/Runtime/App/Goals/TypedSlotJsonReadTests.cs) | 3 | `ATypedDictSlot_FirstReadOfTheDict_GoesThroughJsonAndAnswers` — the first read of the settled dict goes through the json type and answers one entry; today the made value loses json and the first read answers nothing (decision 758 — companion to the coder's lazy-pin `ATypedSlot_SettlingAReferenceToUnreadContent_DoesNotReadIt`) | C# |

Suite deltas:
- Baseline (start.md): 245 pass / 16 fail / 12 stale / 3 skipped
- With stage 1 tests: 246 pass / 18 fail / 12 stale / 3 skipped
- With stage 2 tests: 246 pass / 22 fail / 12 stale / 3 skipped
- With stage 3 tests: 247 pass / 25 fail / 12 stale / 3 skipped (+1 pass guard, +3 fail)
- C# PLang.Tests.Types: +1 failure (KindNestsTests, replaced single-case round-trip)
- C# PLang.Tests.Runtime: +1 failure (TypedSlotJsonReadTests)
