# path: a location, never a template

A path is where a file, a folder or a web resource is. It holds two facts about itself, both fixed at construction:

| field | what | file |
|---|---|---|
| `_location` | the location as given: `"data/x.json"`, `"/x"`, `"//tmp/x"`, `"http://…"` — `Raw`, `ToString`, the wire form, `FileName`, `Extension` | `path/this.cs:56` |
| `_absolute` | the resolved host form (a file's relative location anchored to the running goal's folder at resolve time) — `Absolute`, internal, behind `Authorize` | `path/this.cs:61` |

Both are private `readonly string`s. `Raw`'s `init` replaces the location as typed (`:134`); the scheme factories set
it when the developer's text differs from the resolved form.

## Never a template

A path's location is the text it was given. It is never parsed for `%variables%` and never rendered: `Value` is the
base's (answers itself), and a path is final and cacheable. So a location that came from outside stays as written:

- a listed file named `%!app.type.list.count%.txt` (`file/filesystem/this.cs:47`, `file/this.Operations.cs:139`)
- a value off the wire typed a path, marked a template or not (path `serializer/Reader.cs:25`)
- a string the program holds, taken as a path (`data.Value<path>` → `path.Create`, `path/this.cs:89–97`)

A developer's path template (`read %dir%/x.json`, the builder's `/.build/traces/%!trace.id%/manifest.json`) is not
a path yet: the build marks the `.pr` slot a template (`goal/step/action/formal/reader.cs:370`), and it is read as a
**source** of type path holding the variables its row lists (`type/this.cs:444`). The source renders its text
through text, then the path type makes itself from the rendered text (`source.code.md`, "A partial template renders
through text"). The path that comes out is plain, so what a variable held is filled once and never again.

## How a path is born from text

Every door below makes a plain path; which variables a template may fill is the source's question, never the path's.

```
path.Resolve(raw, ctx)                                path/this.cs:117
  scheme of raw (none → "file") → its kind's factory   path/scheme/this.cs:24
  ├─ file.Resolve: relative → the goal's folder         path/file/this.cs:66
  │    new file(absolute) { Raw = raw }                 :88   (an OS location: its plang form, :94)
  └─ http.Resolve: new http(raw) { Raw = raw }          path/http/this.cs:89
path.Create(value, declared, data)  — ICreate courier  path/this.cs:89   (a string → Resolve; a refusal
                                                                          lands on data: SchemeNotRegistered)
new file(absolute) / new http(raw)                      listings, derivations (Parent, Combine, WithName,
                                                        WithExtension, InFolder), a redirect's Location
```

Callers of `Resolve`, by whose text it is:

| whose text | where |
|---|---|
| the engine's own literals | `goal/setup/this.cs:41`, `app/this.cs:344,453,490`, `goal/list/this.cs:141,151,197`, `module/build/this.cs:46`, `module/this.cs:229`, `ui/code/Fluid.cs:298`, `test/report/this.cs:77`, `type/this.Generic.cs:80` |
| a value read in (a `.pr` slot, a wire value, a file's json, a goal's own path) | path `serializer/Reader.cs:25`, `serializer/Default.cs:16`, `kind/json/converter.cs:82`, `kind/reflection/this.cs:179`, `goal/serializer/Reader.cs:68` |
| a running program's or a peer's strings | `llm/code/OpenAi.cs:664` (an LLM's answer), `http/code/Default.cs:821,842,886`, `build/code/Default.cs:46`, `code/this.Snapshot.cs:108`, `shortcut/list/this.cs:52` |

## Tests

- `PLang.Tests/Types/App/Types/PathTests/OutsideTextTests.cs` — outside text stays as written (listing, wire, string
  as path); a developer's template fills and reads, a built `%!…%` renders, what a variable held is not filled again,
  an unset variable fails "not set".
- `PLang.Tests/Runtime/App/Errors/KeepsItsKeyTests.cs` — a path template to an unknown scheme fails on the Data as
  `SchemeNotRegistered`.

## Known faults

- **A plain `.pr` path slot with a bad scheme throws** out of `Value`: the path reader calls `Resolve`, which throws
  `SchemeNotRegistered`, instead of declining. A template path's lands on the Data (through `path.Create`).
