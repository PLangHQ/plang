# list.query: the query type and its parts

`list.query` (`PLang/app/module/list/query.cs:17-23`) takes a `query` (`type/query/this.cs`) and runs it on a list:
`query.Run(list, execution, context)`. A query is a dict of parts — `where`, `group`, `distinct`, `order` — each part
a class under the query at its name (`type/query/where/this.cs`, …), each applying itself to the rows it is handed.

## Made from its dict

```
query.Create(raw, declared, data)                                   type/query/this.cs:33
├─ a dict, else why on data, naming the parts (part.@this.Names)
├─ :48  each key: part.@this.Known[name]  — no such part: "'<key>' is no part of a query — its parts are …"
├─ :54  make(entry, data, context)        — the part's own static Create; null = why is on data
└─ no part at all: "a query names at least one part: …"
```

`part.@this.Known` (`type/query/part/this.cs:26-34`): the part classes under the query, found once (reflection over the
assembly, cached in a `Lazy`) and held as name → that class's `internal static Create(written, data, context)`. A
part's name is the last segment of its namespace (`NameOf`, :18); `Names` (:36) lists them sorted, for refusals.

Each part's `Create` answers the part or null with why on `data`, said as the part's (`where: …`, `group: …`,
`order: …`); no part throws:
- `where/this.cs`: `{field, op, value}`, `{and: [...]}`, `{or: [...]}`, or a list (and); recursive `Condition`; an
  unknown operator is checked against `Operator.Choices`.
- `group/this.cs`: the field to group by.
- `distinct/this.cs`: `true`/`false`.
- `order/this.cs`: a field, `{field, desc}`, or a list of them.

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
