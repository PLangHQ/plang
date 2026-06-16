# Host-carrier handoff (2026-06-16)

**Branch:** `compare-redesign`. **Authoritative design:** `host-carrier-spec.md`
(read it first). **Architect review:** `architect/host-carrier-review.md`.

## The decision (don't relitigate)
`clr` is NOT removed — it's fixed into a **closed** foreign-object carrier. A C#
object PLang can't narrow IS an **`item`** (the apex): `type=item`, `kind` = the
declared name. No `host`/`external`/`clr` family in plang vocabulary. Engine
handles (`%!app%`, `%!callStack%`, …) ride it; they're host objects, not values.
Reflect-write is deferred (its own gate). Nested Data is abolished.

## Done & committed (in order)
- `32cfcbf2b` slice 1 — uniform value-owned navigation: `item.Navigate(parent,key)`;
  dict→key, list→index, clr→reflect host. Legacy navigator chain kept as stopgap.
- `d167d69f6` slice 2 — close the box: `clr.Peek()=>self`; Normalize unwraps the
  carrier to its host before reflecting `[Out]`. Retired the nested-Data round-trip test.
- `9bca2208f` test infra — `PLang.Tests.TestApp.Create(path)` sets
  `Tester.IsEnabled=true` → in-memory settings store. Migrated `PrPipelineTests`.
- `e581765b4` slice 3 — abolish nested Data: `clr` ctor throws on a `Data`; removed
  `Wire.cs` nested-Data read courier; retired the courier tests.

## Remaining (the next slices)
1. **`kind` via `[PlangType]` + `Mint`** — derivation is impossible (see spec
   "What goes in `kind`": `app.variable.list.@this`/`app.channel.list.@this` both
   namespace-tail to `list`; `type.Name` is always `@this`). So: declare
   `[PlangType("app")]`, `[PlangType("variable")]`, `[PlangType("channel")]`,
   `[PlangType("callstack")]`, `[PlangType("trace")]`, `[PlangType("test")]`,
   `[PlangType("serializers")]`, `[PlangType("context")]` on the concept handle
   types; then `clr.Mint()` → `type="item"`, `kind = ResolveName(clrType) ?? FullName`
   (NOT via `App.Type.Name` → `"@this"`). Watch `Loader.ReservedShadow` (a
   `[PlangType]` class declaring `type`/`error`/`success`/`@schema` fails the build —
   `app.@this` has an `Error` property, so check).
2. **§C courier-label cruft** — delete `_declared`/`Labeled`/`_declaredStrict` from
   `clr` and the `type/this.cs` Judge sites (`:451,:452,:464,:482,:483`).
3. **3 reflection consumer sites → use the plang type** (not `.Peek().GetType()`):
   `type/this.cs:276`, `condition/code/Default.cs:46`, `module/debug/this.cs` (use
   `.Type`/`Mint().Name`). They only break if a clr flows there — verify, then fix.
4. **Full Normalize dissolution** — move host-reflection off `Normalize` onto
   `clr.Write` (value owns its wire shape, OBP #9). Bigger step; needs the
   cycle-guard/visited-set re-homed. Currently Normalize unwraps the carrier (slice 2).
5. **Deferred:** reflect-write + its actor-permission gate.

## Workflow gotchas (these cost hours)
- **Test pollution:** C# test counts inflate from `PLang.Tests/Shared/Fixtures/pr/.db`
  (persisted grants, no teardown). `rm -rf` it before trusting counts; if a baseline
  stash shows the SAME inflated count, it's environment not your code. New persist
  tests → `TestApp.Create`. See `Documentation/v0.2/todos.md` + memory.
- **Speed:** targeted single-class runs (`--timeout 60s`, a whole-suite cap), NOT
  repeated full suites + stash cycles. One clean before/after comparison is enough.
- **LSP noise:** csharp-ls doesn't run the source generator → `TestAttribute`,
  `Context`, `AtKind`, `Signature`, `ICodeGenerated`, `__action` read as undefined.
  Trust `./dev.sh build`, not the LSP diagnostics, for real errors.

## Verify
`./dev.sh build`; then per-suite `PLang.Tests/<Suite>/bin/Debug/net10.0/PLang.Tests.<Suite>
--timeout 60s`. Pre-existing isolation flakes (ignore): `Authorize_GrantExists_…`,
`ReadUrl_Fetches_OverHttp` (fail in isolation on HEAD too).
