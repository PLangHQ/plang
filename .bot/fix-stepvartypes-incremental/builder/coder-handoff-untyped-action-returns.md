# Actions producing `(object)` in compile snapshots

Survey across all `Tests/.build/traces/` — captures the producer action whose return value flowed into a `variable.set Value=%!data%` whose resulting variable then appeared as `%var%(object)` in a later step's `Variables in scope` snapshot.

**Counts** are occurrences of an `(object)`-typed variable in some step's snapshot whose producer is the listed action. A single rebuild of a hot test goal can repeat the producer many times — read counts as relative weight, not absolute frequency.

Three categories below. **A** needs C# typing work, **B** is correctly-typed-as-object (mathematical or genuinely polymorphic), **C** is the LLM not honoring `→ returns T` from the catalog.

## A. Bare `Data` — C# return type lacks a generic (16 actions)

These handlers return `Task<data.@this>` with no `<T>` parameter. There's no static return type for the catalog to advertise, so the trailing `variable.set` lands as `type:"object"` regardless of what the action actually produces. Suggested fix: change each `Run()` signature to `Task<data.@this<T>>` for the appropriate `T`, and update `SetProp` / value construction to match.

| Count | Action | Handler | Suggested return type |
|---:|---|---|---|
| 262 | `test.discover` | `PLang/app/modules/test/discover.cs` | List<File> (list<file>) — see Tester.File |
| 145 | `test.run` | `PLang/app/modules/test/run.cs` | Results (results) |
| 103 | `file.read` | `PLang/app/modules/file/read.cs` | string (text) or bytes — needs a polymorphic carrier; consider `Data<object>` as honest, or a `FileContent` record carrying both shapes |
| 76 | `test.report` | `PLang/app/modules/test/report.cs` | string (the JSON/junit content path) or new ReportResult record |
| 56 | `test.tag` | `PLang/app/modules/test/tag.cs` | bool (whether tag was added) or void-like Ok |
| 40 | `llm.query` | `PLang/app/modules/llm/query.cs` | object (genuinely polymorphic — depends on Schema) |
| 18 | `http.request` | `PLang/app/modules/http/request.cs` | HttpResponse record (status, headers, body) |
| 10 | `settings.get` | `PLang/app/modules/settings/get.cs` | _(needs domain decision)_ |
| 5 | `http.upload` | `PLang/app/modules/http/upload.cs` | HttpResponse or bool |
| 4 | `goal.call` | `PLang/app/modules/goal/call.cs` | _(needs domain decision)_ |
| 3 | `channel.set` | `PLang/app/modules/channel/set.cs` | _(needs domain decision)_ |
| 2 | `goal.getTypes` | `PLang/app/modules/goal/getTypes.cs` | _(needs domain decision)_ |
| 2 | `output.ask` | `PLang/app/modules/output/ask.cs` | _(needs domain decision)_ |
| 1 | `builder.types` | `PLang/app/modules/builder/types.cs` | _(needs domain decision)_ |
| 1 | `builder.actions` | `PLang/app/modules/builder/actions.cs` | _(needs domain decision)_ |
| 1 | `builder.goals` | `PLang/app/modules/builder/goals.cs` | _(needs domain decision)_ |

## B. Already `Data<object>` — genuine polymorphic return (17 actions)

These are typed as `object` deliberately — the action's return type depends on input (math operators yield int or double; list accessors yield whatever the list element is). Snapshot showing `(object)` here is **correct**. No fix needed.

| Count | Action | Handler |
|---:|---|---|
| 126 | `list.first` | `PLang/app/modules/list/first.cs` |
| 81 | `list.last` | `PLang/app/modules/list/last.cs` |
| 55 | `list.get` | `PLang/app/modules/list/get.cs` |
| 28 | `math.add` | `PLang/app/modules/math/add.cs` |
| 23 | `math.subtract` | `PLang/app/modules/math/subtract.cs` |
| 21 | `math.multiply` | `PLang/app/modules/math/multiply.cs` |
| 19 | `math.divide` | `PLang/app/modules/math/divide.cs` |
| 17 | `math.abs` | `PLang/app/modules/math/abs.cs` |
| 15 | `math.round` | `PLang/app/modules/math/round.cs` |
| 13 | `math.min` | `PLang/app/modules/math/min.cs` |
| 11 | `math.max` | `PLang/app/modules/math/max.cs` |
| 9 | `math.power` | `PLang/app/modules/math/power.cs` |
| 7 | `math.sqrt` | `PLang/app/modules/math/sqrt.cs` |
| 5 | `math.floor` | `PLang/app/modules/math/floor.cs` |
| 3 | `math.random` | `PLang/app/modules/math/random.cs` |
| 3 | `math.ceiling` | `PLang/app/modules/math/ceiling.cs` |
| 1 | `math.modulo` | `PLang/app/modules/math/modulo.cs` |

## C. Already `Data<T>` with specific T — LLM not honoring it (15 actions)

These are already strongly typed in C#. Snapshot showing `(object)` means the LLM emitted `type:"object"` on the trailing `variable.set` instead of using the catalog's `→ returns T` annotation. The fix is on the prompt/discipline side, not C#.

| Count | Action | C# return | Should be |
|---:|---|---|---|
| 74 | `list.contains` | `data.@this<bool>` | `(bool)` instead of `(object)` |
| 69 | `list.count` | `data.@this<int>` | `(int)` instead of `(object)` |
| 40 | `list.split` | `data.@this<list>` | `(list)` instead of `(object)` |
| 25 | `list.any` | `data.@this<bool>` | `(bool)` instead of `(object)` |
| 23 | `list.join` | `data.@this<string>` | `(string)` instead of `(object)` |
| 16 | `variable.exists` | `data.@this<bool>` | `(bool)` instead of `(object)` |
| 15 | `list.remove` | `data.@this<list>` | `(list)` instead of `(object)` |
| 14 | `file.exists` | `data.@this<path>` | `(path)` instead of `(object)` |
| 8 | `list.range` | `data.@this<list>` | `(list)` instead of `(object)` |
| 4 | `list.unique` | `data.@this<list>` | `(list)` instead of `(object)` |
| 4 | `crypto.hash` | `data.@this<byte[]>` | `(byte[])` instead of `(object)` |
| 3 | `file.list` | `data.@this<List<path>>` | `(List<path>)` instead of `(object)` |
| 2 | `cache.wrap` | `data.@this` *(bare — should be in Category A)* | `(should be re-categorized)` instead of `(object)` |
| 2 | `identity.export` | `data.@this<Identity>` | `(Identity)` instead of `(object)` |
| 1 | `ui.render` | `data.@this<string>` | `(string)` instead of `(object)` |

## Unknown — handler not located (1 actions)

- 30  `mock.intercept`

## Summary counts

- Category A (C# typing gap): **16 actions**, 729 occurrences
- Category B (correctly polymorphic): **17 actions**, 437 occurrences
- Category C (LLM discipline gap): **15 actions**, 300 occurrences
