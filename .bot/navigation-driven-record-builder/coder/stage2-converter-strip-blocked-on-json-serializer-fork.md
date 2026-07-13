# Architect Q — the Json.cs strip can't finish: `dict.Json` props up a second serialization path (the STJ `application/json` MIME serializer)

Branch: `navigation-driven-record-builder`. Raised by coder during the Stage-2 close-out.
The door deletion + test re-home (the `stage2-tryconvert-deletion-handoff.md` A + B) is **done, committed, green** (commit `2a519aa25`). This blocks only **C — the converter strip**.

## What the plan said
`stage2-tryconvert-json-sweep-answer.md` §"Demolition list" step 3 + the handoff §C:
> Strip the 13 `type/item/<name>/Json.cs` + their `[JsonConverter]` lines + the type entity's own converter. `dict/Json.cs` only after A's firing sites are gone (already unhooked). Goal: zero `Json.cs` under `type/item/` except `kind/json/`.

Premise: once `TryConvert`'s STJ arms + `dict.Clr`'s STJ fallback die, **nothing fires these converters**, so they're dead.

## What I found
- **12 of 13 are genuinely dead** — `archive, binary, bool, date, datetime, duration, guid, list, null, number, text, time`: zero external references (only their own `[JsonConverter]` attribute). They strip clean; build stays green. (Verified: stripped all 13, build failed on exactly one symbol.)
- **`dict.Json` is load-bearing** — one live consumer the demolition list missed:
  ```
  PLang/app/channel/serializer/Json.cs:57   new global::app.type.item.dict.Json()
  ```
  and its own committed doc (`dict/this.cs:17-23`) names four more live paths it governs:
  *snapshot-clone round-trip, debug-variable display, the `application/json` channel, `set … type=json` SerializeToNode.*

## The actual fork (this is the real issue, bigger than a converter)
`dict.Json` isn't a stray converter — it's holding up a **whole second serialization mechanism**: the STJ-based `application/json` MIME serializer.

```
app.channel.serializer.Json   (sealed ISerializer, raw STJ)        ← the DEFAULT serializer
   registered by serializer/list/this.cs for:  application/json, text/json, text/html   (_default = json)
   its Converters set needs new dict.Json() so a native dict projects as {} not its C# surface (cycles)

vs.

app.channel.serializer.plang.@this  (ISerializer, the WIRE)  →  data.Normalize → item.Output (IWriter)
   registered for:  application/plang, application/plang+json
```

So the **same value has two serialization implementations** depending on the channel MIME:
- `application/json` → raw STJ + `[JsonConverter]` attributes (`dict.Json`, and formerly the 12 scalar ones).
- `application/plang` → the IWriter / `item.Output` / `data.Normalize` path.

Stripping the 13 converters does **not** remove the STJ path (the path is the *serializer*, `channel.serializer.Json`, not the converters). It only removes the 12 dead scalar hooks and would **break** `dict.Json`'s live consumers. So "strip the converters" was never sufficient for one-path; the premise (converters gone ⇒ STJ path gone) doesn't hold.

## The question for you
Ingi's directive: **if the plan says kill it, kill it — the open question is *how*.** To let `dict.Json` + the type-entity converter die and reach one path, the `application/json` MIME serializer has to stop being raw-STJ and ride the **same IWriter/`item.Output` path** the wire uses. Concretely:

1. **Is that the intent** — `application/json` becomes a *view/syntax* of the one writer (JSON braces + `[Out]` filtering, **no** signing envelope / no `@schema` wrapper), rather than a separate STJ serializer? Or is a distinct STJ `application/json` serializer a *legitimate* second path we keep (in which case `dict.Json` stays and the plan line should be amended)?
2. **If collapse:** how should the four non-channel consumers re-home —
   - **debug-variable display** (currently STJ of the value) → `item.Output` in a `Debug`/Store view?
   - **snapshot-clone round-trip** (`snapshot` deep-clone via STJ) → the plang wire round-trip?
   - **`set … type=json` SerializeToNode** → the json *kind*'s own `Parse`/`Output`?
   - **`serializer/list` default** — `_default = json`; if the STJ `Json` serializer dies, what's the default MIME serializer?
3. **`serializer/plang` references the STJ `Json` base** in its doc comment ("lives on the base `app.channel.serializer.Json`") — is `plang` actually built on `Json`, or fully independent? (They're separate `ISerializer` classes; need confirmation the wire doesn't inherit STJ behavior.)

## Blast radius (grep-verified, HEAD `2a519aa25`)
- `channel/serializer/Json.cs` — the STJ serializer itself (sealed ISerializer; the `_default`).
- `channel/serializer/list/this.cs` — registers it as `application/json`, `text/json`, `text/html`, and `_default`.
- `channel/serializer/plang/this.cs` — comment ties Sensitive-stripping to the `Json` base.
- `channel/serializer/filter/Sensitive.cs`, `View.cs` — reference `serializer.Json` as the externalizing serializer.
- `dict/Json.cs` (+ `dict/this.cs` `[JsonConverter]`) — the projection.
- Consumers: snapshot clone, debug display, `set type=json`.

## Recommendation (coder)
Do **not** half-strip. I reverted C entirely — the tree is clean at `2a519aa25` (all 13 converters intact). The 12 dead scalar converters can be removed in a trivial follow-up **once** the `application/json`-serializer decision is made, so the strip lands as one coherent change with the path collapse (or is dropped if you rule the STJ `application/json` serializer a keeper). Awaiting your ruling on §Q.
