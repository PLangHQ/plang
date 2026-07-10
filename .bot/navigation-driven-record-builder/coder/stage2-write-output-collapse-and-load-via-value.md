# For architect — collapse `Write`/`Output`, kill `Load()` via `.Value()` — and the raw-passthrough constraint

**From:** coder. **Direction set with Ingi (2026-07-10).** Came out of the json-channel-write reroute (B,
landed `d6372446f`). Ingi wants the serializer's `Load()` pre-pass gone — the value should materialize
itself at the leaf, in the single async `Output` pass, now that `Output` is async. Traced it; there are
real subtleties (a widely-used sync `Write` primitive, an image/directory asymmetry, Store-view rendering,
and a **raw-passthrough** case Ingi flagged that must survive). Writing it up so the shape is ruled before
a ~21-file change.

## The current shape (traced, not assumed)

Two-tier value write:
- **21 leaves** override sync `void Write(IWriter)` — mode-free (a `guid` is `guid` in every view). The
  base `item.Output(writer, mode, context)` is a 3-line wrapper: `Write(writer); return CompletedTask;`.
- **14 containers/complex** (list, dict, type, reflection, signature…) override async
  `ValueTask Output(IWriter, mode, context)` directly and *do* use mode.

So the base `Output`-wrapping-`Write` is a **middleman** for the 21 leaves (Ingi's read — correct), and
`mode` isn't dropped by accident: leaves don't need it; the ones that do override `Output`.

Reference fundamentals (`ILoadable`: image, file, url, directory) override only the **sync** `Write`, which
emits `Bytes` — a sync property **empty until loaded**. Their async materialization is split:
- `file`, `url` override `Value(data)` to **read/load bytes**.
- `image`, `directory` do **not** — they load only via `BytesAsync()`/`List()`, reached today through the
  serializer's `data.Load()` pre-pass. So `.Value()` on an image does **not** load it.

## Why the "full collapse" (delete `Write`) is bigger than 21 files

`Write(IWriter)` is not just the `Output` wrapper — it's a **sync value-emission primitive with ~15 direct
callers**:

```
channel/serializer/json/writer.cs:184   case item v: v.Write(this);      ← the writer's OWN sync Value() dispatch
type/signature/this.cs                  Algorithm.Write(w); Nonce.Write(w); Created.Write(w); Identity.Write(w); …
type/path|permission|directory/serializer/Default.cs   value.Write(writer);
type/directory/this.cs:93               p.Write(writer);
```

Removing `Write` forces all of these async — the writer's own value dispatch, signature's field writes,
three type serializers. That's a contract change to `IWriter`'s sync value path, well past the leaf collapse.

## What Ingi actually wants (kill `Load()`) is contained — and works *because* the emit stays sync

```
container Output loop:  await element.Value();   // ASYNC — renders %var%, loads image/file bytes ("where it matters")
                        <emit element>            // SYNC v.Write reads the NOW-loaded bytes
```

`.Value()` is the async load; the sync `Write` then emits already-materialized bytes. So:

1. `image` + `directory` gain a loading `Value(data)` override (parallel to `file`/`url`) → `.Value()`
   becomes the uniform materialize door for every reference fundamental.
2. `list`/`dict` `Output` loops `await element.Value()` before emitting each element.
3. Delete `data.Load()` + `data/this.Load.cs` + the two `await data.Load()` call sites (json + plang channels).

No `Write`-removal ripple. The middleman/`Write`→single-async-`Output` collapse is a **separate** pass
(the 15 sync callers), likely its own spec.

## The constraint Ingi flagged — raw passthrough must NOT parse

The dream:
```plang
- read file.json, write to %json%      // never parsed — %json% holds the RAW bytes/text
- write out %json%                      // must emit those exact bytes, verbatim
```

`data/this.Load.cs:31-35` already protects this — a `RawUntouched` value short-circuits `Load()` (no
parse; the writer emits `_raw`). **The fear is real:** if the new path calls `.Value()` on a
`RawUntouched` value, `.Value()` **parses** it (materializes json → dict/list), and the verbatim
passthrough is gone — `write out %json%` would emit re-serialized json, not the original bytes.

So the `.Value()`-materialization MUST preserve the `RawUntouched` short-circuit: **never `.Value()` a
raw-untouched value — emit `_raw` as-is.** Whatever replaces the `Load()` pre-pass has to carry that guard
forward (it's not enough to move loading into `.Value()`; the raw-passthrough branch must stay a
short-circuit, exactly as `Load()` has it today). This is the one place where "materialize at the leaf" and
"emit verbatim" collide — the ruling needs to say where the `RawUntouched` check lives in the new shape
(the container loop? `data.Output`? a guard inside the leaf?).

## Decisions needed

1. **Scope:** contained `Load()`-removal now (`.Value()` materialize + image/directory loading `Value`),
   `Write`→async-`Output` collapse as a separate pass? (coder lean — yes; the 15 sync `Write` callers make
   the collapse its own contract change.)
2. **`RawUntouched`:** where does the short-circuit live once `Load()` is gone, so `write out %json%` stays
   byte-verbatim?
3. **Store view:** `.Value()` renders `%var%`; the `.pr` (Store) write preserves refs verbatim (`data.Output`
   already skips vref resolution on Store). Loading reference-fundamental bytes is Store-safe, but rendering
   `%var%` is not — so the container-loop `.Value()` must stay Out-only (or be reference-fundamental-load
   only), not a blanket render on Store.

## Landed already (context)

- json channel drives `json.Writer` bare (`d6372446f`), no STJ in the value path; no regressions
  (Types 22 / Data 35 = baseline). Attributes still present — stripping them is gated on the read-side
  reroute + the remaining STJ write sites (`dict/list format text`, `text.Create`, `dict.Clr`, diff,
  diagnostics), which is the rest of the Json.cs sweep.
