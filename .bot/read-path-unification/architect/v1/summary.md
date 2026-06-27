# Read-path unification — summary

Collapse the scattered read onto a format-agnostic, registry-driven path that defers all value parsing.

**Law:** parse the `.pr` to learn the type; never load or parse a value until `.Value()`.

**Leg A — load → lazy `Data`:** `read(IReader)` (mirror of `value.Write(IWriter)`; `json` is one `IReader`, owns the buffer). `@schema` dispatches through the reader registry (`App.Reader(schema)` — `signature`/`data` are registered readers, no `if signature`; `signature` is the outer wrapper that recurses to `data`). The `data` reader pulls `name`/`type`/`properties` via `IReader.Field` and captures `value` via `IReader.Raw` (raw slice, **no DOM**) → `new source(value, type)` → holder `Data`. Nothing parsed.

**Leg B — first `.Value()`:** `Data.Value` is `result = await item.Value(this); if (!item.IsFinal) item = result; return result`. `source` is the only non-final placeholder — `source.Value => app.type.Create(this) => App.Type.Reader(source).Read(source)` (total registry: a specific reader ‖ one generic default reader holding the old `Convert` logic; never null). It parses once, `Data` swaps `source → real type`. `path`/`file`/`dict`/`text`/templates are final: they render/resolve in place and stay. A bad parse **throws** to the boundary seam (`Navigate`/typed-ask) — no try/catch in `source`/`Data`.

**Branches left:** only **F2** (the `!IsFinal` source swap). F1 (envelope) and F3 (reader) both became registry dispatch; `Cacheable`, `Convert` (name), the STJ entry, and the value DOM are gone. All discrimination rides registries (`@schema`, `(type, kind)`) + virtual `Value`.

6 phases, full demolition worklist + OBP table in `plan.md`.
