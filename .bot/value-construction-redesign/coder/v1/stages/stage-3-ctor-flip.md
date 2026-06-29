# Stage 3 — the ctor + Declare flip (the core change)

**Goal:** move the typed-construction fork **off the Data ctor and onto `type.Build`** (OBP Rule #1 — behavior on the owner). The ctor delegates in one line; `type.Build` is reimplemented to fork internally (raw → `source`, built → hold/convert, null → typed-absence). The `if _context != null … Build : Judge` context fork dies; `Declare` delegates to the same `type.Build`.

**Kind:** flip. This is the heart. Both targets (the from-raw `source` path, the case-2b `Convert(item)` op) exist and are green from Stages 1–2, so this is a re-route — the fork itself lives on the type, reading `this`, not in the ctor.

**Depends on:** Stage 1 (every reachable type has a reader) + Stage 2 (source covers Variable/`%ref%`; `Convert(item)` exists; 2a sub-rules known).

---

## The current block (`data/this.cs:184` + `:193-212`)

```csharp
_item = Create(new json(_context).Parse(value), _context);   // :184 — ALWAYS lifts (the throwaway text)
...
if (type is { IsNull: false } && !type.Polymorphic)          // :193
{
    if (value == null && _item is @null.@this)               // :195 typed absence — KEEP
        _item = new @null.@this(type.Name, type.Kind);
    else if (_context != null) { type.Context ??= _context; _item = type.Build(_item); }   // :200 DELETE fork
    else _item = type.Judge(_item);                                                          // :211 DELETE fork
}
```

The problem: `:184` eagerly `Create(Parse(value))`s — minting the throwaway `text` — *before* the type block. The flip must stop the eager lift for the from-raw case.

---

## The target fork

**OBP — the fork lives on the `type`, NOT in the Data ctor.** Behavior belongs to the owner (OBP Rule #1): "make a value of this type from X" is the type's job, and the type already owns it via one call — `type.Build(_item)`. Do **not** spread the discriminants (container? scalar? bytes? same-type? re-kind?) into the Data ctor as free helpers — that decomposes the type (Rule #1 + #4) and is exactly the smell to avoid. The Data ctor stays dumb and **delegates**; the fork happens *inside* `type.Build`, where every discriminant reads the type's **own** state via `this`.

**The Data ctor (one delegation, no fork):**

```csharp
var parsed = new json(_context).Parse(value);     // natives-out JsonElement/JsonNode; plain string untouched
_item = type is { IsNull: false, Polymorphic: false }
      ? type.Build(parsed)                          // the TYPE forks internally (raw / built / null)
      : Create(parsed, _context);                   // polymorphic / no-declared-type — unchanged
```

The eager `Create(Parse(value))` at `:184` now runs **only** for the polymorphic/no-type path — the throwaway-`text` lift no longer happens for a declared type.

**`type.Build` reimplemented — no context fork, every branch reads `this`** (`type/this.cs:232`, keep the name per Ingi):

```csharp
public item.@this Build(object? value)
{
    if (value is null) return new @null.@this(Name, Kind);          // case 1 — typed absence

    if (value is item.@this built)                                  // case 2 — already a built value
        return Mint().Name == built.Mint().Name                     // 2a: same type → hold (re-kind/facet per Stage 2)
             ? built /* + re-kind if Kind!=null && built kindless; facet-match holds */
             : Convert(built);                                      // 2b: different type → the surviving convert op

    return new item.source(value, Name, Kind, format: RawFormat);   // case 3 — raw form → deferred source
}
```

- **`RawFormat` is a noun on `type.@this`** (not a free `FormatFor(...)`): the type names the format its own raw form carries —
  - container (dict/list/object/item) → `application/plang` (json reader, `BeginObject`),
  - `byte[]`-backed (image/binary family) → `App.Format.Mime("." + Kind)` (kind→mime) — **never `text/plain`**,
  - scalar → `text/plain` (the scalar `value.Reader`).
  Reuse the type's existing kind/family classification — do **not** add a parallel `IsContainer` flag (OBP smell #1/#5); check `type.@this` for the discriminant it already has.
- **2a/2b live inside `Build` reading `this`** — `Mint().Name`, `Kind`, `Facet(Name)` are all the type's own state. The same-type-missing-kind re-kind and the facet-match hold (the Stage 2 sub-rules, lifted from today's `Judge`) stay *inside* `Build`, not in free helpers.
- **Disjoint:** case 3 mints a `source`, never calls `Convert`; case 2b calls `Convert`, never mints a `source`. That disjointness IS invariant #2 (one conversion).

This makes `Build` **reimplemented** (raw branch mints a `source` instead of lift-to-text+convert), not "from-raw scaffolding carved out into the ctor." Stage 5 then thins `Build`'s *internals* (the throwaway-text + reflection die); `Build` survives as the one construction entry.

---

## Declare (`data/this.cs:241-253`)

Same owner, same call. `Declare` runs on an already-constructed Data whose `_item` is a **built value** (callers `builder/code/Default.cs:927,943` hand it a built `text`). So it delegates to the *same* `type.Build` — which routes a built value into case 2 (hold or convert):

```csharp
internal void Declare(type declared)
{
    if (declared is not { IsNull: false } || declared.Polymorphic) return;
    declared.Context ??= _context;
    _item = declared.Build(_item);   // built value → case 2 inside Build (2a hold / 2b convert). One door.
}
```

No free `SameType`/`ReKindIfNeeded` — that logic is inside `Build`. No `source` — a built value has no clean raw to defer, so case 2 converts (2b) or holds (2a).

**Keep Declare's input a built value, NOT a `source`.** The `%var%`-skip guard at `builder/code/Default.cs:934-935` reads `p.Peek() as text.@this` + `StartsWith("%")`. If `Declare`'s input became a `source`, `Peek()` returns a raw string, the `as text.@this` cast nulls, and the guard mis-fires — a `%var%` reference would get a kind stamped. Do not change `p`'s construction to lazy at these call sites.

---

## Edge checks before declaring done

- **JSON null literal:** `value` is a `JsonElement` of kind Null → `Parse` returns the null citizen → it is a *built* `null.@this`, so it lands in case 2 (built), not case 1 (`value == null`). Confirm 2a holds it as a typed null (or that the behavior matches today's `Build(null.@this)`). Add a test.
- **Empty string `"" as number`:** `Parse("")` returns `""` (string) → case 3 raw scalar → `source` → `number.Convert("")` fails at first use → `MaterializeFailed`. Confirm (deferred-failure semantics, accepted).
- **Already-native matching container (`{a:1} as dict`):** case 2a hold. Confirm no re-parse.
- **`set`'s match short-circuit still wins first:** `set.cs:240-246` returns before the ctor for raw-untouched same-type — so the ctor never sees that case. Good; don't duplicate it.

---

## Exit criteria

- [ ] Data ctor delegates to `type.Build` for the declared-type path (one line, no fork in the ctor); the `if _context != null Build : Judge` **context fork** gone; `:184` eager lift no longer runs for declared-type raw forms.
- [ ] `type.Build` reimplemented: case 1 typed-absence, case 2 built (2a hold/re-kind/facet, 2b `Convert`), case 3 raw → `source(…, RawFormat)`. No context fork. `RawFormat` is a noun on the type.
- [ ] `Declare` delegates to `type.Build` (built value → case 2); no free `SameType`/`ReKindIfNeeded`, no `source`.
- [ ] Tests: `"5" as number` → number, ONE conversion, no throwaway text (assert via a materialization counter or by tracing the reader is hit once); `'{"a":1}' as dict` → dict; `{a:1} as dict` → held; `text "5"` re-typed to number via 2b; typed-null preserved; JSON-null + empty-string edges.
- [ ] `Judge`/`Deserialize` still exist (deleted in Stage 5) but the ctor/`Declare` no longer call them. `Build` survives (reimplemented) — it IS the construction entry. Grep proves `Judge` has no live caller.
- [ ] Global exit gates green.

## What must NOT happen

- No fork in the Data ctor — the discriminants (container/scalar/bytes, same-type/re-kind) live inside `type.Build`, reading `this`. The ctor delegates.
- No free helpers (`FormatFor`, `SameType`, `ReKindIfNeeded`) — behavior on the owner (OBP Rule #1). `RawFormat` is a noun on the type.
- No `source` minted for a built value (case 2b converts; it cannot re-source — built `text` has no clean raw).
- No `Convert` called for a raw form (case 3 defers via `source`).
- No Variable/`%ref%` special-case in the ctor (Stage 2 put them in `source`).
- No deletion of `Judge` yet (Stage 5, after `set`/`validateResponse` reroute in Stage 4). `Build` is kept (reimplemented), not deleted.
- Don't add a parallel `IsContainer` flag if the type entity already exposes the discriminant.
