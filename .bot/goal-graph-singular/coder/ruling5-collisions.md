# Ruling 5 — every name the type registry resolves to more than one class

Measured at 8779c8d29 by a probe over the live registry (`app.type.list`), not by reading code.
The registry has two directions, and a name can collide in either:

- **name → class** (`ResolveType`, `_nameToType`): first `TryAdd` wins. The C# primitives are
  seeded FIRST (`SeedClrPrimitives`), then every `[PlangType]` item and every `@this` item
  (not a scheme variant) claims its name in reflection order.
- **class → name** (`_typeToName`): what a class reports as its name. EVERY `@this` class
  records one — values and engine classes alike — and a scheme/family variant records its
  family's name.
- The **catalog** (`BuildTypeEntries` → `CatalogByName`) has its own tie-break: `Richness`
  (record 3 > closed set 2 > scalar 1 > bare 0), and the first entry wins a tie.

No code has changed. Proposed owners follow Ingi's ruling.

## A. The primitives — the C# type wins the name today

| name | classes | wins today | why | proposed owner |
|---|---|---|---|---|
| text | `System.String`, `item.text` | `System.String` | seeded first | `item.text`; `string` is its mate |
| bool | `System.Boolean`, `item.bool` | `System.Boolean` | seeded first | `item.bool`; `bool` its mate |
| date | `System.DateOnly`, `item.date` | `System.DateOnly` | seeded first | `item.date` |
| datetime | `System.DateTimeOffset` (+ `DateTime` reports it), `item.datetime` | `System.DateTimeOffset` | seeded first | `item.datetime` |
| duration | `System.TimeSpan`, `item.duration` | `System.TimeSpan` | seeded first | `item.duration` |
| guid | `System.Guid`, `item.guid` | `System.Guid` | seeded first | `item.guid` |
| time | `System.TimeOnly`, `item.time` | `System.TimeOnly` | seeded first | `item.time` |

`number` already works the way the ruling wants: `item.number` owns the name; `int`, `long`,
`double`, `decimal`, `float` only report "number" (class → name). `dict`, `list`, `tag` appear
twice only because the primitive table and the item's own `@this` name the SAME class, so those
are not collisions.

Proposed shape: the primitive table stops seeding name → C# type. The C# types are reached as mates
through the existing owned-CLR mapping (`OwnedClrTypes` → `_clr`, the one `number` uses). The static
table itself becomes instance data in 3.6.

## B. Plang values (items) that share a name

| name | classes | wins today | why | proposed owner |
|---|---|---|---|---|
| action | `goal.step.action.@this`, `goal.step.action.modifier.@this` | name → class: `action`. Catalog: **modifier** | modifier is a family variant of action, so it reports "action". In the catalog both are Richness 3, and the tie goes to the first entry, which is modifier | `action` keeps "action"; **modifier gets "modifier"** (ruling) |
| list | `item.list`, `goal.step.list`, `goal.step.action.list`, `goal.tag.list` | name → class: `item.list` | the three program lists are family variants of `item.list`, so they report "list" | `item.list` alone owns "list". A program list that needs a plang identity is `{list, step}`, `{list, action}`, `{list, tag}` (ruling) |
| path | `item.path`, `item.path.file`, `item.path.http` | `item.path` | the scheme variants report their family | **no change proposed**: file/http are schemes of path. Their identity is `{path, kind: file/http}`, which `path.Type` already gives. Tell me if Ingi reads "one name, one class" as covering schemes too. |

## C. A plang value and an engine class share a name (class → name only)

Only the item is a type (only items claim name → class). The engine class records the same
name as a concept name. Who reads a non-item's concept name: the `.Kind` a non-value concept
reports (e.g. callstack/trace).

| name | the item (owner) | engine class that also reports it |
|---|---|---|
| error | `app.error.Error` | `callstack.call.error` |
| code | `type.code` | `module.action.code` |
| permission | `item.permission` | `actor.permission` |
| tag | `item.tag` | `callstack.call.tag` |
| path | `item.path` | `variable.path` |
| list | `item.list` | 13 host/registry lists: `actor.list`, `callstack.call.child.list`, `event.lifecycle.binding.list`, `event.list`, `format.list`, `goal.list`, `goal.step.action.property.list`, `module.list`, `service.list`, `test.list`, `item.choice.list`, `item.type.list`, `type.kind.list`, `type.list`, `variable.call.list`, `variable.list`, `warning.list` |

Proposed: an engine class claims no type name. Only items name themselves; a host class keeps
its concept name only where something reads it. The host lists stop reporting "list" (ruling:
the host lists don't claim `list`).

## D. Engine concept names shared by several engine classes (no item involved)

| name | classes |
|---|---|
| call | `callstack.call`, `variable.call` |
| channel | `channel.@this` + 7 channel types (`file`, `goal`, `http`, `message`, `noop`, `session`, `stream`) |
| kind | `type.kind`, `item.kind.{dict,json,list,reflection}`, `item.number.kind` + 15 number kinds |
| reader | `data.reader`, `type.reader` |

No plang type is involved; these are concept names from the `@this` naming convention. The
proposal follows C: they stop recording a type name unless a reader needs it. I'd check each
reader when implementing.

## E. The catalog

| catalog name | entries | winner | proposed |
|---|---|---|---|
| `list<clr>` | `callstack.call.diff`, `goal.step.action.property.list`, `callstack.call.child.list`, `warning.list`, `callstack.audit`, `callstack.call.error` (all Richness 1) | `callstack.call.diff` (first of a tie) | host collections are not catalog entries. They drop out once they claim no name (C). |
| action | action, modifier (both Richness 3) | **modifier** (first of a tie) | resolved by B: modifier → "modifier" |
| path | `item.path` three times (itself + its two schemes) | `item.path` | harmless duplicate; goes if the schemes stop entering the catalog on their own |

## Questions for Ingi

1. B/path: are the path schemes one class per name (keep as `{path, kind}`), or should each scheme own its own name?
2. C/D: may every non-item class stop recording a type name, with the few that need a concept name found and kept as each reader is traced? Or should engine classes keep concept names in a separate map, apart from type names?
3. The guard (a second class claiming a taken name fails at registry build): should it cover class → name as well as name → class? With C/D as proposed, it could.
