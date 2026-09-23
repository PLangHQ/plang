# coder → architect — type registry pass: inventory + proposals (no code yet)

Scope: `PLang/app/type/list/this.cs` (881 lines), the choice registry, `choice<T>`, `primitive.@this`, and the menu.
Caller counts exclude `obj/`. "prod" means PLang + PlangConsole + generator output.

## Member inventory — `type/list/this.cs`

| member | line | static | prod callers | test callers | fate (proposal) |
|---|---|---|---|---|---|
| `GetPrimitiveOrMime(name)` | 90 | yes | 1 — `type/this.cs:163` (the `ClrType` fallback when the entity has no Context) | 14 (NonNullInvariantTests) | **Q3** |
| `GetPrimitiveName(Type)` | 108 | yes | 0 | 0 | delete |
| `GetTypeNameStatic(Type)` | 121 | yes | 2 — `module/list/this.cs:248,336` (`App?.Type?.GetTypeName(..) ?? GetTypeNameStatic(..)`, for a module list with no App) | 5 | dies into the entity door; the no-App module list gets its App (fixtures build through TestApp) |
| `Get(name)` | 189 | — | 3 — `http/HttpBuildHelpers.cs:37`, `file/read.cs:134` (both ask "is this a known type": `Get(n) == null`), `variable/set.cs:217` (`type.ClrType ?? Type.Get(name)`) | 2 | **Q1** |
| `Clr(name)` alias | 192 | — | 1 — `type/this.cs:163` | — | dies (alias) |
| `Rank(entry)` | 238 | private | catalog tie-break | — | onto the entity (its own richness); **Q4** |
| `this[string]` | 258 | — | many | many | the name→entity door (**Q1**) |
| `this[System.Type]` | 279 | — | many | many | THE CLR→plang door; gains the choice arm (§1) |
| `ContainerFamily(Type)` | 316 | private | `this[Type]` | — | instance private (the registry's own selection knowledge) |
| `Get(name, depth)` | 357 | private | — | — | stays private |
| `ClrFromMime(mime)` | 402 | yes | 1 — `Get` (internal) | 0 | moves onto the primitive node (§2) with its MIME table |
| `GetTypeName(Type)` | 427 | — | 4 — `goal/step/action/this.Schema.cs:83`, `choice/list/this.cs:50`, `module/list/this.cs:248,336` — **plus the generated `Parse()` decline** (`Context.App.Type.Name(typeof(T))`, emitted into every handler) | 29 | body folds into the entity door; callers read `Type[clr]` (**Q2**) |
| `Name(Type)` alias | 498 | — | generated `Parse()` only | 5 | dies (alias) |
| `StripGenericArity` | 500 | private | naming | — | instance private, or the entity's naming; falls out of Q2 |
| `Register` / `RegisterDomainTypes` | 513 / 526 | — | 1 / 1 (`app/this.cs:294`, empty body) | — | `RegisterDomainTypes` is a no-op → delete |
| `GetValidValues(Type, ctx)` | 538 | — | 0 | 6 (via TypeMappingTestFacade) | dies (§2) |
| `ValidValues` alias | 558 | — | 0 | 0 | dies |
| `IsScalarPlangType` | 567 | yes | 0 | 1 | delete (test-only) |
| `IsPrimitive` | 599 | yes | 0 | 24 (TypeMappingTests) | delete (test-only); those tests go or become a test extension |
| `GetBuilderTypeNames` / `BuilderNames` | 622 / 625 | — | 1 — `type/list/view/this.cs:57` | 5 | both die (§2) |
| `BuildTypeEntries` | 635 | — `[Obsolete]` | 3 (view :58, :78; catalog cache :214) | 13 | out of scope (already marked for the list<type> render); a **verb+noun** logged |
| `ComplexSchemas()` | 804 | — | 1 — `type/this.cs:597` | 1 | a middleman over `CatalogByName`: **Q4** |
| `ReadStaticString` / `ReadStaticStringList` / `UnwrapType` | 813 / 833 / 854 | private | `BuildTypeEntries` + `IsScalarPlangType` | — | live and die with BuildTypeEntries; instance private meanwhile |

Out of this file but in the pass: `primitive.@this` is a **`static class`** (Aliases, Canonical, InlineFundamentals, ReferenceFundamentals, Fundamentals, BuilderNames). `choice<T>.ValidValues` (static, `choice/this.cs:62`). `choice.list.Get` (the `[Choices]` MethodInfo map, `choice/list/this.cs:73`).

## §1 choice — one face `{choice, kind}`

1. **The value names itself.** `choice<T>` overrides `Type` → `{choice, kind: <T's name>}`. Snag: `item.Type` takes no context, and T's plang name comes from the registry (`_typeToName` → `[PlangType]`, else the lowered CLR name). Either the choice derives it the way the registry's fallback does (lowercased `typeof(T).Name` — two derivations, the smell), or the value asks `Context.App.Type[typeof(T)]` (choice has no Context today). **Q5.**
2. **The naming door** answers `choice<operator>`; the static copy (:133) dies with GetTypeNameStatic; the instance arm (:440) changes.
3. **`choice/list/this.cs:53` dies.** Consequence: a closed `choice<Operator>` is no longer in `_typeToName`, so `this[Type]` falls to `ContainerFamily` → null → `clr`. It needs an explicit choice arm next to the list-node arm: `{choice, kind}`. The reader stays keyed `(choice, kind)`.
4. **Menu:** `propertiesUser.template:20` renders `p.Type.Name` alone. It becomes the whole face — name, kind when present, and **values** when the type has them (`Operator (choice<operator>: ==, !=, >, <, …, required)`). The same template change shows `list<path>`. The harness menu (`tools/decider/build_pr.py plang_type`) mirrors it.
5. Verify the `Output` round trip (`{choice, kind}` write → read) before and after; suspected broken today.

## §2 one door per question

- **CLR → plang:** `Type[clr]` (the entity). `GetTypeName`'s string answers for `int?` / `dict<k,v>` / nullable are what the 4 callers print. **Q2** covers whether the entity's face renders those.
- **plang → CLR:** **Q1.**
- **Valid values:** `GetValidValues` + its alias die. **Proposal:** the choice registry holds one element per closed set (kind name → its reader + its values), built at `Register`. The type entity's `Values` for `{choice, kind}` reads `Choice[kind].Values`. `choice<T>.ValidValues` (static) and `choice.list.Get` (the static `[Choices]` MethodInfo map) fold into that element: one owner of "what are this set's options".
- **Builder names:** `primitive.@this` stops being a static class. It becomes an instance node on the registry (`app.Type.Primitive`, born in the registry ctor) holding its tables, the MIME table (`ClrFromMime` moves there), and `BuilderNames`. The view reads `Type.Primitive.BuilderNames`. Both proxies die.

## §3 statics

After §2: `GetPrimitiveName`, `IsScalarPlangType`, `IsPrimitive` are deleted (no production caller); `GetTypeNameStatic` dies; `ClrFromMime` moves. That leaves `GetPrimitiveOrMime` (**Q3**). The private statics become instance members of whoever owns them: `ContainerFamily` stays on the registry, `Rank` goes to the entity (**Q4**), and the three reflection readers go with BuildTypeEntries.

## Questions

- **Q1 — plang→CLR door.** Recommend `Type[name]` (the entity) plus `.ClrType`. `Get`/`Clr` die, with `Get(name, depth)` staying private. But `this[string]` **throws** on a miss, while all 3 `Get` callers test for null ("is this a known type"). Either the entity door answers null on a miss (and its current throw-on-miss callers must be checked; I'll count them before touching anything), or there's a `Has(name)` beside it. Which?
- **Q2 — naming strings.** `GetTypeName` renders `int?`, `list<text>`, `dict<text,number>`. After it folds into the entity, the entity's face must render the same text (name + kind; nullable is the row's own `Nullable`, not a `?` suffix). OK to drop the `?` suffix? Callers: `module/list:248,336` (catalog rows, which already carry `Nullable`) and `action/this.Schema.cs:83` (return type).
- **Q3 — `GetPrimitiveOrMime`.** It exists only because a type entity can be born without Context (`type/this.cs:163`). Born-with-context says kill the fallback, but type entities are minted context-less widely (`new app.type.@this("bool")` in tests and production). Kill it and fix the births, or keep it until type entities are born with context (a separate pass)?
- **Q4 — `ComplexSchemas()` / `Rank`.** `ComplexSchemas()` just returns `CatalogByName`, and its one caller (`type/this.cs:597`, the entity promoting itself) could read `Type[name]` instead. Delete it? And `Rank` → the entity's own member (a noun: `Richness`)?
- **Q5 — how `choice<T>` knows T's name** (see §1.1).

## Also found

- `this.cs:295-299` says `_typeToName` "legitimately holds non-item hosts (goal …)" and that answering `Type[typeof(goal)]` as an entity would resurrect "goal is a plang type". But `goal` is now `item.@this, ICreate` (`goal/this.Item.cs:7`), as are action, step, actor, mock and snapshot. The comment is stale, and it is the same fact behind the graft #1 marker question.
