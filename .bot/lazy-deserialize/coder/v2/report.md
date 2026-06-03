# coder — lazy-deserialize — v2 report

## Status: Stage 4 COMPLETE (4a–4e). Stage 5 = access-resolution core + scalar interpolation + Cut2/Cut4/Cut5 + ReadFailure DONE. Remaining: Cut1 + WireReadLazy (lazy Wire.Read/Write) + Cut3 (signing) — 10 deep wire/signing stubs.

### Update (later in session, all pushed)
- **Cut5** (number tower), **Cut2** (touch materializes), **Cut4** (http body-lazy/metadata-eager) green.
- **Scalar interpolation wired**: `Variables.Resolve` renders `%x%` via `ScalarValue` — bare `%cfg%` of a lazily-read object is the raw json string; dotted `%cfg.port%` still navigates+materializes. Hot path; goal suite stayed 262/0.
- **4e DONE**: http channel kind (`channel/type/http`) + `http.response` dissolved. `ParseResponseAsync` returns plain Data (body=lazy value from Content-Type, status/headers/duration=Properties); `request`/`upload` Run → `Task<Data>`; OpenAi reads body from Data value; `http.response.@this` deleted. Updated ~9 dependent C# test files (Stage3_HttpResponse/ContentTypeDispatch rewritten to the lazy contract; RequestAction/UploadAction `.Body`→`Value`/`ScalarValue`; obsolete Out/Normalize rows removed) and filled HttpChannel/Cut4/ChannelKindLayout. Goal http tests stayed green.
- **ReadFailure** green: malformed read surfaces `Data.Error`, never throws into a courier.
- C# suite: **10 fail (all stubs), 0 regressions**. Goal suite: **262 pass, 0 fail, 10 stale**.

### Still remaining (10 stubs — all deep wire-serializer / signing)
- **Cut1** verbatim passthrough (4) + **WireReadLazy** typed-slot deferral (2): both need lazy `Wire.Read` (capture the value-slot raw + defer when a type slot is present) and `Wire.Write` (emit `_raw` verbatim for an untouched raw-backed Data; today `Normalize` reads `.Value` → materializes). The blocker the architect flagged: the STJ `Wire` converter has **no actor context**, so a deferred wire Data can't dispatch the reader at materialize. Resolution path: thread context to the deserialized Data (the plang serializer / channel sets it), or give `FromRaw`-from-wire a context hook. Touches snapshot + signing round-trips → do deliberately.
- **Cut3** sign→wire→verify (3): signing round-trip incl. nested signed Data via the lean envelope. Needs identity setup + sign/verify actions.
- **assert/compare full-match scalar**: `Variables.Resolve` (interpolation/output) uses `ScalarValue`; the `assert %x% equals` full-match compare path may still read `.Value` — verify if a goal test needs it.
- **Goal `.test.goal` building**: the 10 LazyDeserialize goal tests are *stale* (need `plang build` + `config.json`/`.png` fixtures; build needs the LLM builder). Mechanism proven in C#.

---
(original v2 detail below)


Builds clean (0 PLNG002). C# suite: **4028 total, 30 fail** — every failure is an
unimplemented Stage-4e/integration-cut stub; **zero regressions**. Goal suite
(`plang --test`): **262 pass, 0 fail, 10 stale** (the 10 stale are the
LazyDeserialize `.test.goal` stubs that need 4e / goal-level scalar).

Commits on top of v1 (`6ab54bd6a`), all pushed:

- table type: `table` shape + `(table,csv)` reader; csv/xlsx → `{table,kind}`
- boundary: `channel.read` stamps `{type,kind}` from Mime → lazy Data
- channel kinds: moved stream/session/message/goal/noop under `channel/type/`
- file channel: `file.read` reads lazily through a file channel kind
- goal reader: lazy `.pr` read materializes back to a Goal
- stage 5: access-driven resolution (scalar/navigate/property/as-cast, no sniffing)
- file-backed image: preserve `image.Path` facet under lazy read

---

## Stage 4 — what landed

