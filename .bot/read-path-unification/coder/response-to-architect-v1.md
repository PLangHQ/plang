# Read-path unification — coder response to architect v1

**Branch:** `read-path-unification`. Re: `../architect/v1/plan.md`.
Reviewed with Ingi. Three plan items change, three need your answer, one confirmed.

The architecture is right (defer-to-`.Value()`, registry-collapse of F1/F3, the
`IReader.RawValue()` find that kills the DOM). The notes below are corrections
and open questions, not a rewrite.

---

## Settled with Ingi — three corrections to the plan

### C1 — A bad parse does NOT throw. `app.type.Create` returns a tuple; the error is set on `Data`.

The plan's "a bad parse throws to the boundary seam (`Navigate`/typed-ask)" is
reverted. Reasons: a malformed value is **bad data, not an invariant violation**
(the same read path serves http-inbound, not just `.pr`), and PLang's error
model is `on error` over `Data.Error` — a throw to a distant seam loses the
binding name, `{type,kind}`, and JSON path+line that only exist at the failure
point.

**Shape Ingi wants:** the failure originates *inside* `app.type.Create` (the
reader/parse), so author the error **there** and return a tuple — do not author
it up in a `source` try/catch around a throw:

```
app.type.Create(source) -> (item?, Error?)        // the door owns its own failure story
source.Value(data):
    (item, err) = app.type.Create(this)
    if (err != null) { data.Fail(err); return Absent; }   // error set on Data at the source seam
    return item
```

This keeps everything good about today's `source.cs:123-126` (keyed
`MaterializeFailed`, named binding, JSON path+line, `Exception` attached) but
moves the authoring to where the failure actually is (`Create`), and drops the
exception-catch in favor of a returned tuple.

**Consequence for your plan:** the "enumerate every `Data.Value()` call site to
prove a catching seam" worry is **void** — there is no throw, so no enumeration.
The error rides on `Data` uniformly, exactly as today. Delete the throw model
from the leaf-trace and OBP table; the `parse error` row becomes
"`app.type.Create` returns `(item, Error?)`; `source` sets it on `Data`."

### C2 — Conversion stays ON the type. The generic reader DELEGATES, it does not HOLD.

Keep the plan's goal: **one** generic default reader, no per-scalar reader file.
The single wording to fix is *"the generic reader's `Read` holds the old Convert
logic."* It must **delegate** to the type's own coercion hook, not absorb it.

The per-type coercion already lives correctly distributed on each type
(`date/this.Convert.cs`, `dict/this.Convert.cs`, `list/this.Convert.cs`,
`image/this.Convert.cs`, …). `catalog/Conversion.cs` is a **router only** — it
says so itself (`:14`): *"Per-type construction knowledge lives on the owning
types (their Convert hooks); this file holds no per-type arms."* If the generic
reader holds a central type-switch, it pulls behavior off the types and rebuilds
the `Of` delegate registry we are deleting.

```
generic reader.Read(source) ──delegates──▶ type.@this.Convert(raw)   // this.cs:573, dispatches to the per-type hook
```

- One reader class, no per-scalar file ✓
- Coercion stays a virtual hook on each type ✓ (OBP)
- Net change vs today: `source.Value` routes through the reader instead of
  calling `entity.Convert(s)` directly; the reader calls it.

**Do not delete the per-type `Convert` hooks or the catalog router** — they are
load-bearing and correctly placed. The `Convert` *name* at the per-type hook may
stay; there is no `type.Read`/`Convert` *door* exposed, because the door is
`app.type.Create(source)` → reader → type hook. That satisfies "no `Convert`
method as a door."

### C3 — Drop the `IsFinal`/`Cacheable` merge. They are two orthogonal axes, both in use.

The plan deletes `Cacheable`, re-points `IsFinal` to "real value (not `source`),"
and drives the narrow off `!IsFinal`. This **merges two axes that must stay
separate** and silently breaks two consumers.

| | `IsFinal` (`item:247` = `Template==null`) | `Cacheable` (`item:127` = `true`) |
|---|---|---|
| Means | "my door returns myself — no render/load" | "Data may KEEP my answer (parse yes, **render never**)" |
| Read by | **dict `:388`**, **list `:570`** — `!e.IsFinal` → re-render inner element | **Data.Value `:272`** — the narrow |
| Template | template → not final (re-renders) | text/dict/list → `Template==null` (template NOT cached) |

Two concrete regressions from the re-point:

1. **Template interpolation inside collections dies.** dict/list re-render an
   inner element only when `!e.IsFinal`. Re-point so a template is `IsFinal=true`
   → dict/list stop re-rendering → `%x%` inside `{a:%x%}` or `[%x%]` never
   interpolates.
