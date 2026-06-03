# Stage 3: Lazy Data — `{ raw, type, kind, value:lazy }`

> **Note for coder:** every field name, getter shape, and rule below is a **suggestion** that captures architect intent — not a contract. You own the implementation, especially the `_raw`-vs-`_value` field split and the mutation-invalidation trigger. Push back if a rule reads wrong.

**Goal:** `Data` gains a raw backing and materializes its value lazily — through the Stage 1 reader — only when something touches it. A value that is never touched serializes its raw straight back out: free verbatim passthrough, and signatures verify against exactly what arrived. This is the core shift from eager to lazy.

**Scope:**
- `app/data/this.cs` — add `_raw`; `.Value` materializes via the reader when `_value` is null and `_raw` is set; drop `ConvertValue`.
- `app/data/this.Navigation.cs` — navigation reads `.Value` (which materializes); remove the `ConvertValue` call.
- `app/data/Wire.cs` — `Wire.Read` captures the value slot raw and defers; **delete `LiftDataIfShaped`**.

**Dependencies:** Stage 1 (the reader registry is what `.Value` materializes through). Stage 2 is not a hard dependency but should land first so numbers materialize to exact kinds.

**Out of scope:**
- The channel boundary (Stage 4) — Stage 3 makes `Data` able to hold raw + defer; the *sources* that produce raw-backed Data land in Stage 4. Stage 3 can be proven via `Wire.Read` and direct construction.
- Access-pattern resolution (Stage 5) — Stage 3 materializes on `.Value`; the scalar-vs-navigate-vs-cast rules are Stage 5.

**Deliverables:**

1. **`_raw` field + lazy `.Value`.**
   ```csharp
   private object? _raw;      // undecoded source form: string for text, byte[] for binary
   private object? _value;    // materialized; null until first touch of a raw-backed Data
   private type?   _type;     // carries Name + Kind

   public virtual object? Value {
     get {
       if (_valueFactory != null) { _value = _valueFactory(); _valueFactory = null; }
       if (_value == null && _raw != null)
         _value = Materialize();   // reader.Of(_type.Name, _type.Kind)?.Read(_raw, _type.Kind, ctx)
       return _value;
     }
   }
   ```
2. **Materialize only when `_value` is null and `_raw` is set.** Inline-authored values (`set %x% = 5`) populate `_value`, leave `_raw` null, and never hit the byte path — so the existing `%var%`-resolves-fresh-per-read contract (`app/data/this.cs:152`) is untouched. Which field is set tells you the origin; no mode flag.
3. **`_raw` is `string | byte[]`.** Text stays text (a json file's raw is the json string), binary is `byte[]`. No utf-8 encode tax on the common path. Name it `raw`, not `bytes` — it holds the source form, not always bytes.
4. **`_raw` stays authoritative until a mutation.** Materialization is a read-through: it sets `_value` but does **not** clear `_raw`. On serialize, if the value was never touched (`_value` null), emit `_raw` verbatim — the passthrough and signature-safe form. A mutation (`SetValueDirect`, navigation-set) invalidates `_raw`, so serialize then renders from `_value` via the renderer. Nail the exact set of operations that count as a mutation — this is the rule passthrough and signing depend on.
5. **Fold `ConvertValue`.** The string→typed-on-first-navigate path (`app/data/this.cs:199`, `this.Navigation.cs`) is subsumed by materialize-from-raw. Remove `ConvertValue` once navigation reads `.Value`.
6. **Keep `_valueFactory` / `DynamicData`** (`app/data/this.cs:186`, `:1205`). Different laziness — recompute-on-every-access (a live view) vs materialize-once-and-cache. Two lazinesses that mean different things; keep both, say why.
7. **`Wire.Read` goes lazy; `LiftDataIfShaped` deletes.** Today `Wire.Read` (`app/data/Wire.cs:141`) eagerly deserializes the value slot, and `LiftDataIfShaped` (`:346`) sniffs the value's json shape (`name`+`value` keys) to guess whether it's a nested Data, with a `GetRawText` double-parse. Change `Wire.Read` to capture the value slot's raw json into `_raw`, stamp `type`/`kind` from the type slot, and defer. The shape-sniff and double-parse go: the type slot says what the value is, and a genuinely nested Data is rebuilt by the containing type's reader (e.g. `Signature` rebuilds its Data field).

## Design

**The two origins of a Data, distinguished by which field is set.**

| Origin | `_value` | `_raw` | `.Value` behavior |
|---|---|---|---|
| Authored in-script (`set %x% = 5`, a literal, a `%var%`) | set | null | returns `_value`; `%var%` resolves fresh per read (unchanged) |
| Read from a source / off the wire | null (until touched) | set | materializes via the reader on first touch, caches into `_value`, keeps `_raw` |

No flag, no enum, no `pending` state — the field that's populated *is* the state.

**Why `_raw` must survive materialization.** Two payoffs ride on it:
- **Verbatim passthrough.** A courier (variable memory, callstack, channel routing) that never touches `.Value` serializes `_raw` straight out — no parse-then-reserialize. The OBP courier rule becomes physically true.
- **Signature verification on the raw.** The signature is over the bytes; verify `_raw` against it at the boundary, independent of (and before) any materialization. A re-serialized form might not match the signed canonical form; `_raw` is exactly what was signed.

So materialization is strictly a *read-through* — it never clears `_raw`. Only a mutation invalidates it. If anything clears `_raw` on a read, passthrough and signing break; that's the invariant to test hardest.

**Errors move from read-time to touch-time.** A malformed json file no longer errors at `read` — it errors at first touch (navigation / `As<T>`). That's the point of laziness and is acceptable, but it relocates where a developer sees the failure. Make the touch-time error name the source: `"failed to read %x% as json"`.

**`Wire.Read` capture mechanism.** STJ can hand you the raw json of the value slot (buffer the token / `GetRawText` *once*, store the string as `_raw`) without materializing it to a dict. That single capture replaces both the eager `Deserialize<object?>` and the `LiftDataIfShaped` sniff. The outer envelope (`{name, type, value, properties, signature}`) parsing is unchanged; only the value slot defers.