### 4a — `table` type + shape-based MIME (Part 4 tail)
- `app/type/table/this.cs` — grid value: `Headers` + `Rows` (each row a dict keyed by header), `RowCount`/`ColumnCount`.
- `app/type/table/serializer/csv.cs` — `(table,csv)` reader, RFC-4180 parse (quoted fields, doubled-quote, embedded newlines).
- `format/list`: csv/xls/xlsx/ods MIMEs stamp `{table,kind}` by shape (`_tabularMimeToKind`). json/xml/yaml stay `{object,kind}`.
- Updated 3 pre-existing TypedReturns assertions pinning old `csv→text` to new `csv→table`.
- No `(table,xlsx)` reader (binary, needs a library) — a `.xlsx` stamps `{table,xlsx}` and rides as raw bytes (Materialize returns the byte[]). Captured as a follow-on.

### 4b — `channel.read` is the one boundary
- `channel/this.cs`: base owns `StampReadAsync(byte[])` — the boundary. `text/plain`/unset → `{text,null}`; `application/octet-stream`/unknown → `{bytes,null}` (byte[] raw); the plang **container** (recognised by *which serializer owns the Mime* — `GetByType(Mime) is plang.@this`, **not** a string prefix, so `application/plang-goal` is correctly a value, not the container) → serializer reconstructs the Data; everything else → `Format.TypeFromMime` + lazy `FromRaw`. Text-shaped Mimes keep raw as a decoded string (Decision 3 — no utf-8 tax); binary keep `byte[]`.
- `ResolveEncoding` moved to base (one decode path; deleted the stream copy).
- `stream/this.cs`: `Read` reads bytes and stamps lazy Data — no bare text.
- **Answered Ingi's question: no `.pr` special-case anywhere.** The container is defined semantically (the plang transport serializer's registered Mime), which naturally excludes `application/plang-goal`.

### 4c — channel kinds under `channel/type/`
- Moved stream/session/message/goal/noop → `app.channel.type.*` (the `event`-binding type stays at `channel/event` — it is not a channel kind). Updated all C#/test refs + `GlobalUsings.StreamChannel`.

