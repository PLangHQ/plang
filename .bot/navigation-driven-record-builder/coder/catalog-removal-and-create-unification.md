# Remove `catalog`; `ICreate<T>.Create` is the one construction door

**For the architect. Decided by Ingi (2026-07-08), folds INTO this branch** (`navigation-driven-record-builder`) — not a separate effort. This extends the navigation-driven plan: the record-builder's thesis ("the target owns building itself from a source") is the *same door* we're now finishing for **every** type. Two names go away: `catalog` (the concept) and `convert.OfStatic` (the CLR-keyed dispatch).

---

## Decision 1 — `catalog` is not a plang concept; delete the name

`App.Type` is `type.catalog.@this` — a made-up god-object stapling three unrelated jobs:

1. **Type-identity registry** — `[name]→entity`, `[clrType]→entity`, `Get`/`Clr`/`Register`/`GetTypeName`, `[PlangType]` assembly index.
2. **Schema fold** — `BuildTypeEntries`/`ComplexSchemas`/`GetValidValues` (LLM teaching).
3. **A bag of 8 sub-registries it only parents** — `Choices`, `Scheme`, `KindHooks`, `Kinds`, `Conversions`, `Compares`, `Renderers`, `Readers`.

**The list of all types IS `list<type>`.** Ingi's call, and it holds under trace: `app/type/list/this.cs` is *"the native PLang list/array value type"* — the generic `list<T>`. A registry of types is just `list<type>` (element = `type.@this`, which is itself a plang value). So `app.type.list` = `list<type>`. **No name collision** — the type-registry is an *instance* of the list value type, not a rival class. `list` appearing as one element of `list<type>` is self-reference in data, fine.

I looked for a genuine diff between "the list value type" and "the list of all types." There is no *conceptual* one. The only residue `catalog` carries beyond a bare `list<type>`:
- **name/CLR keyed lookup** → a keyed find over the list; the O(1) dict is a **perf index carried on the collection**, not a second concept.
- **schema fold** → each `type` entity already self-describes lazily via `Promote()` (`type/this.cs:804`); the fold is a **view over `list<type>`**, behavior on the element.
- **8 sub-registries** → the god-object smell itself. They **dissolve/rehome** (below), they are not a property of the collection.

### Where catalog's jobs go

| catalog job | new home |
|---|---|
| identity registry (name/CLR/entity) | `app.type.list` = `list<type>` + a keyed lookup index on it |
| `[PlangType]` assembly scan | the list's **source** (how `list<type>` is populated) |
| schema fold (`BuildTypeEntries`/`ComplexSchemas`) | a **view** over `list<type>`; each `type` already promotes its own Fields/Values/Example |
| `Conversions` (`convert.@this`) | **dissolves** — Decision 2 |
| `Readers` / `Renderers` | reached directly (`app.type.readers`) or onto the `type` element — arch's call |
| `KindHooks` / `Kinds` / `Compares` / `Scheme` / `Choices` | reached directly under `app.type.*`; not parented by a "catalog" node |

Net: `type.catalog.@this` is deleted. `App.Type` becomes the `type` collection node (`app.type` — select `app.type["number"]`, enumerate `app.type.list`), with the sub-registries hanging off `app.type.*` directly rather than off an invented parent.

---

## Decision 2 — one construction door: the type owns `Create`; kill `convert.OfStatic(clrType, …)`

`convert.OfStatic(clrProp.PropertyType, value, kind, ctx)` — a reflective dispatch **keyed by a CLR `System.Type`** — is the OBP violation. The object should own its own construction: *"make yourself from another value."* And — Ingi's point, confirmed — **"convert into myself from another" is just `Create`.** Today that one operation wears three faces:

```
convert.OfStatic(clrType, value, kind, ctx)   // reflective, CLR-keyed        ← the violation
type.@this.Convert(value, ctx)                // entity router → OfStatic       (type/this.cs:187)
ICreate<T>.Create(value, data)                // the target builds itself       (item/ICreate.cs:30)  ← the real one
```

**Collapse to `ICreate<T>.Create`.** The type/value owns building itself from another; there is no central CLR-keyed dispatcher above the types.

The one thing the dispatcher legitimately did — map a *raw* CLR value with no plang wrapper (`typeof(int) → number`) — already lives on `type.@this.Create(raw, ctx)` (`type/this.cs:439`), with each family declaring `OwnedClrTypes`. So `OwnerOf` + the `_cache`/`Discover` reflection become a **perf index behind `type.Create`**, not a concept and not a call site anyone reaches directly.

### This is the navigation-driven builder, generalized

The record-builder already says "the target pulls itself from a navigable source." Decision 2 is the same statement for *every* type: **every type builds itself from another via `Create`.** That's why it folds into this branch instead of trailing it — finishing `ICreate.Create` as the sole door *is* the mechanism the plan's Stage 2 already reaches for. The plan's async sweep (`ICreate.Create` + `list<T>.Convert` async) is the enabling step; this decision says the async `Create` is the **only** door, and `type.Convert`/`convert.OfStatic` fold into it rather than living beside it.

### What this does to I1 (the deep-write)

