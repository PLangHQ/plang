# Stage 4: One I/O boundary — file and http become channels

> **Note for coder:** every path, channel kind, and Mime mapping below is a **suggestion** that captures architect intent — not a contract. You own the implementation, especially whether `file` is a new channel kind or reuses `stream`. Push back if a placement reads wrong.

**Goal:** Channel is the foundational I/O layer. There is one read verb — `channel.read` — that stamps `type`/`kind` from the channel's `Mime` and produces a lazy `Data`. `file` and `http` stop being self-contained I/O actions and become channel kinds; everything reads through the one boundary. `http.response.@this` dissolves into plain `Data`.

**Scope:**
- `app/channel/this.cs` — the base `Read` (`:101`) becomes the boundary: stamp `type`/`kind` from `Mime` (`:38`), produce lazy `Data`.
- `app/channel/type/` — all channel kinds move here (the existing `stream`, `session`, `message`, `event`, `goal`, `noop`, plus the new `file`, `http`).
- `app/channel/type/file/this.cs` — new: filesystem channel kind, `Mime` from extension, bytes via `path.ReadBytes` (AuthGate stays on path).
- `app/channel/type/http/this.cs` — new: http channel kind, bidirectional, `Mime` from `Content-Type`.
- `app/module/file/read.cs` — opens the file channel and reads; stops converting at read time.
- `app/module/http/code/Default.cs` — opens the http channel; body→value, metadata→properties; stops `Content-Type` deserialize.
- `app/http/response/this.cs` — **deletes** (dissolves into Data).
- `app/format/list/this.cs` (`TypeFromMime` `:415`, `TypeFromExtension` `:446`) — shape-based stamps: json/xml/yaml → `{object, kind}` (unchanged), csv/xlsx → the new `{table, kind}`.
- `app/type/table/` — new type family (grid: rows/columns/headers) + `serializer/Default.cs` Read; `(table, csv)` reader now, `(table, xlsx)` a follow-on.

**Dependencies:** Stages 1–3 (the reader registry, exact numbers, and lazy `Data` — the boundary produces lazy Data that materializes through the registry).

**Out of scope:**
- Access-pattern resolution (Stage 5) — Stage 4 produces lazy Data; *how* a touch resolves it (scalar vs navigate vs cast) is Stage 5.
- `.plang` container internals (the self-describing `application/plang` format) — a separate branch. Stage 4 assumes `channel.read` can stamp type/kind from Mime, and (for an `application/plang` payload) that the serializer can read type+kind from the container; designing the container is not here.

**Deliverables:**

1. **`channel.read` is the boundary.** The base channel `Read` (`app/channel/this.cs:101`) stamps `type`/`kind` from `Mime` (`:38`) via the existing mime mapping (`Format.Mime`, `Format.TypeFromMime`, `ClrFromMime`) and produces `Data { raw = stream content, type, kind, value: lazy }`. The stream channel's bare-text return (`app/channel/type/stream/this.cs:69`, after the move) stops.
2. **All channel kinds under `channel/type/`.** Move the existing kinds and add `file`/`http`. (`channel/` keeps `list/` the collection and `serializer/`.)
3. **`file` channel kind.** Filesystem-backed, `Mime` from extension. Reads bytes through `path.ReadBytes` — which holds the AuthGate — so the channel does no `System.IO` of its own (PLNG002 stays clean). `file.read` (`app/module/file/read.cs:27`) opens it and calls `channel.read`; the read-time `Context.App.Type.Convert(text, materialized, …)` in `FilePath.ReadText` (`app/type/path/file/this.Operations.cs:61`) goes away. May reuse the existing `stream/` kind if a separate one isn't worth it — your call.
4. **`http` channel kind.** Bidirectional — write the request, read the response. The response **body** is the lazy value (type/kind from `Content-Type`); **status, headers, duration** become Data **properties** (read with `!`) — what `BuildProperties` already attaches. `http.get` (`app/module/http/code/Default.cs:463`) opens it and reads; stops deserializing by `Content-Type`.
5. **`http.response.@this` dissolves.** Delete the record (`app/http/response/this.cs`). Result is plain Data: body in the value slot (lazy), `status`/`headers`/`duration` in properties.
   ```
   - get http https://api/...     write to %response%
   - if %response!status% == 200    / property read — body untouched
       - write out %response.name%   / body materializes: raw → json → .name
   ```
6. **Shape-based MIME mapping + the `table` type.** `Format.TypeFromMime` / `TypeFromExtension` stamp by shape: `application/json`/xml/yaml → `{object, kind}` (keeps today's json→object — `app/format/list/this.cs:443`), `text/csv`/xlsx → the new `{table, kind}`. `config.json` → `{object, json}`; `report.csv` → `{table, csv}`. `table` is a new type family this stage (grid: rows/columns/headers); its `(table, csv)` reader lands here, `(table, xlsx)` is a follow-on (binary, needs a library — a `.xlsx` still stamps `{table, xlsx}` and rides as raw bytes until then).

## Design

**Channel is the foundation; file and http are channels, not peers of channel.** Today both do their own I/O and their own deserialize, three different ways (`file` by extension, `http` by `Content-Type` into a second type, `channel` ignoring `Mime`). After this stage there is one read path: open a channel (of whatever kind), `channel.read` stamps from `Mime`, you get lazy Data. The permission model for files stays exactly where it is — on `path`'s gated verbs — because the file channel goes through `path.ReadBytes`, not `System.IO`.

**Status/headers are properties, not the value.** The body is the payload (lazy); status/headers/duration are metadata read at the boundary from the protocol. Putting them in `Data.Properties` (read with `!`) means `if %response!status% == 200` never touches the body — a status check doesn't force a parse. This also dissolves the parallel `http.response` type: the result is plain Data like everything else.

**The wire-container case.** When a channel's `Mime` is `application/plang`, the bytes are the Data container (binary, or text/json by default) — the serializer reads type+kind from it and produces lazy Data (this is `Wire.Read` from Stage 3). When the `Mime` is a bare value type (`application/json`, `image/png`, `text/csv`), the bytes are a value — stamp `(type, kind)` from the Mime and the raw is the value's source form. So `application/plang` ⇒ container (Stage 3's lazy `Wire.Read`); `application/json` ⇒ an `{object, json}` value. Don't conflate them.

**Shape-based typing, and why it's still lazy.** The type names the value's *shape* — `object` for json/xml/yaml (a tree, navigated by key), `table` for csv/xlsx (a grid, navigated by row/column) — and `kind` is the encoding. Stamping the type does **not** parse: `config.json` lands `{object, json}` with `raw` = the json string, untouched, and the structure materializes only when `%cfg.port%` navigates. json→object keeps today's behavior; the new work is the `table` type for csv/xlsx — grouping by shape is what lets a renderer draw a grid by dispatching on `type=table` alone. The rest of the stage is plumbing.