### 4d — file channel + lazy `file.read`
- `channel/type/file/this.cs` — filesystem channel; Mime from extension; bytes via `path.ReadBytes` (AuthGate). No `System.IO` in the channel (PLNG002 clean).
- `path/file ReadBytes`: now guards missing-file + IO → error Data (it owns the IO), so the channel stays clean.
- `file.read`: opens the file channel, returns **lazy** Data; dropped the eager image-lift + read-time convert.
- `image/serializer/Default.cs`: reader now owns `byte[]→image` materialization (the leaf's job).
- **Goal reader** (`app/goal/serializer/Default.cs`) + `format.TypeFromMime` fix: a non-primitive CLR type now keeps its real PLang name (`application/plang-goal → goal`, not `object`), and the `(goal,*)` reader re-houses the context-bound `Convert→Goal` that `ReadText` did eagerly. This keeps `GoalCall`'s lazy `.pr` load producing a Goal. **General fix, no `.pr` carve-out.**
- **File-backed image**: `file.read` rebuilds the path-backed image from raw bytes at the read site (it owns the path) so `%img.Path.Exists%` still works; the generic reader can't know the source path. Other types stay lazy.

---

## Stage 5 — access-driven resolution (core DONE)

- `data.ScalarValue` — the `%x%`/output accessor: returns the raw decoded form (utf-8 if the bytes decode, else `byte[]`; text stays text), **never** a structured parse. Authored/materialized values return as-is. (`.Value` still materializes — the Stage-3 contract for navigation/As/leaf actions.)
- **Navigation type-unknown error**: navigating by key into a value whose type is unknown (a bare string, no type stamp) now errors with `cannot navigate .X: %name% has no type; add `as <type>` (e.g. `as object/json`)` instead of a silent NotFound. Typed values still materialize via the reader first.
- **No content sniffing (Decision 4)**: deleted the `JsonString` navigator (leading-brace `{`/`[` sniff) + its file. **Zero test fallout** — it wasn't load-bearing (typed json navigates via the reader→Dictionary path; only untyped-json-string navigation relied on the sniff, and nothing in either suite did).
- `Data.As`: `as <type>/<kind>` (slash form, e.g. `as object/json`) reads toward the encoding through the reader registry — the explicit cast that replaces the removed sniff. Bare `as <type>` keeps the CLR-Convert path (unchanged).
- All 17 AccessResolution C# rows green (Scalar 3, Navigation 6, Property 2, AsCast 2, NoSniffing 4).

---

## Remaining work (for v3 / next session)

### 4e — http channel + `http.response` dissolve (Decision 6) — NOT started
The biggest remaining Stage-4 piece. Plan:
- `ParseResponseAsync` (`module/http/code/Default.cs:463`): stop building `http.response.@this`; return plain Data — **body = lazy value** stamped from `Content-Type` (via `Format.TypeFromMime`), **status/headers/duration = Properties** (`BuildProperties` already populates these — read with `!`).
- `request.cs` / `upload.cs`: return type `Task<data.@this>` (drop `<http.response.@this>`).
- `OpenAi.cs:227`: read body from the Data value, drop `as http.response`.
- Delete `app/http/response/this.cs`.
- `channel/type/http/this.cs`: new bidirectional channel kind.
- **Fallout to update (~12 C# files + goal tests)**: `Stage3_HttpResponseTests` (delete — it pins the type's shape), `OutAttributeInventoryTests`, `JsonWriterDomainShapeTests`, `RequestActionTests`, and the `Tests/Modules/Http/*` goal tests that read `%response.Body%`/`.Status` (now `%response%` = body, `%response!StatusCode%` = property). This breaking surface is why it wasn't rushed.
- Greens: HttpChannelTests (6), ChannelKindLayout `HttpChannel_Exists` (1), Cut4 (3).

### Goal-level scalar wiring — NOT done (the marquee payoff's last mile)
`ScalarValue` exists but the variable-resolution / `write out` / `assert` paths still read `.Value` (which materializes). For `ReadConfigJson_UntouchedIsJsonString.test.goal` (`%cfg%` equals the raw json string) to pass, those paths must read `ScalarValue` for raw-backed Data. **Design finding (important):** scalar `%x%` and navigation `%x.field%` are genuinely different doors — `.Value` must stay materializing (number arithmetic, leaf actions, navigation depend on it; the Stage-3 `Value_MaterialisesViaReader` test pins it), so the split is a *separate scalar accessor* wired into variable substitution, **not** a change to `.Value`. Find where `%var%` full-match resolves (around `AsCanonical`/`Variable.Get`) and the output/assert read sites; route raw-backed Data through `ScalarValue` there.

### Integration cuts (`IntegrationCutsTests/`) — NOT done
- **Cut1 verbatim passthrough**: needs `Wire.Write` to emit `_raw` verbatim for an untouched raw-backed Data (the report's deferred "RawBackedSerialize" rows). Wire-serializer change.
- **Cut2 touch materialises**: mostly testable now via file channel + ScalarValue/navigation; csv/table + number/image rows.
- **Cut3 sign→wire→verify**: signing round-trip; depends on the nested-Data lean envelope (Stage 3) + verify path.
- **Cut5 number tower round-trip**: Stage 2 is done — should be straightforward to fill.

### Follow-ons already filed (out of scope, in `Documentation/Runtime2/todos.md`)
`(table,xlsx)` reader; `table`→UI renderer; fully type-driven nested Data (a `data` type).

---

## Key design decisions made (for review)
1. **plang container = serializer ownership, not string match** — `application/plang-goal` rides as a value. (No `.pr` special-case — Ingi asked.)
2. **`format.TypeFromMime` keeps the real type name** for non-primitive CLR types (goal), so its reader can materialize toward it. Only `application/plang-goal` changes (`object→goal`); every other mapping is identical.
3. **`JsonString` navigator deleted** — content-sniffing is forbidden; verified zero fallout in both suites.
4. **`as <type>/<kind>` uses the reader registry**; bare `as <type>` unchanged.
5. **File-backed image path facet rebuilt at the read site** — the only place that knows the source path; not a lazy-vs-eager call.
6. **`ScalarValue` is a new accessor, not a change to `.Value`** — scalar and navigation are different doors.
