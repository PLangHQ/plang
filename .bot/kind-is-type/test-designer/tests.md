# kind-is-type — tests

| test | stage | why it's red today (quoted) | plang/C# |
|---|---|---|---|
| [ShortNameFindsTheType](../../../test/plan/kind-is-type/app/type/ShortNameFindsTheType.test.goal) | 1 | guard — green before and after; `as text` yields `text`, `as list` yields `list` | plang |
| [SettingClassIsAType](../../../test/plan/kind-is-type/app/type/SettingClassIsAType.test.goal) | 1 | `no type 'llm.setting'` — today the LLM settings class is a kind under `[PlangType("setting")]` (decision 48), not a type named by its namespace | plang |
| [ModuleTypeIsNamedByItsModule](../../../test/plan/kind-is-type/app/type/ModuleTypeIsNamedByItsModule.test.goal) | 1 | `no type 'window.window'` — today `window.type.window` is named `window` via `[PlangType("window")]`; `screen.type.screen.window` carries no PlangType and isn't registered as a plang type at all | plang |
| [DecimalIsANumber](../../../test/plan/kind-is-type/app/type/number/DecimalIsANumber.test.goal) | 2 | `Expected "decimal", Actual (null)` for `%n!type.kind.name%` — today the kind of a number is lost at run (even with formal `Type={name:"number", kind:"decimal"}`); `kind.name` reads null because kind isn't a navigable type | plang |
| [JsonIsAText](../../../test/plan/kind-is-type/app/type/text/JsonIsAText.test.goal) | 2 | `Expected "json", Actual (null)` for `%x!type.kind.name%` — same shape; the text's kind doesn't carry json as a navigable type | plang |
| [ListKnowsItsElement](../../../test/plan/kind-is-type/app/type/list/ListKnowsItsElement.test.goal) | 2 | `Expected "path", Actual (null)` for `%files!type.kind.name%` — the typed list's element kind isn't a navigable type at run | plang |
| [KindNestsTests](../../../PLang.Tests/Types/App/Types/KindNestsTests.cs) | 2 | `AListOfListsOfPath_HasAKindWhoseInnerKindIsPath`: `Expected "list", found "list<path>"` (kind.Name is the glued whole string). `TheWrittenForm_RoundTrips_ListOfListOfPath`: `Expected "path", found ""` (looking the inner face up as a type yields a kindless type — the nested kind doesn't round-trip). Both fail because today a kind is a flat string on `type.@this`, not itself a type. | C# |
| [NamedValuesBelongToTheType](../../../test/plan/kind-is-type/app/type/separator/NamedValuesBelongToTheType.test.goal) | 2 | `Expected true, Actual (null)` for `%isText%` — the `if %line% is text` branch never fires because today separator is a top-level type (deriving from `item`, not `text`) | plang |
| [SetAsImageGifStrictMismatch](../../../test/app/type/kind/strict/SetAsImageGifStrictMismatch.test.goal) | 2 | stage 2 adopts this and the three siblings in `test/app/type/kind/strict/` as its proof per architect; red today per the kind/strict baseline | plang |

Baseline before stage 1 tests ran: 245 pass / 16 fail / 12 stale / 3 skipped (from start.md).
After stage 1 tests landed: 246 pass / 18 fail / 12 stale / 3 skipped. Delta +1 pass (guard) +2 fail.
After stage 2 tests landed: 246 pass / 22 fail / 12 stale / 3 skipped. Delta +0 pass +4 fail (the four plang reds).
C# suite — PLang.Tests.Types: +2 failures (both KindNestsTests cases).
