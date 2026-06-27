# Read-path unification — summary

Collapse the scattered read onto a format-agnostic, registry-driven path that defers all value parsing. Incorporates the coder's v1 corrections (tuple error, delegate-don't-hold, keep both `IsFinal`+`Cacheable`).

**Law:** parse the `.pr` to learn the type; never load or parse a value (or a property's value) until `.Value()`.

**Leg A — load → lazy `Data`:** `read(IReader, View)` (mirror of `value.Write(IWriter)`; `json` is one `IReader`, owns the buffer). `@schema` dispatches through the registry (`App.Reader(schema)` — `signature`/`data` registered readers, no `if signature`; `signature` is the outer wrapper that verifies with the injected `View`+context, then recurses to `data`). The `data` reader captures the `value` **and every property value** via `IReader.RawValue()` (raw, no DOM) → `new source(value, type)` → holder `Data`. Nothing parsed.

**Leg B — first `.Value()`:** `Data.Value` is `answer = await item.Value(this); if (answer != item && item.Cacheable) item = answer; return answer`. `source.Value => (it,err) = app.type.Create(this)` over a **total** registry (specific reader ‖ one generic default reader that **delegates** to the type's `Convert` hook). `source` caches its parse (`Cacheable=true`, source→real type, replaced); templates/`computed` (`Cacheable=false`) re-render every read; `path` stays a path (content at output). A bad parse returns `(null, Error)` — authored in `Create`, set on `Data` by `source`, **no throw**.

**Two axes kept distinct:** `Cacheable` (keep-parse-never-render) drives the narrow; `IsFinal` (`=Template==null`) drives `dict`/`list` inner re-render. Not merged.

**Branches left:** one value-path narrow (**F2**, `Cacheable`). F1 (envelope) and F3 (reader) became registry dispatch; the STJ entry, value DOM, and `Build`/`Judge` fork are gone.

6 phases; value-ctor full retirement is **last** (scope decided when we reach it). Full leaf-trace, demolition worklist, reader-coverage, settled questions, OBP table in `plan.md`.
