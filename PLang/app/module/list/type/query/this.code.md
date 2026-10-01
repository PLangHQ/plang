# list.query: the query type and its clauses

`list.query` (`PLang/app/module/list/query.cs`) takes a `query` (`type/query/this.cs`) and runs it on a list:
`query.Run(list, execution, context)`. A query is written as a dict of clauses — `where`, `group`, `distinct`,
`order` — and each clause is a query itself: the clauses are the query's kinds (`[Kinds]` on `query.@this`, so a where
is `{query, kind: where}`, as a file is `{path, kind: file}`), each applying itself to the rows it is handed. A query
made from a dict is the list of its clauses, `query.list` — always, one clause being a list of one.

The clauses are the query's own: written only inside the query's dict (the list writes itself), never navigated as
values (`%q.where%` answers nothing). They are no types of their own: the registry's `FamilyName` finds the nearest
base declaring `[Kinds]` (`type/list/Registry.cs`).

## Made from its dict

```
query.Create(raw, declared, data)                      type/query/this.cs
├─ a query → itself; a dict → query.list.Create(dict, data); else why on data, naming the parts
query.list.Create(dict, data)                          type/query/list/this.cs
├─ each key: query.@this.Known[name]  — no such clause: "'<key>' is no part of a query — its parts are …"
├─ make(entry, data, context)          — the clause's own static Create; null = why is on data, said as the
│                                        clause's ("where: …")
└─ no clause at all: "a query names at least one part: …"
```

`query.@this.Known`: the classes under the query with a static `Create(clause, data, context)`, found once (reflection,
cached in a `Lazy`) — `query.list` has none, so it is no clause. A clause's name is the last segment of its
namespace; `Names` lists them sorted, for refusals.

Each clause's `Create` takes what the query holds under its name and answers the clause or null with a plain why on
`data`; no clause throws, and none prefixes its own name:
- `where/this.cs`: one line over `condition.@this.Create(condition, …)` (`where/condition/this.cs`) — a list (and),
  `{and: [...]}` → `and.Create`, `{or: [...]}` → `or.Create` (each reads its list through `condition.Read`), else
  `compare.Create(comparison, …)`, which checks the operator against `Operator.Choices`. The compare's value is
  settled where the query runs (`compare.Keep`): a %variable% not set fails VariableNotFound.
- `group/this.cs`: the field to group by.
- `distinct/this.cs`: a bool, read by `bool.@this.Create` — `"true"` reads, `"yes"` is refused with bool's why.
- `order/this.cs`: a key (`order/key/this.cs`: a field — or a %variable% holding one, settled at the sort — or
  `{field, desc}`, desc read by bool), or a list of them; each key sorts the rows by itself (`key.Sort` → `list.Sort`,
  which refuses a field no element has: FieldNotFound) and writes itself.

## Run

`query.Run` is virtual: a clause's is `Apply(rows, [])`; `query.list.Run` orders its clauses by `Rank` (where 0, group
1, distinct 2, order 3) unless `%!list.query.setting.execution% = written`, and the first applies itself and hands what
it answers to the rest (`query.Next`). A clause's failure is named by it (`query.Named`). The input list is unchanged.

`Rank` is a clause's place in SQL's order; the list only orders by it (its own answers 0 and is never read).

## Tests

- C#: `PLang.Tests/Modules/App/Modules/list/QueryPartTests.cs` — each clause reads; each refusal's words; one clause is
  a query.list of one. `PLang.Tests/Types/App/Types/KindsTests.cs` — the families and no clause a type.
- plang: `test/plan/list-query/module/list/query/*.test.goal` (where and/or/variable/unset, order/unset/unknown field,
  distinct, group then order, SQL and written order, input unchanged, unknown field).
