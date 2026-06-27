# Stage 3 — thin `source` + the `Cacheable` narrow

**Design authority:** `plan.md` "Phase 3" + Leg B. Stub — **firm up when Stage 2 is green + pushed.** Re-verify line numbers then.

## Entry
- Stage 2 green + pushed.

## Exit
- `source.Value(data)` = `(item,err)=await app.type.Create(this); if err {data.Fail(err);return Absent} else return item` — no try/catch, no throw. `source._raw` → `_value`.
- `Data.Value` rebind keyed on `Cacheable`, field `_type` → `item`. `IsFinal` unchanged (`=Template==null`), still driving `dict`/`list` inner re-render.
- `%ref%` full-match → variable judgement lives in the `text`/`variable` reader (`ReadContext.Template`). Build + both suites green.

## Dies / Stays
- See `plan.md` Phase 3 — populate + re-verify line numbers when this stage starts. (Note: `Cacheable` and `IsFinal` both STAY.)

## Shipped + deltas from plan
_(coder fills.)_
