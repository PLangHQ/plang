# Stage 2 — `read(IReader, View)` + `@schema` dispatch + raw value capture

**Design authority:** `plan.md` "Phase 2" + Leg A. Stub — **firm up when Stage 1 is green + pushed** (entry/exit may shift with what Stage 1 actually landed). Re-verify line numbers then.

## Entry
- Stage 1 green + pushed (total reader registry).

## Exit
- The read entry is `async read(IReader, View) → Task<Data>` (not `JsonSerializer.Deserialize<Data>`); `json.Reader` is one `IReader`.
- `@schema` dispatched via `App.Reader(schema)` (`data`/`signature` registered readers, no `if signature`).
- The `data` reader captures `value` **and every property value** via `IReader.RawValue()` — no DOM. The `signature` reader awaits verify (View-gated).
- Thin `JsonConverter<Data>` STJ adapter bridges sync at the perimeter. Build + both suites green.

## Dies / Stays
- See `plan.md` Phase 2 — populate + re-verify line numbers when this stage starts.

## Shipped + deltas from plan
_(coder fills.)_
