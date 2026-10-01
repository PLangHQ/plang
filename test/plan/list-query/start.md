# list-query

## Why

A learner describes what they want from a list in one sentence: "filter %users% where type is 'student' and age > 20, group by name, distinct, order by age". Today that takes four steps, one per action (`list.where`, `list.group`, `list.unique`, `list.sort`), each holding one field, and `and`/`or` can't be said at all. `list.query` takes the whole sentence as one value, a `query`, which the builder fills from the step's words. The query runs its parts in a fixed order, so the result doesn't depend on the order the words came in, and every part goes through the doors the list and its items already own. Four actions become one, so the model has one action to pick instead of four, and fewer ways to get the pick wrong.

## Decisions (Ingi, 2026-10-01)

- **`query` is its own plang type**, in the list module's folder: `app/module/list/type/query/this.cs`, as `hash` lives under crypto because crypto makes it.
- **`list.query` replaces `list.where`, `list.sort`, `list.group` and `list.unique`.** There's one door; a one-part sentence ("where %users% age > 20") is a small query.
- **The parts run in SQL's order:** where → group → distinct → order. That's the default of the action's setting `%!list.query.setting.execution%`, `sql | written`; `written` runs them in the order the step says them.
- **A query never changes its input.** It answers a new list. A step that names no destination ("sort %people% by age") is built with an explicit `write to %people%`, so the write-back shows in the `.pr`.
- **After a group, `order by` orders the items inside each group** ("group by name, order by age": each name's items by age).
- **The action takes the list itself**, `List` as `data<list>`, never a variable's name. Its name, when needed, is `list.variable.name`.
- **Names are dot paths** (the dot-case rule): `%!list.query.setting.execution%`, never a glued word.
- **Later, not in this plan:** the same query against the database (the db module); dot case for existing action properties.

## The shape (the coder owns the code; this is the intent)

- `query` holds its parts: `where` (a condition tree: `and`/`or` over `{field, op, value}` leaves, `op` one of condition's operators, `value` may be a `%variable%` read at run), `group` (fields), `distinct`, `order` (`{field, desc}` in order). A wrong field fails at build, naming the part.
- `list.query(List, Query)` → the query applies itself to the list. The query owns the sequence, the list and its items own each operation:
  - each where leaf → `item.Where(field, op, value, context)` (`type/item/this.cs:471`, list `:939`, dict `:426`);
  - group → `list.Group(key, context)` (`type/item/list/this.cs:913`), giving `{key, items}`;
  - distinct → `list.Unique(context)` (`:888`);
  - order → `list.Sort(by, descending, context)` (`:875`). After a group, it orders each group's items.
- `execution` is the action's option, so it reads as `%!list.query.setting.execution%` (389).

## Stages

1. **The type and the action:** where with and/or, group, distinct, order, in SQL order; the setting with `written`. Plang tests below. No builder change yet: the tests are written in formal where the builder can't pick it.
2. **The builder picks it:** `query.description.md`, `query.notes.md` (tagged lines, decision 411), `query.examples.md`; golden cases for one-part and many-part sentences; a step with no destination gets its explicit `write to`. Eval round by name; re-pick the golden.
3. **The four go:** `where.cs`, `sort.cs`, `group.cs`, `unique.cs` and their `.md` files deleted; every caller moved (below); the catalog, `types.json` and goldens follow.

## Demolition

- `PLang/app/module/list/where.cs`, `sort.cs`, `group.cs`, `unique.cs`.
- `os/system/modules/list/where.description.md`, `where.examples.md`, `sort.description.md`, `group.description.md`, `unique.description.md`.
- The two `list.Sort` overloads (`type/item/list/this.cs:458`, `string? by` and `:875`, `text? by`): one stays, the one the query calls. Check `:458`'s callers first.
- Callers to move: `test/plan/app-systems/module/list/where/PartialFieldFilters.test.goal`, `MisspelledFieldIsAnError.test.goal`; C# tests naming the four (ListTests, ListAddIdentityTests, Stage6_ConsumersTests, KeepsItsKeyTests, HostRenderSpikeTests, MissingVariableNameTests: check each); `Documentation/v0.2/type-system.md`; the Fluid comments naming `list.where` (`ui/code/Fluid.cs:72`, `:192`).

## Stays

- The doors the query calls: `item.Where`, `list.Group`, `list.Unique`, one `list.Sort`.
- Every other list action (`add`, `remove`, `count`, `first`, `last`, `get`, `set`, `contains`, `any`, `indexof`, `join`, `split`, `flatten`, `range`, `reverse`).
- condition's operators, which a where leaf uses as they are.

## How it's proven

- [start.goal](start.goal) runs the plan's tests; each lives under this folder at the path of what it tests (`module/list/query/…`).
- At each stage's end its tests are built and run. A stage is accepted when its tests are green, the checks hold, and the architect's OBP review is clean.
- **Every test must fail without its change.**
- The plan's plang says what is wanted; the coder rewrites each test in real plang that compiles, keeping what it proves.

## What tests can't show (checks the coder runs and reports)

- **Stage 2:** the eval round by name. Each golden sentence picks `list.query` with the right parts, and no other case regresses. A one-part sentence ("where %users% age > 20") still builds.
- **Stage 3:** no `.goal`, template, `.llm`, doc or C# reference to the four is left (grep), and the C# suites are at the baseline.
- **Every stage:** the gate by name; PLNG001/002 at 0.

## OBP validation

| new surface | plang path | C# | file | the three agree |
|---|---|---|---|---|
| the type | a value of type `query` | `app.module.list.type.query.@this` | `app/module/list/type/query/this.cs` | yes: crypto's `hash` precedent |
| the action | `list.query` | `app.module.list.query` | `app/module/list/query.cs` | yes |
| its setting | `%!list.query.setting.execution%` | `query`'s `Execution` option | `app/module/list/query.cs` | yes: action → its setting → option (389) |
| the sequence | — | `query.Apply(list, context)` (the coder names it: one verb) | the type | the query owns the order; the list owns each operation |

Smells to check in review: no `is`/type-switch over the parts (each part applies itself); no copied `ListName`; no glued names; nothing reaches into a list's elements except through `item.Where`.
