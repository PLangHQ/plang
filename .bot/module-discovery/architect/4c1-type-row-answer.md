# 4c.1 ruling — the row carries the ENTITY; generics ride the KIND axis; `getTypes` → `goal.variables`

Answer to `coder/to-architect.md` (the 4c.1 fork + the model #4 correction), settled with Ingi 2026-07-15.

> **You own this.** Code below was reviewed in chat for shape; bodies and factoring yours. `file:line` verified against the session's reads — re-verify at implementation.

## Model #4 correction — ACCEPTED, plan updated

Choice registration belongs to `app.type.Choice` (`Register(assembly)`, per-assembly trigger), not the module collection's walk — your trace stands (choices aren't a module concern; 7 of ~9 inner types are enums with no `[Choices]`, so sets are only identifiable by `choice<T>` usage). The plan's model #4 is corrected; 4b/4c must not re-assume the module walk owns choices.

## The 4c.1 ruling — B, and the compound case is ALREADY modeled: kind

Not A (a name string rebuilds the string-typed shadow that killing `getTypes` is meant to end), and not B-with-a-new-`Element`-axis (Ingi caught that as a fork): **the system already carries a generic's element as the KIND — `choice<Operator>` rides the wire as `{name:"choice", kind:"operator"}`, and the choice reader resolves the closed type FROM the kind.** Generalize exactly that:

