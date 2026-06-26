# Stage 3: Values born through the context

**Goal:** Make every value type carry a non-null `Context`, born from the context already in hand rather than constructed-then-stamped. This removes the `!` lie in `Data.Context` — the load-bearing source of the whole branch's class of bugs.
**Scope:** Mechanism B. Included: the seven value types (`type`, `dict`, `list`, `path`, `clr`, `computed`, `source`) + `Error`, the `Data.Context` getter/setter, the `context.Null/Ok/Error` factories, sentinels, the two reflection births. Excluded: serializers (Stage 4) — though they consume these births, the serializer wiring is Stage 4.
**Deliverables:**
- `actor/context/this.cs` — new factory methods: `context.Null()`, `context.Ok(value)`, `context.Error(...)` (mint a value/Data carrying this context).
- `data/this.cs` — `Data.Context` getter becomes `get => _context;` (drop `?? (_type as IContext)?.Context!`); setter drops the `value != null` null-guard (`:116,120`). `Data.Null()` (`:534`) sources from a context (`context.Null()`), not the `@null.Instance` static.
- The seven value types' `Context` field/property → non-null (`type/this.cs:86`, `dict/this.cs:109`, `list/this.cs:141`, `path/this.cs:120`, `clr/this.cs:16`, `item/computed.cs:20`, `item/source.cs:22`), plus `error/Error.cs:107`.
- Reflection stamps: `type/this.cs:391` (choice from enum) stamps `.Context = context` (context is already in scope at `:383,395`); `data/Wire.cs:265` (`WrapAsTyped` → `Data<T>`) receives `_context` from its single caller (`:193`) and stamps.
- Drop the `?` / `= null` on context params in these files as births become non-null.
**Dependencies:** Stage 1 (a non-null context exists to mint from).

## Design

The factory model is the fix: a value is born *from* the context, so there is no construct-then-stamp window. Every real birth already has a context in hand — handler result, `%var%` resolve, deserialize, LLM parse — they just need to route through the factory or stamp the in-scope context. No System-context floor: if a birth needs context, the birth knows its context.

Sentinels (`Data.Null`) can no longer be `static readonly` born before the App — they mint from the context at the call site. The two reflection births are the only `Activator.CreateInstance` sites that build a plang value, and both already hold a context; stamping after `CreateInstance` is enough (the field type is what matters — `Context`, not `Context?`). The other `Activator` sites build CLR intermediates / converters, not values — leave them.

`path` is the case that proves the value must carry context: `path.Authorize(verb)` needs context even outside a Data wrapper.

Full detail: `plan/value-births.md`.

## You own this

The factory method set and names (`context.Null/Ok/Error` are the intent), and how each value-creation site reaches its context, are yours. The contract: the seven value types + `Error` have non-null `Context`, the `!` is gone from `Data.Context`, and no value is observable without a context.
