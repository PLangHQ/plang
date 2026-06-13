# coder — compare-redesign — summary

## What this is
The typed-value model: every value is its own typed instance; `Data` is a thin
binding holding an item; deserialize (raw → typed item) is owned by each type;
`Judge` (the central type reconciliation) is to be deleted with its work moved
onto the types.

## Latest state (deserialize-flow design pass)

The Judge-removal arc reached the deserialize path and stalled there. Routing
`Wire.ReadBody` through the type's reader was directionally right (kind is born by
the type) but the path underneath was wrong, producing reactive patches
(JsonElement unwrap, try/catch on reader throws, an item.source fallback). Those
were symptoms.

Worked the root with Ingi and agreed a clean design — written up in
**`deserialize-flow-design.md`** (coder root, for architect review). Core:
- A Data is one flat layer `{@schema?, name, type, value}`; `@schema` optional.
- `value` is the **type's** responsibility: Data reads `name`+`type`, hands the
  positioned **reader** to the type; the type parses its own value.
- The reader is serializer-independent — an **`IReader`** abstraction, the
  symmetric mirror of the existing `IWriter` write side. Types know `IReader`,
  not `Utf8JsonReader`.
- **Single pass, no DOM, no double-parse.** Today the value slot materializes a
  `JsonElement`/`JsonDocument` (and sometimes re-stringifies via `GetRawText`)
  then re-parses — the source of every bug. `type` precedes `value` on the wire,
  so the type consumes value tokens directly off the one pass.
- `value` stays a **sibling** of `type` (not nested) — `type` must stay a pure
  reusable identity that serializes standalone when there's no value.
- This removes `IsDeferrableShape`/`deferredRaw`/`LiftDataIfShaped`/`_readDepth`,
  the reader-registry context-less `Shared` fallback, the JsonElement unwrap, and
  `Judge`'s kind/strict reconciliation. Containers (`list`/`dict`) own recursing
  into their elements (element-parsing moves out of `Wire` into those types).

Open questions for architect captured at the end of the design doc (IReader
surface, type-before-value invariant, polymorphic/no-type default reader,
container element parsing).

## What was done this session
- **Reactive production patches reverted** to milestone (`Wire.cs`, `type.cs`,
  `reader/this.cs`) — superseded by the agreed design above. Working tree clean.
- **Committed (stands alone): test-caller migration** that unblocks `dev.sh build`.
  The deferred CLR-typed `Get<T>` / 3-arg `Set` test callers were migrated to the
  new contract: scalar reads → `GetValue`; `Get<number>`/`Data<T>` for the
  generic-Get tests; native `list`/`dict` + store-clone-isolation for the
  container clone tests; value-owns-its-type for the former `Set(name,value,type)`
  tests. `dad1824b1`. `./dev.sh build` is green again (was broken by these
  callers, which had blocked all reliable measurement).

## Next
Architect reviews `deserialize-flow-design.md`; then implement the `IReader`
read path + flatten `Wire.ReadBody` to `{@schema?, name, type} → type reads value`
+ move container element parsing onto `list`/`dict` + delete `Judge`.

## Tooling note
`plang --test`'s test binaries segfault non-deterministically after printing, so
counts vary run-to-run; trust targeted `--treenode-filter` runs and failure-set
diffs (not raw counts). `dev.sh build` only works now that the `Get<T>` callers
compile.
