# list.query: the query type and its parts

`list.query` (`PLang/app/module/list/query.cs:17-23`) takes a `query` (`type/query/this.cs`) and runs it on a list:
`query.Run(list, execution, context)`. A query is a dict of parts — `where`, `group`, `distinct`, `order` — each part
a class under the query at its name (`type/query/where/this.cs`, …), each applying itself to the rows it is handed.

## Made from its dict

```
query.Create(raw, declared, data)                                   type/query/this.cs:33
├─ a dict, else why on data, naming the parts (part.@this.Names)
├─ :48  each key: part.@this.Known[name]  — no such part: "'<key>' is no part of a query — its parts are …"
├─ :54  make(entry, data, context)        — the part's own static Create; null = why is on data,
│                                            which query.Create says as the part's ("where: …")
└─ no part at all: "a query names at least one part: …"
```

`part.@this.Known` (`type/query/part/this.cs`): the part classes under the query, found once (reflection over the
assembly, cached in a `Lazy`) and held as name → that class's `internal static Create(part, data, context)`. A
part's name is the last segment of its namespace (`NameOf`); `Names` lists them sorted, for refusals.

Each part's `Create` takes what the query holds under its name (`where`, `group`, `distinct`, `order`) and answers
the part or null with a plain why on `data`; no part throws, and none prefixes its own name:
- `where/this.cs`: one line over `condition.@this.Create(condition, …)` (`where/condition/this.cs`) — a list (and),
  `{and: [...]}` → `and.Create`, `{or: [...]}` → `or.Create` (each reads its list through `condition.Read`), else
  `compare.Create(comparison, …)`, which checks the operator against `Operator.Choices`.
- `group/this.cs`: the field to group by.
- `distinct/this.cs`: a bool, read by `bool.@this.Create` — `"true"` reads, `"yes"` is refused with bool's why.
- `order/this.cs`: a key (`order/key/this.cs`: a field or `{field, desc}`, desc read by bool), or a list of them;
  each key sorts the rows by itself (`key.Sort` → `list.Sort`) and writes itself (`key.Output`).

## Run

`query.Run` (:68): the parts in SQL order (`Rank`: where 0, group 1, distinct 2, order 3) unless
`%!list.query.setting.execution% = written`; the first part applies itself and hands what it answers to the rest
(`part.Next`, part/this.cs:52). A part's failure is named by it (`part.Named`, :61). The input list is unchanged.

## Tests

- C#: `PLang.Tests/Modules/App/Modules/list/QueryPartTests.cs` — each part reads; each refusal's words.
- plang: `test/plan/list-query/module/list/query/*.test.goal` (stage 1: where and/or/variable, order, distinct,
  group then order, SQL and written order, input unchanged, unknown field).

## Known faults

- Stage 2 (the builder picks list.query from a step's words) is not taught yet: who teaches the builder a new action
  is with Ingi.
