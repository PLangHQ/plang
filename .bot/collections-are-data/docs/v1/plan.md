# docs v1 — plan for collections-are-data

## Context

47-commit branch making `dict` and `list` the native PLang value types:
collections hold Data end-to-end, a single `Compare` mediator owns all
equality and ordering, and the chunk/row list model drives `IListLeaf`.
All upstream bots PASS (codeanalyzer v4, coder v7, tester v7, security v1,
auditor v1). The `@schema:"data"` wire marker replaces the old
name+value+type shape-sniff.

## Inputs read

- `.bot/collections-are-data/auditor/v1/report.md` — architecture summary,
  O1/O2 observations, `@schema:data` marker, IListLeaf, Compare mediator.
- `PLang/app/type/dict/this.cs`, `PLang/app/type/list/this.cs` — type shape.
- `PLang/app/data/Compare.cs`, `ScalarComparer.cs` — mediator + scalar leaf.
- `PLang/app/data/IListLeaf.cs`, `IEquatableValue.cs`, `IOrderableValue.cs`.
- `PLang/app/data/Wire.cs` — `@schema:data` writer + reader.
- Existing docs: `type-system.md`, `wire-serialization.md`, `good_to_know.md`.

## Work items

1. **Add four sections to `type-system.md`** (append after `type.@this.Null`):
   - `dict.@this` and `list.@this` — native collection types, symmetric peers,
     end-to-end Data, `[JsonConverter]` governs raw-STJ only, `ToRaw()` is
     read-out not mutation.
   - `Compare` — single typed-compare mediator: null policy, coercion,
     dispatch; `IEquatableValue`/`IOrderableValue`; `ScalarComparer`; footgun
     (don't add arms to `Compare`).
   - List chunk/row model and `IListLeaf` — rows under the hood, flat on read,
     O(1) `Add`, `IListLeaf` = dissolve-into-list, `CopyStructure` aliasing.

2. **Add one section to `wire-serialization.md`** (before "Wire passthrough"):
   - `@schema:"data"` marker — written first by `Wire.Write`, recognized by
     three readers, depth-capped, name excluded from signing.

3. **Update `good_to_know.md` index** — four new pointer entries.

4. **File `claude-md-proposals.md`** — three new CLAUDE.md bullets for:
   `IEquatableValue`/`IOrderableValue`/`Compare`, native collection types,
   `IListLeaf`/`CopyStructure`. (Truthiness bullet unchanged.)

5. No C# docstring drift found on the changed files — all comments were
   added fresh on this branch and are already current.