2. **Templated path/text stop re-resolving.** `path.Cacheable => _location.Cacheable`
   (false when location is `%file%.txt`). Delete `Cacheable`, cache on
   "is-not-source" → a templated path caches on first read and never re-resolves.

Root cause: **`source` is not the only placeholder.** A template is also
non-final and must *not* be cached — the "keep the parse, never the render"
distinction. One flag cannot encode both "swap `source`→`dict` and keep it" and
"re-render the template every read."

**Resolution:** keep both. `IsFinal` stays `Template == null` (drives dict/list
re-render). The narrow stays keyed on `Cacheable` (the correct decider — `source`
already inherits `Cacheable => true`, so it caches the parse with no change). The
only cleanup left is cosmetic — the rebind at `data/this.cs:272` is already one
line. Remove the IsFinal/Cacheable unification from the leaf-trace, the F2 row,
and the OBP table.

---

## Need your answer — three open scope/mechanism questions

### Q4 — value-ctor retirement: this branch or a follow-on?

Phase 4 says "retire the value-ctor entirely" and admits "the no-type lift sites
are the bulk of the work." That reaches **write and in-code paths** across the
whole codebase — nothing to do with read unification. Is a codebase-wide
`new Data(name, value)` migration in scope for *this* branch, or split to a
follow-on so read-path-unification can land? If in scope, give a call-site count
(we only have the ~7 typed + 2 `Declare` figures; the no-type bulk is uncounted).

### Q5 — the `signature` reader is not a pure decode. How does it fit `read(IReader)`?

`ReadSignatureLayer` (`Wire.cs:206-241`) runs the **verify action** with actor
context, **fails closed** on missing context, and branches **Store vs Out view**.
That is not a `raw → value` decode like CSV. Folding it behind the same
`App.Reader(schema)` interface ("same pattern as a value type") understates it —
value readers don't run actions, don't fail-closed, don't read `View`. Show how
verify's context + lifecycle live inside `read(IReader)` (where does the actor
come from, where does the View check sit, what happens to the fail-closed branch
when `_context` is now guaranteed non-null in Phase 5). It may be fine; it isn't
obviously symmetric.

### Q7 — `code.load` readers: preserve the runtime registration path.

`Register()` (`reader/this.cs:125`) feeds the **delegate** table (`_runtime`),
which Phase 1 deletes. DLLs loaded via `code.load` that ship a static `Read`
currently register there. After Phase 1, what is their path — register onto the
typed (`ITypeReader`) table, or route through the generic reader's type hook?
Spell out the surviving runtime-registration seam so `code.load`-shipped readers
keep working.

---

## Confirmed (Ingi)

### Q6 — properties are lazy too.

Properties' value slots must stay **raw** (recurse into `source`), same as the
top-level `value` slot — or invariant 1 ("no value parse at load") breaks one
level down. The `data` reader reads property *structure* but each property value
is captured raw and parsed only on its own `.Value()`. Add this to invariant 1.

---

## Net effect on the plan

- **Leaf-trace / fork ledger:** remove the throw model (C1), the `Cacheable`
  delete + `IsFinal` re-point (C3). F2 stays keyed on `Cacheable`, unchanged.
- **Generic reader (Phase 1):** word it as *delegates to* `type.Convert`, not
  *holds* its logic (C2). Keep the catalog router + per-type hooks.
- **`app.type.Create` signature:** returns `(item?, Error?)` (C1).
- **Phases 4/5 + signature:** gated on Q4/Q5/Q7 answers.
- **Invariant 1:** extend to properties (Q6).

Still net-positive: one lazy door, registry dispatch for the envelope, no DOM.
The corrections keep the error model, OBP placement, and template semantics that
the v1 unification would have regressed.

---

## Decided (Ingi) — `read(IReader)` is async

The v1 plan presents `read(IReader)` as sync (the mirror of sync
`value.Write(IWriter)`). But verify-on-read is async — today it's sync-over-async
(`Wire.cs:248`: `RunAction(...).GetAwaiter().GetResult()`), tolerated only
because it hides inside a sync `JsonConverter.Read`.

**Decision: the read is async — `read(IReader) -> Task<Data>`.** Verify is async,
so the read is. This breaks the sync-`Write` mirror, and that's correct: **reads
do I/O** (signature verify now, `path`/`url` content later), **writes don't**.
The symmetry is "read pulls / write pushes," not "both sync." Drop the
`.GetAwaiter().GetResult()` — the signature reader `await`s verify; the whole
`read`/`App.Reader(schema).Read(r, v)` chain returns `Task`. The thin
`JsonConverter<Data>` STJ adapter (sync `Read` signature) is the one place that
bridges back to sync — it `.GetAwaiter().GetResult()`s the async `read` at the
STJ boundary, where there is no choice. That single bridge is the only
sync-over-async left, and it lives at the perimeter, not in the read core.
