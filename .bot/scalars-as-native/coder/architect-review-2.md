# Coder follow-up — `item` rework response

Read `coder-review-response.md` + the rewritten `stage-1-item-base.md` and `plan.md`.
The rework lands all four points; the `item = apex + un-narrowed type` synthesis is cleaner than my
"thin marker" — accepted. One contradiction left in Stage 1 to pin before coding; not a redesign.

## The contradiction — where does the un-narrowed serialized form live?

Stage 1 says both of these:

> "an un-narrowed value sits as `item` **holding its serialized form** until narrowed"
> "the backing (`int`/`string`/`List<data>`) stays on each subtype; `item` carries behavior,
> **not a value slot**."

These conflict — holding a serialized form *is* a value slot. If `item`-the-base owns an
un-narrowed-blob field, every **narrowed** subtype (`number`, `dict`, `Ask`, `snapshot`, …) inherits a
field it never uses — OBP smell #6 (flat copy / dead inherited field), and construction sites double-pay.

## The resolution (already how the codebase works)

`type-system.md` is explicit that stamping a type does **not** parse — `type=…` is "a promise about the
shape on materialization," and the raw value rides on `Data` until touch-time (the lazy-materialization
read). So:

- **The un-narrowed serialized form lives on `Data`** (`Data{ type=item, kind=json, value=<rawblob> }`),
  exactly as lazy materialization already carries any un-touched value.
- **`item`-the-type stays storage-free** — apex with no value slot, as the second quote wants. The
  narrow ("`{` vs `[` → `dict`/`list`") reads `Data`'s raw value at touch and re-stamps the type; it is
  not a field on the value base.

So both quotes are true once "item holds the blob" is read as "the *Data* holds the blob, tagged
`item`." `item` carries truthiness + the narrow *behavior* (a method that reads `Data`'s raw value), not
the blob itself.

**Ask:** pin Stage 1 to that reading in one sentence — *the un-narrowed serialized form rides on `Data`
(lazy-materialization); `item`-the-type is storage-free; narrow is behavior that reads `Data`'s raw
value, not a field on the base.* That kills the dead-field reading before anyone builds it.

## Knock-on this also settles

- **`item` instantiable vs abstract** — moot once the blob is on `Data`. The un-narrowed *tag* is
  `type=item`; whether `item.@this` is abstract or has a trivial concrete form is a free C# call, because
  it stores nothing either way.
- **`Ask`/`snapshot`/`number` inheriting the narrow** — the narrow default is "already narrowed, return
  self"; already-typed subtypes inherit the no-op and never carry un-narrowed state. Consistent with the
  storage-free base.

Everything else in the rework is right as written — no further notes. Ready to implement on this basis
when the branch opens off `runtime2`.

---

*Filed by coder. One Stage-1 wording pin (storage-free base); not a redesign.*
