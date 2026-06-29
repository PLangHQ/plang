# Stage 3 — the ctor + Declare flip (the core change)

**Goal:** rewrite the Data constructor's typed-construction block and `Declare` to the four-case fork. The `if _context != null … Build : Judge` branch dies; the context fork is gone.

**Kind:** flip. This is the heart. Both targets (from-raw `source`, case-2b `Convert(item)`) exist and are green from Stages 1–2, so this is a re-route.

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

`json.Parse(value)` first (it natives-out `JsonElement`/`JsonNode`; leaves a plain string untouched — `json.cs:131`). Then:

```
1  value == null (→ _item is null citizen)        → new @null.@this(type.Name, type.Kind)        [KEEP :195-199]
2  parsed value is an already-built item.@this:
   2a  its type == declared (incl. facet-match,
       and same-type-missing-kind → re-kind)       → hold as-is (re-kind if needed)               [Stage 2 sub-rules]
   2b  its type != declared                         → type.Convert(builtItem)  (the surviving op)
3  parsed value is still a raw form (string/byte[]) → mint source(value, type, format-by-type), defer
```

**Format-by-type (case 3) — the judgement the coder must nail:**
- scalar type (number/date/bool/guid/duration/text/path/time/…) → `format = "text/plain"` (the scalar `value.Reader`).
- container type (dict/list/object/item) → `format = "application/plang"` (the json reader, `BeginObject`).
- `byte[]` raw → `format = App.Format.Mime("." + kind)` (kind→mime, as the binary-family readers do) — **never `text/plain`**.

Pick the format from the declared type's reader mode. A clean predicate (sketch — coder owns the final form):

```csharp
string FormatFor(type t, object raw) =>
    raw is byte[]            ? t.Context.App.Format.Mime("." + (t.Kind ?? "")) // kind→mime
  : t.IsContainer()                                                            // dict/list/object/item
                            ? global::app.channel.serializer.plang.@this.Mime  // application/plang
  :                           global::app.channel.serializer.Text.Mime;        // text/plain
```

Verify the `IsContainer()` discriminant against how the type entity already classifies itself (do not invent a new flag if one exists — check `type.@this` for a kind/family predicate first; OBP smell #1/#5 if you add a parallel one).

**Wiring the from-raw arm:** mint the `source` and hand it to the **born-typed holder ctor** path (`data/this.cs:222`) — i.e. `_item = new source(value, type.Name, type.Kind, format: FormatFor(...), ...)`. The `source` is itself an `item.@this`, so `_item` holds it directly; first `.Value()` parses it once. The eager `Create(Parse(value))` at `:184` must NOT run for case 3 (that is the throwaway-`text` it deletes). Restructure so `:184` runs only for the polymorphic / no-declared-type path.

**Crucial:** case 3 mints a `source`; it never calls `type.Convert`. Case 2b calls `type.Convert(item)`; it never mints a `source`. Keep them disjoint — that disjointness IS invariant #2 (one conversion).

---

## Declare (`data/this.cs:241-253`)

Same logic, on an already-constructed Data whose `_item` is a **built value** (its callers `builder/code/Default.cs:927,943` hand it a built `text`). So Declare is almost entirely a **case-2/2a/2b** site — there is no raw form here:

```csharp
internal void Declare(type declared)
{
    if (declared is not { IsNull: false } || declared.Polymorphic) return;
    declared.Context ??= _context;
    // _item is a built value: hold if already the declared type (2a, re-kind if needed),
    // else convert (2b). No source — a built value has no clean raw to defer.
    _item = SameType(_item, declared) ? ReKindIfNeeded(_item, declared)
                                      : declared.Convert(_item);
}
```

**Keep Declare's input a built value, NOT a `source`.** The `%var%`-skip guard at `builder/code/Default.cs:934-935` reads `p.Peek() as text.@this` + `StartsWith("%")`. If `Declare`'s input became a `source`, `Peek()` returns a raw string, the `as text.@this` cast nulls, and the guard mis-fires — a `%var%` reference would get a kind stamped. Do not change `p`'s construction to lazy at these call sites.

---

## Edge checks before declaring done

- **JSON null literal:** `value` is a `JsonElement` of kind Null → `Parse` returns the null citizen → it is a *built* `null.@this`, so it lands in case 2 (built), not case 1 (`value == null`). Confirm 2a holds it as a typed null (or that the behavior matches today's `Build(null.@this)`). Add a test.
- **Empty string `"" as number`:** `Parse("")` returns `""` (string) → case 3 raw scalar → `source` → `number.Convert("")` fails at first use → `MaterializeFailed`. Confirm (deferred-failure semantics, accepted).
- **Already-native matching container (`{a:1} as dict`):** case 2a hold. Confirm no re-parse.
- **`set`'s match short-circuit still wins first:** `set.cs:240-246` returns before the ctor for raw-untouched same-type — so the ctor never sees that case. Good; don't duplicate it.

---

## Exit criteria

- [ ] `data/this.cs` ctor rewritten to the four-case fork; `if _context != null Build : Judge` gone; `:184` eager lift no longer runs for declared-type raw forms.
- [ ] `Declare` rewritten to 2a/2b (no `Build`/`Judge`, no `source`).
- [ ] Tests: `"5" as number` → number, ONE conversion, no throwaway text (assert via a materialization counter or by tracing the reader is hit once); `'{"a":1}' as dict` → dict; `{a:1} as dict` → held; `text "5"` re-typed to number via 2b; typed-null preserved; JSON-null + empty-string edges.
- [ ] `Build`/`Judge`/`Deserialize` still exist (deleted in Stage 5) — but the ctor + `Declare` no longer call `Build`/`Judge`. Grep proves it.
- [ ] Global exit gates green.

## What must NOT happen

- No `source` minted for a built value (case 2b converts; it cannot re-source — built `text` has no clean raw).
- No `type.Convert` called for a raw form (case 3 defers via `source`).
- No Variable/`%ref%` special-case in the ctor (Stage 2 put them in `source`).
- No deletion of `Build`/`Judge` yet (Stage 5, after `set`/`validateResponse` reroute in Stage 4).
- Don't add a parallel `IsContainer` flag if the type entity already exposes the discriminant.
