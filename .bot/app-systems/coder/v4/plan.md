# coder v4 — stage 4: the collected type

The plan's stage 4 row is in `.bot/app-systems/architect/plan.md`, as carried by plang-40 after stage 3.
This file holds the trace, what doesn't hold as written (with proposals), and the order.

## Scope (plang-40's carry)
- `type<T>` (`type/this.Generic.cs`): `list`, `Get(key)` (the one async door, no C# indexer),
  `current(context)`.
- `IMatch` / `ICurrent` / `IList`, one question each; the strict `list<T>`.
- `item.list` → `item.history`.
- The registry as `list<type>` at `app.type.list`: no stored context; the lookups that need the format
  take the caller's context.
- The global kind store moves to item's kind list.
- The type's navigation: its own members through today's clr reflection; a miss is NotFound; then
  `Get(key)`. The Data built there takes its context from the parent.
- `App.Type` → `app.type`.

## Trace (today, after stage 3)
| fact | where | count |
|---|---|---|
| `App.Type` references | production / tests | 70 in 42 files / 217 |
| item history (`item.list`) uses | source.cs:146,161, image:165, file:49,106, ICreate.cs:66 (error's own causes, not history) | 5 production + tests |
| `list<T>` element types lacking `ICreate` | test (16 uses), test timing (2), LlmMessage (1), type (1) | 4 classes |
| registry `Context` | `Mime` (:77), identity door canonicalising (:101), the `new(context)` ctor | 3 |
| `App.Type.Kind[…]` (the global kind store) | kind/this.cs navigation hop (:85, every hop), Output (:182), clr ctor, source, wire, OpenAi, variable.set, Json.cs, item list kind (×2), path.Resolve | ~12 production |
| `list.@this` storage | `List<object?> _items` behind a lock; enumerating copies `Rows` (ToArray) | every walk allocates |

## What doesn't hold as written, with proposals

1. **`App.Type` → `app.type` is two changes, not a rename.**
   - The plan table puts the registry's lookups on the list: `app.type.list.Mime(…)`, `app.type.list[System.Type]`, the identity door, `Reader`.
   - `app.type` itself is the type named `type`, with `Get(key)` async.
   - So each of the 70 production and 217 test sites becomes either `context.App.type.list[…]` (sync lookups, Mime, Extension, Reader, Renderer, Add) or `await context.App.type.Get(name)`. The async door is plan group 3 ("a name the programmer or the .pr wrote"), and there a 404 must become a result instead of today's throw.
   - **Proposal:** stage 4 moves every site to `app.type.list[…]`, same behaviour and sync, with the throw kept. The move to `await app.type.Get(…)` then happens site by site where the plan's group 3 applies: variable.set's declared type, http, the condition operator. Group 2 (stamping) is stage 9's. This keeps stage 4 compiling commit by commit.
   - **Property name:** `app.@this` gets `public type.@this<type.@this> type`. Inside `app.@this`, a lowercase `type` hides the namespace, so references there are written `global::app.type…` (plan's C# note).
   - **Context property:** `context.App` stays PascalCase; it's the actor context's pointer, not a plang node.

2. **The registry as `list<type>` holds its types in list slots.**
   - Every walk copies the rows under the list's lock.
   - The identity door and `this[System.Type]` run on every raw lift (`item/this.cs:90`) and every `.pr` type read.
   - **Proposal:** make the registry `list<type>` as the plan says, and measure the hot lookups (the C# suites' time and a `plang --test` run) before and after. If it regresses, bring numbers before adding anything; no index unless a measurement asks for one.
   - **Two ways in:** `list<T>`'s public `Add(T)` is non-virtual, so after the change it is a second way in beside `Add(clr, context)`, bypassing sealed and one-name checks.
   - **Proposal for that:** hide it in the registry (`new`) with a version that runs the checks, or make the base `Add` virtual (the plan names "Add/Remove made virtual" as the path if an index returns). Which do you want?

3. **`Get(key)` walks `list.all()`, but `all`/settings are stage 7.** Proposal: stage 4's `Get(key)` walks the `list` itself (types are all in memory); `all(setting)` arrives in stage 7 with goal's lazy list, and Get switches to it then.

4. **The global kind store → item's kind list.**
   - The store answers three questions today:
     - by name, for kinds of any type (json, md, int, operator, file);
     - by C# class (`ClrForm`), on every navigation hop;
     - by type (`kind.list`).
   - The plan says item's kind list selects json/list/dict/`*` by name and ClrForm. The other kinds (number's, hash's, choice's sets, path's schemes, formats) belong to their own types.
   - **Proposal:** a type's `kind` answers `kind[name]` (one of that type's kinds) and `kind.list(context)`. The per-hop C# class selection is item's: `app.type.list["item"].kind[clrType]`.
   - The empty kind a type is born with has no context, so the kinds are held by the type list (`app.type.list`, selection + lifecycle), and a type's kind asks the list it came from. That means the kind carries its type list, a back-reference.
   - **Alternative:** keep one kind collection on the type list (`app.type.list.kind`, internal selection, no plang face), and have plang reach kinds only through `%!app.type.X.kind…%`.
   - **Proposal:** the alternative. The plang face is per type; the C# selection is the type list's own. This supersedes "store at app.type.Kind, move in stage 4". Please confirm.

5. **`type.@this` gains `ICreate<type>`.** The instance `Create(object?, context)` and `Create(object?, data)` clash with the static interface members; C# allows an explicit static interface implementation, so type implements `ICreate<type>` explicitly (plan: "a C# detail, coder's"). `type` is unsealed for `type<T>`.

## Order (each its own commit, green against the baseline)
1. `item.list` → `item.history` (`app/type/item/history/this.cs`).
2. Strict `list<T>` (`ICreate<T>`): test, test timing, LlmMessage and type gain ICreate; type is unsealed.
3. `IMatch`, `ICurrent`, `IList` + `type<T>` (`type/this.Generic.cs`: `list`, `Get(key)`, `current(context)`, navigation: members first, a miss is NotFound, then `Get(key)`); PlangName names a `type<T>` "type".
4. The registry becomes `list<type>` (per the answer to 2); no stored context; `Mime(mime, context)`, the identity door canonicalises with the caller's context. The kind collection per the answer to 4.
5. `app.type` is `type<type>` (`type = new(this)`, created before the actors), and `App.Type` → `app.type.list` at every site (production by Edit, tests by the helper agent).
6. The prompt teaches `%!app.X["key"]%` once, then twins or one eval run.
