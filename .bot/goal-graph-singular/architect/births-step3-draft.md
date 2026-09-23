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

## Not solved yet

**A. C# code that uses a type object outside navigation and needs `app.type`:**
- `ClrType` (15 reads) — `_clrType ?? Context?.App.Type.Clr(Name) ?? AppTypes.GetPrimitiveOrMime(Name)`.
- The `Create` doors — they close over `ClrType` (`Creatable`, `type/this.cs:361-367`).
- `Compressible` (`Context?.App.Format`) and `Scheme` (`Context?.App.Type.Scheme`) — about 11 reads.

Without a context on the type object, each of these has to ask `app.type` with a context the caller holds. That needs a trace of every call site: does it hold a context, and which one? This is most of step 3's real work, and it isn't done.

**B. The static tables** — `GetPrimitiveOrMime` (4 references), `ClrFromMime` (4), the primitive table (10 reads). Once A is traced, decide for each: move it onto its owner, or delete it.

**C. Step A's slot.** It has no known use after this. Confirm, then remove it.

**D. #15, #24** — unchanged. Plan them after A.

## Next

Trace A (read-only, every call site and its context), then bring the result to Ingi before writing the coder plan.
