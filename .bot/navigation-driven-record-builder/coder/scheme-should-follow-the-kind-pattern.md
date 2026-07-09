# For architect — path schemes should follow the kind pattern (a `From`-factory smell survived the redesign)

**From:** coder. **2026-07-09.** Ingi spotted it while I was relocating `path.Create` in Stage 2.
Logging it as its own piece per his call — NOT folded into Stage 2's `Convert→Create` sweep.

## The smell

`path` constructs a concrete scheme through a **registry + factory**:

```
app.Type.Scheme                     ← a registry (selection AND construction)
   Scheme.From(raw, context)        ← a factory that builds the concrete scheme (file://, http://, …)
   Scheme.Register(scheme, factory) ← external registration
```

That's the exact shape the **kind redesign already killed**: a static-ish factory doing the
collection's job (`kind.Of` / the string implicit), a registry that *constructs* instead of just
*selecting*. The kind redesign replaced it with:

- each kind a subclass at `type/item/kind/<k>/this.cs` (`json`, `list`, `dict`, `reflection`),
- one selection door `App.Type.Kind[name|clrType]` — selection + lifecycle only, born-with-context,
  never null,
- the kind IS the behavior (no `Of`/`From` factory, no token/behavior split).

Schemes are the same concept one folder over and never got the treatment.

## The proposed shape (mirror the kind ruling)

- **A subfolder per scheme** under `path/` — `path/scheme/<name>/this.cs` (`file`, `http`, …), each a
  `path.@this` subclass owning its own construction/authorization. (Some already exist as scheme
  subclasses — `FilePath`/`HttpPath`; the move is making the *selection* a collection, not a factory.)
- **One selection door** `App.Type.Path.Scheme[name]` (or `path.scheme.list`) — selection + lifecycle,
  born-with-context, plugin schemes register into the collection the way `code.load` types do; no
  `From`, no `Register` free function.
- The scheme parsed off the raw string (`file://…`, `http://…`, bare) picks the subclass via the door;
  the subclass constructs itself. "the scheme IS the behavior," same sentence as the kind.

## Why it's separate from Stage 2 (and doesn't block it)

`path.Create` (Stage 2) only needs "build a path from a string" — it **delegates** to whatever the
scheme construction is. Today that's `Scheme.From(raw, ctx)`; after this redesign it's the door. So:

- Stage 2 `path.Create` stays on `Scheme.From` for now (green), one delegating line.
- When schemes move to the kind pattern, that one line updates. No coupling.

Ingi's call: **(A)** — I keep `path.Create` delegating to the existing `Scheme.From`, continue the
Stage-2 sweep, and you own the scheme→kind-pattern redesign as its own branch/stage (it's a pattern
change on the scale of the kind redesign, not a Stage-2 rider). Flagging scope: it touches path's
scheme registry, `Scheme.From`/`Register`, the scheme subclasses, and any `App.Type.Scheme` callers.
