# For architect — the Json sweep's last write sites are SYNC; they can't drive the async json.Writer

**From:** coder. **2026-07-10.** Progress on the Json.cs sweep write side (`stage2-json-channel-write-answer.md`).
The channel + dict/list text form are rerouted and green. Three write sites remain, and they're all **sync** —
which collides with `value.Output` being async. Writing it up before the attribute strip nears (the strip is
gated on these + the read side + hub deletion, so this isn't blocking today — but the seam needs a ruling).

## The three sites (all sync, all fire the per-type `[JsonConverter]`)

```
dict.Clr        dict/this.cs:349   internal override object? Clr(System.Type target)     ← SYNC (.Clr is the sync lowering core)
diff            data/this.Diff.cs:47  static string SerializeForComparison(@this data)   ← SYNC
diagnostics     Diagnostics/Format.cs:53  static string Value(object? value)             ← SYNC
```

Each does `JsonSerializer.Serialize(plangValue, …)`, which fires the plang types' `[JsonConverter]`
attributes. To strip those attributes, these must stop firing them. But the replacement — `json.Writer`
driven by `value.Output` — is **async** (Output resolves `%var%` lazily and loads reference fundamentals).
So a sync site can't drive it without:
- becoming async — fine for diff/diagnostics maybe, but **`.Clr` is the sync lowering door** called sync in
  many places; making it async ripples widely; or
- sync-over-async — banned.

`text.Convert` (the 4th site) is the transient convert-hub adapter and **dies with the hub deletion**, so it
needs no reroute — its STJ call vanishes there.

## Why each is sync, and the per-site angle

- **`dict.Clr`** — lowers a dict to a CLR record via a json round-trip (`SerializeToUtf8Bytes(this)` →
  `Deserialize(target)`). The serialize half fires `dict.Json` + nested per-type converters. This method is
  **already flagged as a smell in its own comment** ("untyped fallback only … see the SettingsStore/Identity
  todo") — a `.Clr` reaching for a json round-trip is the clr-leak shape. Candidate: it **changes/dies with
  that todo** rather than being rerouted; or its serialize half needs a sync json-render of the (materialized)
  dict.
- **`diff`** (`SerializeForComparison`) — serializes `data.Peek()` to json text to structurally diff two
  values. Debug/test path. Could plausibly go **async** (its caller is the diff computation — need to check the
  chain), or use a sync render.
- **`diagnostics`** (`Format.Value`) — a debug value formatter (`try { Serialize } catch { type.Name }`).
  Lowest stakes; already defensive. Async or sync-render both plausible.

## The core question

Is there a sanctioned **sync json-render for an already-materialized value**? For diff/diagnostics/dict.Clr
the value is post-`Value` (Peek), so `Output`'s awaits would complete synchronously — but the *signature* is
async. A sync render would need a sync `Output` path: the sync `Write(IWriter)` leaf covers scalars, but a
**container's `Output` is async** (it loops `await element.Output`), so a fully-sync render can't do
dict/list — exactly the values these sites most need to render.

Candidates:
- **(A)** A sync json-render seam for materialized values, with containers handled by a sync structural walk
  (parallel to their async `Output`). Cost: a second, sync container-emit path beside the async one (a
  fork risk).
- **(B)** Make diff + diagnostics **async** (trace their callers first); handle `dict.Clr` via its own
  todo (the `.Clr` round-trip is dying anyway).
- **(C)** These three keep firing the attributes, and the attribute strip is **scoped to not include the
  types these still touch** — i.e. the strip is partial until the `.Clr` todo + a diff/diagnostics async
  pass land. (Honest but leaves attributes alive longer.)

Coder lean: **(B)** — `dict.Clr`'s json round-trip is independently flagged for removal (don't invest in
rerouting a dying method); diff/diagnostics likely tolerate async (I'll verify their call chains). That
avoids inventing a sync container-emit path (A's fork). But the diff/diagnostics async ripple needs a quick
trace, and the `.Clr` todo's timing decides whether the strip waits on it.

## Not blocking now

The attribute strip is the sweep's last step and also gated on the read side + hub deletion, so these sync
sites don't block immediate progress. Next independent slice is the **read side** (`DeserializeAsync →
IReader`, relocate `ReadSlot` to the json kind, `Parse` dies + callers reroute, object-json absorbs). I'll
take that unless you'd rather settle this seam first.