- `list<path>` = **`{name:"list", kind:"path"}`**; `dict<…>` = kind names the VALUE element (key defaults text — if a real param ever needs a keyed axis, surface it, don't design it speculatively).
- **Nothing new on `type.@this`** — Name+Kind exist, the `.pr` type slot already carries kind, the reader registry is already keyed `(type, kind)`. Bonus that falls out free: a wire slot declared `{list, path}` hands its kind to the list reader → elements born typed `path`.
- **Kind is DERIVED from the C# truth, never stored beside it** (Ingi's model sentence: *C# generics remain the mechanism; the kind is their plang face*). The element fact always existed — in `list.@this<T>`'s type parameter, in host `PropertyType`s, in `Data<T>` — invisible to plang. This surfaces it; a second stored field would be the stored-twice smell.
- **The face is per-type**: each type owns how its kind displays — list composes `"list<path>"`, image composes its mime form. Templates print `{{ p.Type }}`; parity with today's `GetTypeName` strings is the entity's own responsibility, provable in the golden.
- Nested generics (`list<list<path>>`): you measured none in real params — the kind token stays a simple name; if one appears, surface it.

### The one new door — land BEFORE the rows; it is the shared owner

`this[System.Type]` (the three-rung identity door) gains one rung, between the item rung and the clr fallback:

```csharp
// a closed generic of a registered family answers {family, kind: element} —
// the choice precedent generalized. Kind DERIVED from the C# generic argument.
if (clrType.IsGenericType && this[clrType.GetGenericTypeDefinition()] is { } family)
    return new app.type.@this(family.Name, Name(clrType.GetGenericArguments()[^1]));
    // list.@this<goal> → {list, kind:"goal"}; List<path> → {list, kind:"path"};
    // Dictionary<,> → value element ([^1]); closed choice<T> registrations unaffected (they hit earlier rungs)
```

Verify: the open-generic family resolves through the door (`this[typeof(list.@this<>)]` — if the open definition isn't indexed, key the rung off the definition's registered name); closed `choice<T>` keeps its existing resolution. This rung serves all three consumers — property rows (4c.1), `action.Return` (below), `goal.variables` (4e) — one owner, no row-builder-local minting.

### The row (4c.1) — unchanged from the draft except the type field

`property.@this` carries `Type : app.type.@this` (the entity, compound-as-kind), `Nullable`, `Default`, `IsVariable`, `Name`. Everything else you listed (filters mirrored from `Describe()`, the `IChannel` synthetic row) proceeds as planned.

## `getTypes` → **`goal.variables`** (Ingi named it) — the 4e rewrite, code reviewed

Same forward walk (that part was always right); every string replaced by the entity, every hack by a door:

```csharp
// the working map holds ENTITIES:
var working = new Dictionary<string, global::app.type.@this>(StringComparer.OrdinalIgnoreCase);

// variable.set arm — no TypeNameOf name-fishing: the declared type IS an entity on the .pr
working[name] = hinted                                            // the Type slot: already a type.@this
             ?? (isChainData ? chainReturn : valueParam?.Type)    // data.Type IS the entity
             ?? Context.App.Type["item"];

// foreach arm — THE REGEX DIES; the kind axis answers the element:
working[itemName] = collEntity?.Kind is { } k
    ? Context.App.Type[k.Name]                                    // {list, kind:"goal"} → the goal entity
    : Context.App.Type["item"];

// chain-return arm — no reflection here; the catalog element owns it:
chainReturn = Context.App.module[a.Module]?.Actions[a.ActionName]?.Return;
```

- **`action.Return`** (class-zoom partial, replaces `DescribeReturnTypeName`'s string): `public global::app.type.@this? Return => _return ??= ReflectReturn();` — Run()'s `Task<Data<T>>` → the entity for T via the door (compounds ride kind); bare `Task<Data>` → null (polymorphic).
- **Output shape unchanged** — `list<dict>` so `%varTypes[step.Index]%` still indexes — but the dict VALUES are entities (type is a plang value; rides in a dict natively). Templates print `{{ v }}` → the entity's face. The LLM can never see `string`/`int`/`object` again because nothing in the pipe is a string.
- **Dies with the rename**: `TypeNameOf` (the name-fishing switch), `ElementOf` (the regex), `DetermineReturnType` (per-call reflection — cached on the element now), `ToValueType`'s object→item folding (entities are canonical at birth), `GetTypeNameStatic` leaves this path. The goal line becomes `- goal.variables Goal=%goal%, write to %varTypes%` (`BuildStep/Start.goal:19`); `getTypes` the name dies with the shadow it carried.
- Name reasoning on record: `variables` names what you get back (the goal's variables, per step, typed — noun-answer actions have precedent: `list.count`, `list.first`); `scope` was accurate but jargon; the per-step slicing is said by the return shape.

## Addendum — `GetTypeName`'s fate (Ingi asked; traced 2026-07-16)

`GetTypeName` (type/list/this.cs:411-478) is THREE questions fused into one method, and the ruling above already re-homes two of them:

1. **Slot conventions** (:415-424 — `Nullable<T>` → `"?"`, `Data<T>` unwrap, `choice<T>` surfaces as T): declaration-slot facts, not type names. Already re-homed: `Nullable` is a ROW fact; the `Data<T>` unwrap is the reflection leaf's job (your `this.Schema.cs` mirrors it — correct).
2. **Compound naming** (:429-461 — `"list<path>"`, `"dict<k,v>"`, arrays): the string shadow. Becomes the door's generic rung + the entity's own face. **One gap my rung code missed, found in this trace: the door also needs the ARRAY rung** — `T[]` → `{list, kind: element}`, `byte[]` → the primitives-table answer — `GetTypeName` handled arrays at :448-454 and `IsGenericType` doesn't catch them.
3. **Leaf naming** (:463-477 — primitives table → `_typeToName` → lowercased fallback): the one legitimate question. It survives as **`Name(System.Type)`**, which stops being an alias (:482) and becomes the method, leaf-only:

```csharp
/// <summary>The plang NAME of a CLR type — the naming index (vocabulary, kind tokens,
/// display), distinct from the identity door (construction). LEAF names only: slot
/// conventions (Nullable/Data<T>) are the reflection leaf's row facts; compounds are
/// entities {family, kind} whose own face composes "list<path>".</summary>
public string Name(System.Type clrType)
{
    if (app.type.primitive.@this.Canonical.TryGetValue(clrType, out var name)) return name;
    EnsureInitialized();
    if (_typeToName.TryGetValue(clrType, out var declared)) return declared;
    return StripGenericArity(clrType.Name).ToLowerInvariant();
}
```

**Deleted at 4e:** the `GetTypeName` spelling + fused body; `GetTypeNameStatic` whole (:121-157 — it is a FORK: the same rungs duplicated statically, and all four of its callers are Describe/getTypes paths that die at 4e anyway).

**Caller dispositions (all seven production sites):**

| Caller | End state |
|---|---|
| `module/list/this.cs:329,528` (Describe param loops), `:495` (DescribeReturnTypeName) | die at 4e with Describe |
| `getTypes.cs:216` | dies at 4e (`goal.variables` reads `action.Return`) |
| `build/code/Default.cs:914` (schema stamp) | slot-unwrap once (the loop already does at :927-929) → the DOOR → `p.Declare(entity)`; the `!= "object"` filter becomes `entity.Polymorphic` |
| `spec/render/this.cs:177` (example type tags) | the door → the entity FACE (`this[unwrapped].ToString()`); rides into the examples door at 4e |
| `choice/list/this.cs:50` (closed-set kind token) | `Name(inner)` — genuinely leaf naming, stays as-is |
| the door's generic/array rungs | use `Name(element)` for kind tokens — leaf naming INSIDE the door. This is WHY Name survives: `list<goal>`'s kind must be `"goal"` (the vocabulary answer), never `"clr"` (the construction answer) — naming and identity are two doors by design. |

Net after 4e: two consumer kinds remain — the door (kind tokens) and display/teaching faces — both on `Name()`, ~5 lines, one question. The verb+noun spelling does not outlive the stage.

## Landing order

Door rung → 4c.1 rows (+ `action.Return` while you're in the partial) → templates/parity (4d) → `goal.variables` at 4e with the deletions.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| compound = {family, kind} | the existing axis, choice precedent; no new member, no Element fork | ok |
| kind derived from C# generic | one truth (the mechanism), one face (the kind); no stored-twice | ok |
| entity face per type | the value owns its display; templates print, never compose names | ok |
| the door rung | one owner for CLR-type→entity across rows/Return/variables | ok |
| `goal.variables` | noun-answer action naming what it returns; getTypes verb+noun dies | ok |
| entities in the output dict | type is a plang value; no name strings anywhere in the pipe | ok |
