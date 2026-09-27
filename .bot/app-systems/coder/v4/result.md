# coder v4 — stage 4 result: the collected type

Branch `app-systems`. The trace and proposals are in `v4/plan.md`. The rulings came from plang-40
(decisions logged in `.bot/app-systems/architect/summary.md`).

## Commits
| commit | what |
|---|---|
| 010bfa885 | `item.list` → `item.history` (`type/item/history/`) |
| b67ac285d | strict `list<T>` (`T : item, ICreate<T>`); test, test timing, LlmMessage and type gain ICreate; type unsealed |
| 94ea6df1a | `type<T>` + `ICurrent`/`IList` beside `IMatch`; a `type<T>`'s class names "type" |
| 0339f2bcf | the types are a `list<type>` behind one `Admit` guard; no stored context (Mime/Extension/identity door take the caller's); the global kind store is gone: kinds live on their types |
| 5e9b5ad0f | `app.type` is `type<type, type.list>` (the list class as the second parameter); every `App.Type` site → `app.type.list` |
| a6d4e740b | names a programmer or `.pr` wrote ask `await app.type.Get(name)`; `data.Is(string)` → test extension; navigation takes a member only when it holds one |
| 0af97062c | `%!app.type%` is the list's entry named `type` |

## Decisions along the way (plang-40)
- **The guard.** One `protected virtual Admit` guard, not a virtual `Add` with every adder routed through it. Routing the O(1) chunk adders through a per-item `Add` would make every list extend O(n).
- **The list class as a parameter.** `type<T, L>`: the element names its list class through `IList<T, L>`, so `app.type.list` is the types' list with no casts.
- **Kinds.** There is no flat kind store. A type's empty kind holds its kinds; the type list answers `Kind(name)` and `Kind(clr)` by walking them, and mints a classless kind by name. Kinds hold no context; `kind.type(context)`.
- **`Get` without `all()`.** `Get(key)` walks the list until stage 7 brings `all()`.
- **Moved to stage 7.** The prompt line teaching `%!app.X["key"]%`, and the rule that a concept type reads its facts itself. Stage 4 stays builder-neutral.

## Verification
- Six C# suites after every commit: no new failures. Final: Modules 38, Types 17, Wire 18, Data 44, Generator 18, Runtime 24 (baseline 38/23/18/45/18/24).
- Prompt twins (`PickListTests`) byte-equal after every commit.
- `plang --test` after a clean rebuild: 7 / 0 / 317, as baseline.
- Timing before/after the list step, in seconds:

  | run | before | after |
  |---|---|---|
  | Types | 10.7 | 10.4 |
  | Data | 24.8 | 23.1 |
  | Modules | 40.5 | 37.0 |
  | `plang --test` | 1.74 | 1.80 |

  No index was added.
- New tests: `CollectedTypeTests` (Get answering a match or a 404, the class naming "type", current, `app.type`'s Get and navigation, `%!app.type%` as the list's entry). About 150 test files moved to the new API through the helper agent (rounds 6 and 7), reviewed against the suites.

## Notes for later stages
- `SetTests.Validate_TypeMismatch_ReturnsError` pins a non-strict type check production no longer makes. It was red in the baseline and needs Ingi's call (rewrite or delete).
- Source-reading tests that point at moved files (`Stage7_PathGrowthTests`, `Stage6_ConsumersTests`) were red in the baseline.
