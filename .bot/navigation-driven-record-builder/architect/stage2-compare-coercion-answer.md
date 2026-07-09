# Decision — comparison coercion IS construction: `Create` gets a pure overload

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage2-convert-has-nonconstruction-consumers.md`. Good stop-and-surface — the consumer is real, and (C) alone would have changed language semantics.

## The ruling — (A), shaped as an overload of the same verb

"bool, make yourself from `text("true")`, or decline" is literally the `Create` contract — comparison coercion is construction with no courier. The friction was only ever the `Data` parameter. So:

```csharp
// THE PURE CORE — the relocated Convert body (parse/coerce). Null = decline.
// No Data, no error side-channel, no kind ceremony.
public static @this? Create(item.@this value)

// THE ICREATE MEMBER — the courier face: reads kind/strict off data.Type,
// data.Fail(reason) on decline, then delegates to the core.
public static @this? Create(item.@this value, data.@this data)
```

- **Compare coerces via the pure overload:** `b as @this ?? @this.Create(b)`. No synthetic Data, no `data.Fail` state dragged into comparison; a null coercion just means "not coercible" and the compare proceeds by rank — semantics unchanged (`%x% == "true"` keeps working).
- **Not a middleman:** the Data overload does real work (kind/strict, failure channel) before delegating — layering, not proxying.
- **No new name:** same verb, two arities — the `Get(string)`/`Get(path)` shape.
- **The shared core is sanctioned** by the extraction rule: two genuine callers (construction + comparison).
- **(C) rejected as a full answer:** born-native reduces raw operands but literals legitimately arrive as the other type (`== "true"`, `== 404`); dropping coercion changes what plang programs mean.

## Fallout

- The per-type `CoerceOwn` helpers (bool/date/datetime/guid/duration/time) **inline away** — each had one caller (its own `Compare`), and the body becomes the one-liner above.
- Stage 2's relocation is now mechanical, per type: today's `Convert` body → the pure `Create(item)` core; the ICreate `Create(item, data)` wraps it; `Compare` switches to the core; the hub (`OfStatic`/`Of`/`Invoke`/`Discover`) dies as planned.
- Kind-carrying construction (number's precision, image's format) lives only in the courier face (it reads `data.Type.Kind`) — the pure core takes the type's default construction. If a type's core genuinely needs a kind someday, that's the courier face's job; don't grow a kind param on the core.

## Acceptance

- `%x% == "true"` / `== 404` comparison tests stay green through the relocation.
- Grep zero on `CoerceOwn` and `convert.OfStatic` when Stage 2 closes.
