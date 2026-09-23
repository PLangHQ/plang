# architect → coder — the type registry: one name per type, one door per question, no statics

Decided by Ingi 2026-09-23: "anything named in two ways is bad" → option A for choice; "those others is also something to fix, we dont want static in obp". Queue position: after `%!event%` and graft typing (b). Its own pass.

> **You own this.** Decisions settled; mechanics, names and commit split are yours. Inventory first, then code.

## Why

`choice<Operator>` answers four ways today, and three of them disagree with the reader:

| who answers | answer | where |
|---|---|---|
| the value itself | `choice`, no kind | `type/item/this.cs:279` (namespace tail; choice has no `Type` override) |
| the naming door (menu, catalog text) | `operator` | `type/list/this.cs:133-135` (static copy) and `:440-443` (instance copy) |
| the CLR-type door (the catalog row's `Type`) | entity `operator` | `type/list/this.cs:300-301`, because `choice/list/this.cs:53` registers `operator` as a type name; the family arm at `:302-307` whose comment says `{choice, kind:operator}` is unreachable for choice |
| the reader | `(choice, operator)` | `choice/list/this.cs:55-57` |

Under Ingi's graft rule the LLM copies the menu's type onto the row, so a name the reader cannot read is a broken slot, not a cosmetic difference.

## 1. choice — one face: `{choice, kind: <set>}` (option A)

The `list<path>` = `{list, kind: path}` precedent and the settled kind model.
- `choice<T>` names itself with its kind (a `Type` override on choice; the kind is T's name, derived, never stored).
- The naming door answers `choice<operator>` for `choice<Operator>`, as it answers `list<path>`.
- The choice registry stops registering the set's name as a type (`choice/list/this.cs:53` dies); the family `choice` and the per-(choice, kind) reader stay.
- The CLR-type door answers `{choice, kind}` for a closed choice.
- The menu renders the type's whole face (name AND kind) so the LLM copies `{"name":"choice","kind":"operator"}`; check `propertiesUser.template:20` (`p.Type.Name` alone would drop the kind — and the same for `list<path>` slots).
- Option B (each set its own type `operator`) rejected: every enum name becomes a global type name (collisions with real types), the shared choice behavior (rank, comparison, truthiness, parse) loses its family, and it breaks the list precedent.
- Verify: a choice value written by `Output` reads back through its reader (`{choice, kind}` round trip) — suspected broken today, not verified.

## 2. One door per question on the type registry

Each question gets ONE door; everything else answering it dies. The entity door comes first — a type's name is the entity naming itself.

| question | doors today | direction |
|---|---|---|
| what plang type is this CLR type | `this[System.Type]` (`:279`), `GetTypeName` (`:427-497`), `GetTypeNameStatic` (`:121-188`), `Name(clrType)` (`:498`, alias; 5 callers, all tests) | `Type[clrType]` returns the entity; its face is the name. The naming bodies and the alias die into it. |
| which CLR type is this plang name | `Get(name)` (`:189`), `Clr(name)` (`:192`, alias), `this[string]` (`:258`, the entity, carries `ClrType`) | one door; you choose which after the inventory |
| the valid values of a closed set | `GetValidValues` (`:538-555`), `ValidValues` (`:558`, alias), `Choice.Get(type)` (`choice/list/this.cs:73`), `choice<T>.ValidValues` (`choice/this.cs:62`, static) | **wrong location (Ingi).** `GetValidValues` unwraps Nullable/Data/choice and switches enum-vs-`[Choices]` — the set's own knowledge re-derived in the registry. No production callers (its last was `BuildResponse.Validate`, deleted); only tests via `TypeMappingTestFacade`. It and its alias die. The options are the closed set's: they surface on the type entity's face (its `Values`), filled by the choice, where the menu/catalog reads them; `choice<T>.ValidValues` stops being static. |
| the builder's type names | `GetBuilderTypeNames` (`:622`), `BuilderNames` (`:625`, alias) → `primitive.@this.BuilderNames` (`primitive/this.cs:160`, static) | **wrong location (Ingi).** Both are a middleman: the registry proxies the primitive table's list. One production caller, the catalog view (`type/list/view/this.cs:57`), which reads the primitive table directly. The table becomes an instance reached through the registry as a node, not a static; both proxies die. |

No aliases: a second name for a method is the same smell as a second name for a type.

## 3. No statics (Ingi: "we dont want static in obp")

Public statics in `type/list/this.cs` — each moves onto its owner as an instance member, or dies:
- `GetPrimitiveOrMime` (`:90`), `GetPrimitiveName` (`:108`, no callers → delete), `GetTypeNameStatic` (`:121`, dies per §2), `ClrFromMime` (`:402`), `IsScalarPlangType` (`:567`), `IsPrimitive` (`:599`, ~25 callers).
Private statics — each gets the ownership question ("whose knowledge is this?", answered by reading callers): `Rank` (`:238`), `ContainerFamily` (`:316`), `StripGenericArity` (`:500`), `ReadStaticString` (`:813`), `ReadStaticStringList` (`:833`), `UnwrapType` (`:854`).

Out of scope unless it falls out naturally: statics elsewhere in the codebase. This pass is the type registry and the choice type.

## Inventory answers (coder `ed5d15fb6`, ruled 2026-09-23)

- **Q1 plang→CLR:** `Contains(name)` beside the throwing indexer — the collection convention (`goal/list:231`, `module/list:108`, `channel/list:152`). `Get`/`Clr` die; `Get(name, depth)` stays private.
- **Q2:** the face drops the `?` suffix — nullability is the slot's fact (`Nullable` on the row).
- **Q3:** `GetPrimitiveOrMime` dies in this pass by fixing the births: a type entity is born knowing its context (21 production sites; own commit, last). Context-less test births build through the test app. A site with no context at hand is reported, not excused.
- **Q4:** `ComplexSchemas()` dies; `Rank` → the entity's `Richness`. Logged: `Rank` hides a name collision (`app.goal.@this` and `app.channel.type.goal.@this` both "goal") — the inverse of named-twice; todos, not this pass.
- **Q5:** every closed set declares its name with `[PlangType("…")]`; `choice<T>` reads T's attribute; the registry indexes the same attribute; a closed set without one fails loud at registration.

- **Q5 follow-up:** none of the 12 sets carries `[PlangType]` today (names come from the lowered-CLR fallback); each gains it with today's name, and `PlangTypeAttribute`'s doc changes to say a closed set declares its name.
- **Stale `.pr` rows (16 tracked `Tests/**/.build/*.pr` carry `"type":{"name":"operator"|"trigger"|"errororder"}`):** delete `choice/list:53` in the same commit anyway. The stale corpus is not a constraint (plural-gate Q2 reasoning): 467 of 468 test `.pr` files already load as empty goals. A C# test that goes red on one is listed as a stale fixture that regenerates with the builder; no temporary second name, no translate-on-read door.

## Commit 3 scope (ruled 2026-09-23)

- `module.list.Describe()` is deleted (obsolete, no production caller in C# or `os/`, a stored-twice of the property rows); its tests go or repoint to the property rows; `GetTypeNameStatic` dies with it.
- Accepted shifts of the fold: the `?` suffix drops; `dict<k,v>` → `dict<v>`; `Data<T>` is unwrapped by the slot holder; a raw CLR type answers `clr`.
- **`object` dies as a plang type name.** The open slot had two names: the catalog row for a plain `Data` slot said `object` (`property/this.cs:36`, `primitive/this.cs:65,109`, `type/list/this.cs:112,140,418,458`) while the prompt teaches `item`. One name: `item`.
- Coder reports any live handler parameter or `Run()` return typed as a raw CLR type (now naming `clr`) — leaves that should be plang types; not converted in this pass.

## Verify

1. `condition.if` Operator: the menu shows `choice<operator>`; an LLM row `{"name":"Operator","type":{"name":"choice","kind":"operator"},"value":"=="}` grafts, builds, runs; `"=+"` fails at the build with choice's own message.
2. A choice value round-trips through `Output` and the reader.
3. Grep gates: `GetTypeNameStatic`, `GetTypeName(`, `GetValidValues`, `GetBuilderTypeNames` → 0; no `public static` left in `type/list/this.cs`.
4. Six suites by name vs the base: no new reds.
