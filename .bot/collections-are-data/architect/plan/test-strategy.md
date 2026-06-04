# Test strategy — Collections are Data

For the test-designer. Maps each stage to its test layer, names the load-bearing proof, and gives the per-stage integration cut.

## Two layers

- **C# unit** (`PLang.Tests`, `dotnet run --project PLang.Tests`) — the value types in isolation: `dict.Get`/`Keys`/`Has`/truthiness, `list` navigation + intrinsics, the writer's type-based `[]`-vs-`{}` disambiguation, `Conversion` element-unwrap, `Variables.Set` rebind + subscriber-carry, typed compare.
- **PLang integration** (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`) — the developer-visible behavior end to end: `read file.json` → navigation, `set`/`add` aliasing, sign→add→verify, `sort`/`where`/`group`, `is` queries.

Put per-element provenance and the read→navigate→write path at the integration layer — that's where F1 actually lived; a C# unit test on the writer alone would have missed it.

## The load-bearing proof (Stage 3)

```
- sign %x%
- add %x% to %list%
- verify %list[0]%        / must verify
```

This **must fail before Stage 3 and pass after**. It is the regression anchor for the whole branch — a signed `Data` surviving at rest inside a collection. Land it as a failing test first; it proves the [raw, Data] split is gone, not just that arrays serialize.

## Per-stage integration cuts

- **Stage 1 (`dict`)** — `set %u% = {name:"a",age:30}` → `%u.name%`/`%u.age%`; nested `%person.address.city%`; a C# record (`permission`) round-trips `{}`; `count`-key-vs-size precedence. Arrays still `[]` (regression guard — they must not change yet).
- **Stage 2 (`set` rebinds)** — `set %x%="a"` / `add %x% to %list%` / `set %x%="b"` / `%list[0]%`=="a"; `OnChange` fires on rebind; the forked-flow / parallel-foreach variant (the `:199` branch — a `set` in a branch must not rewrite a parent-stored value).
- **Stage 3 (arrays)** — the load-bearing proof above; literal `[1,"two"]` → both elements are `Data`; array-root `read file.json` → `%content[0].name%`; read-json→write-json passthrough leaves `RawUntouched` true (no parse — `Data.Load()` short-circuits, row Q); a signed image at `%list[2]%` loads + verifies through the `Data.Load()` per-element walk.
- **Stage 4 (compare)** — `sort by "age"` numeric, `by "name"` lexical, `by "born"` chronological; `if a.age > b.age` and `sort by "age"` agree; mixed-type/null per the settled contract. *(Gated — write once the contract lands.)*
- **Stage 5 (ops + `where`)** — `where %users% age > 20` filters; `where %user% age > 20` keeps/drops one dict; `group` buckets are `Data<List<data>>`. *(Depends on Stage 4.)*
- **Stage 6 (`item`)** — `if %x% is item` for any value; `if %x% is dict` after an object narrows, false for a list; IS-A survives the lazy narrow.

## You own the final shape

Test names, fixture files, and assertion style are yours. Keep the layer split and the load-bearing proof; everything else is suggestion.
