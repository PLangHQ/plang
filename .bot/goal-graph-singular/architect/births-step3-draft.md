# Births step 3 — the type object (DRAFT, with Ingi — not sent to coder)

Status 2026-09-23: being designed with Ingi. Coder holds after step 1 (`ee03f91f2`) and asked for this plan before coding.

## What step 3 is for (from `births-pass-plan.md`)

- A type object can't reach `app.type`. An item builds a bare type (`Type => new("goal", typeof(@this))`, 36 getters). To answer anything beyond name and kind it needs a context that someone writes onto it afterwards, and `Promote()` copies the facts over (`type/this.cs:573-611`; it throws at `:591` when no context was written).
- The static fallbacks: `ClrType` falls back to a static table (`type/this.cs:163` → `GetPrimitiveOrMime`), plus `ClrFromMime` and the static primitive table (open-items #14).
- The late context stamps that are still left.
- #15 (`ReturnTypeName` as a string), #24 (`test.Create` static).
- It decides whether step A's slot (`26823c1d1`) stays.

## Settled with Ingi

1. **#27 doesn't need step 3.** The builder menu's parameter types already come from `app.type` (`goal/step/action/property/this.cs:36`, `types[value]`), and a choice slot's type carries its values (`type/list/this.cs:205`). The template never prints them: `os/system/builder/llm/templates/propertiesUser.template:20` prints `Name` and `Kind` only. The fix is one template line.
2. **`%x!type%` and `app.Type["image"]` are the same concept, a type.** `app.type.list` is the list of types, and `app.Type[name]` selects one. `%x!type.Description%` must not be empty.
3. **The facts live on the entries in `app.type`.** Those are description, fields, values, properties, shape, constructor signature, example and kinds. A value's type answers them by reading its entry. Ingi: `Description => Context.App.Type[Name].Description`, the owner answering with a one-line member, not a copy.
4. **The context comes from the one asking, not from the type object.** Ingi: "`%x!type%` goes through memorystack, can we attach the context there?" It is already there. Every hop of `%x!type.Description%` is asked by a Data that carries the memory stack's context:
   ```
   memory stack Get("x")
    → x's Data "!type": GetInfrastructureValue      (data/this.Navigation.cs:266-269) — child Data born with x's context
    → child "Description": navigation door          (type/item/this.cs:235-237) — parent.Context
   ```
   So the type object navigates as its entry, found with the asker's context:
   ```csharp
   // type/this.cs — NEW
   public override ValueTask<data.@this> Get(data.@this parent, string key)
       => new global::app.type.clr.@this(parent.Context.App.Type[this], parent.Context).Get(parent, key);
   ```
   `App.Type[this]` (NEW) returns the entry for this name and kind, and returns an entry itself when asked with it. For choice, the lookup uses name and kind together, because the values depend on the set.
5. **Items need no context for their type.** The earlier question ("do text/number hold a context? option 1 or 2") falls away.

**What goes:** `Promote()` and `_foldLoaded`; the type object's `Context`; the stamps `data/this.cs:426` (`minted.Context ??= _context`), `type/this.cs:428` (`{ Context = context }`), `data/this.cs:192` (`other.Context ??= _context`); navigation's stamp on walked values, `data/this.Navigation.cs:105-106` (`contextual.Context = _context`).

## Trace A: C# uses of a type object that need `app.type` (done 2026-09-23)

**Result: every site that needs `app.type` already holds a context when it makes the call. None needs the type object to carry one.**

| Use | Sites | What it needs | Context at hand |
|---|---|---|---|
| `Is(other)` / `Is(name)` (`type/this.cs:485-501`) | `data.Is` | name only | not needed. The stamp `data/this.cs:192` (`other.Context ??= _context`) is useless |
| `ClrType` on an entry already from `app.type` | `data/reader/this.cs:99`, `type/item/this.cs:619`, `type/this.cs:258, :287` | the entry's stamped class | caller asked `context.App.Type[...]`. No change |
| `ClrType.Exit()` on a result's type | `data/ShouldExit.cs:31`, `path/this.Operations.cs:43, :144, :158`, `module/action/file/read.cs:74` | the class (Exit = `IExitsGoal`) | the result Data's context. The same check is written out in 5 places, so it belongs on one owner |
| `ClrType` on a declared type (from `.pr`) | `variable/set.cs:32` (strict probe), `:217` | the class | the handler's `Context` |
| The `Create` doors: `Creatable` → `ClrType` (`type/this.cs:361-367`) | `data/this.cs:250, :303`, `type/item/this.cs:90`, `reflection:168`, `file/this.Operations.cs:78, :97`, `variable/set.cs:227, :293`, `channel/this.cs:291`, `data/reader/this.cs:101` | the class | each door already takes a context (or a Data with one). Its binder gets it (`Bind(raw, ctx)`, `:339`) but asks `ClrType` without passing it |
| `Compressible` (`type/this.cs:168-181`, `Context?.App.Format`) | `data/this.Transport.cs:54` | the format registry | the Data's context |
| `Scheme` (`type/this.cs:550-551`) | no C# reader (path and image use `App.Type.Scheme` directly) | — | dead, or navigation only |
| The eight facts | builder/catalog read entries from `app.type`. No plang or template reads them off a value's type (grep of `os/`, `Tests/`) | the entry | navigation, via the asker's context |

**Where type objects are made today.** The 36 item getters. The registry itself (`type/list/this.cs:185` for primitives, `:205-206` for kinded types and choice). `context.Type.Create(name)` (`type/factory.cs`, 7 callers) plus the static `type.Create(name, …, context)` (8), which is a second door for "a type by name" beside `app.Type[name]`. `type/serializer/Reader.cs:38` (declared types read from `.pr`). `type/kind/this.cs:53`. Seven `new type(...)` outside `type/`. The static helpers `String/Int/Long/Decimal/Double/Bool/DateTime/FromMime` (`type/this.cs:209-217`).

**Late context stamps on type objects (all die):** `data/this.cs:192` (useless), `data/this.cs:426`, `type/this.cs:428`, `type/kind/this.cs:53`, `type/serializer/Reader.cs:38`, `variable/set.cs:211`.

**Found: `variable.set` changes a type object's `Kind`** (`variable/set.cs:207`, `:228`: `type.Kind = ...`). The type object comes from the parameter's value, and step 1's run copy shares that value with the program row. So this writes on the shared program. If type objects become `app.type`'s shared entries, it would also corrupt the registry. `Kind` must become read-only, and `variable.set` gets a new type object instead.

**Step A's slot is dead.** `app.Context` is only ever set (`goal/step/action/this.cs:153`, `goal/this.cs:287`, `Start()`). Nothing in production reads it.

**B. The static tables** — `GetPrimitiveOrMime` (4 references), `ClrFromMime` (4), the primitive table (10 reads). Once A is traced, decide for each: move it onto its owner, or delete it.

**C. Step A's slot.** It's dead (see trace), so remove it.

**D. #15, #24** — unchanged.

**E. One door for "a type by name".** `context.Type.Create(name)` and the static `type.Create(name, …)` sit beside `app.Type[name]`.

## Next

Bring the trace to Ingi, then write the coder plan.