I1's fix restated cleanly: the deep-write (`variable/list/this.cs` `SetValueOnObject`, three divergent arms calling `OfStatic`/`iv.Clr`) routes through **the slot type's own `Create`** — one door, retype-to-slot. The three arms collapse to the same three lines; the convert-vs-lower divergence (Smell #4) dies; and `clr(json) → list<action>` falls out because `Create` routes to `list<action>`'s element build (the hook Stage 1 already fixes to accept a navigable carrier). No bespoke `OfStatic` from the write site.

---

## Decision 3 — action discovery is `app.module.list : list<module>`; the LLM prompt is Fluid over it

The catalog's `BuildTypeEntries(modules)` walk and `module.@this.Describe()` both reach into the module registry and reflect action shapes into a C# schema (`StepActions` / `List<data>`). That's behavior on the wrong owner: **the module registry owns "what modules/actions/properties exist."** Today it leaks that as bare strings —

```
module.@this today:
  list            : IEnumerable<string>      // bare module NAMES  ← producer-hands-raw smell
  GetActions(ns)  : IEnumerable<string>      // bare action names
  GetActionType() : System.Type              // CLR reflection
  Describe()      : StepActions              // a C# schema-builder reflecting props into List<data>
```

— forcing catalog + `Describe()` to re-derive the shape. Ingi's call: the registry hands back plang types, and rendering is a template.

```
app.module.list : list<module>
  module.Actions    : list<action>       // module = NAMESPACE, action = CLASS (names are mechanical)
    action.Properties : list<type>       // keyed by property NAME; value = the prop's plang type
```

**No `field` type.** A property is a plang `type`; the property **name is the entry key** — navigation yields `(key=name, value=type)` pairs, exactly as dict/clr enumerate today. `action.Properties` is a keyed `list<type>`, not a list of `{name,type}` wrappers.

### (a) reuse the current classes — the tree is a navigable VIEW over reflection (NOT a descriptor copy)

Decided: **(a).** The handler class (`variable.set`, `file.read`) *is* the definition; `module`/`action` are thin navigable wrappers over the namespace / `System.Type` — the same idea as `clr` navigating a host object, applied to type metadata. Nothing is materialized or copied, so there is one source of truth and no drift.

```csharp
// module.@this : item.@this  — backed by a namespace + registry, holds NO copy
public list<action.@this> Actions =>
    new(_reg.GetActions(_ns).Select(a => new action.@this(_reg.GetActionType(_ns, a), _ctx)), _ctx);

// action.@this : item.@this  — backed by the LIVE handler System.Type
public string Name => registry.NameOf(_handler);           // class name
public list<type.@this> Properties =>                       // keyed by prop name; value = plang type
    _handler.GetProperties(Public | Instance)
        .Where(p => p.Name is not "EqualityContract" and not "Context")
        .ToKeyedList(p => p.Name, p => _ctx.App.Type[Unwrap(p.PropertyType)]);   // Data<T>/[Code]T → plang type
```

Rejected **(b)** — a stored `ActionDefinition` record mirroring each class: two sources of truth (class + descriptor), drift on every handler edit, extra classes. Single-source kills it.

### Naming is mechanical — no fork

`module` = namespace, `action` = class; both names fall out of reflection. The runtime `action.@this` (`app/goal/steps/step/actions/action/this.cs`, the executing node holding filled param Data) and the module-tree `action` (a view over `System.Type`) are the **same action at two zoom levels** — class vs instance — in different locations. Not a competing name, not a descriptor.

### What deletes

- `module.@this.list : IEnumerable<string>` → `list<module>`; `Describe()` / `StepActions` / `GetDefaults`-as-schema → gone.
- `BuildTypeEntries(modules)` → **a projection over `list<module>`**: "which types must the prompt teach" = collect `action.Properties`' types across the filtered modules. Discovery-of-referenced-types stops being catalog's job.
- The compile prompt = `Fluid(list<module>)` (candidate modules) + the referenced types self-describing (Job 2 fold). Two template renders over plang collections; no C# schema builder survives.

This is the concrete form of "schema-fold-as-view" (open item #5 below): the fold was never one thing — **discovery lives on `app.module.list`, per-type shape lives on the `type` element.** Catalog held neither.

---

## Scope note (Ingi: into this branch)

This grows `navigation-driven-record-builder` past "unblock the builder" into "delete `catalog` + unify construction on `Create`." Accepted deliberately — they are the same door, and splitting would tangle the same call sites across two branches. The async sweep (plan Stage 0) still lands first as prep; catalog-removal + `Create`-unification ride on top, replacing the plan's Stage 2 "generic default" with the stronger "there is only `Create`."

## Open for the architect

1. **`list<type>` bootstrap** — the `list` type registering itself as an element; order of population vs. the type system being available. (Data self-reference, but the *construction* order needs a pass.)
2. **Keyed-lookup index home** — the name/CLR → entity O(1) index lives *on* `app.type.list` (the collection owns its own index) vs. a thin lookup surface. Confirm it's on the collection, not a revived side-registry.
3. **Sub-registry rehoming order** — `Conversions` dissolves into `Create`; the other 7 (`Readers`/`Renderers`/`KindHooks`/`Kinds`/`Compares`/`Scheme`/`Choices`) move from `catalog`-parented to `app.type.*`-direct. Which move in this branch vs. get a follow-on? (`Conversions` is mandatory here; the rest may be mechanical rename-only.)
4. **`type.Convert` callers** — every `App.Type.Conversions.Of(...)` / `convert.OfStatic(...)` / `type.@this.Convert(...)` site becomes `…Create(value)`. Sweep list + the handful that pass a `kind` (does `Create` carry kind, or does the type entity already hold it?).
5. **Schema-fold-as-view** — resolved by Decision 3: discovery → `app.module.list`, per-type shape → the `type` element. Remaining check: confirm the Fluid-render path replaces `Describe()`/`StepActions` without regressing LLM teaching (examples, defaults, return types currently folded in `Describe()`).
6. **Module-tree as reflection view (Decision 3, (a))** — `module`/`action` as `item.@this` views over namespace/`System.Type`. Confirm the confirm-before-converting shapes (I'll show `module` + `action` view classes one at a time), and where `action.Properties`' keyed `(name → type)` navigation lives (the `ToKeyedList` seam — is it dict-backed or a new keyed-list projection?).
