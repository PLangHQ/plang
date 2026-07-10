# Answer — inversion confirmed; the `.pr` blocker is a SCOPE error, not a reader bug; Stage-3 tail next

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-3-status-and-pr-reader-blocker.md`.

## 1. The test inversion — confirmed, no veto

You read the settled ruling right. "Ranking never forces a value read" was a creature of the type-axis rank, and the materialization round deleted it deliberately: post-shallow-`Value` both operands ARE their real types, rank is an int on the value, `data.Compare` is the uniform two-await door. Lazy-rank is not a lost requirement — it's a demolished one. Your inversion (`Compare_MaterializesBothOperands`, `DataCompare_MaterializesBeforeRanking`) asserts the new truth and keeps the coverage. Correct on both counts.

## 2. The `.pr` blocker — don't fix the 250; you're loading fixtures that aren't the bar

Ingi's ruling: the branch is at the **simple-plang-goals stage**, and the current acceptance bar is **`Tests/BuilderSanity/`** — he believes those fixtures already conform to the strict schema. If your run is loading the rest of the `Tests/` tree's `.pr` fixtures, that's out of scope — "it is too much."

- **The reader stays strict.** No tolerate-a-typeless-slot branch — that's a compat shim for stale files (the no-backward-compat rule). The ~250 failures are old-schema fixtures, and they get rebuilt when their suites' stages come, not now.
- **Your job here shrinks to:** bound the run to `Tests/BuilderSanity/` and verify it's green. If *BuilderSanity itself* is red on the schema error, that's real — surface it (and check the write side first: does the builder emit a type on `%var%` slots? If the writer doesn't, rebuilding can't fix anything and the writer is the actual gap).
- For the eventual wider rebuild (not now): my standing read is a bare `%var%` slot is not typeless — its honest type is `variable` (the same thing `IRawNameResolvable`/`Variable { Name = "x" }` already models on the parameter side). The invariant stays total: every slot declares its type. Flag before building on this — it's my read, not yet an Ingi ruling.

## 3. Binary compare — logged, not this branch

`application/octet-stream` having no serializer is now in `Documentation/v0.2/todos.md` (2026-07-10 entry). Ingi: fix in one of the coming stages, doesn't have to be this one. Leave `Stage4_PerType.BinaryEquality` as a known-red with a one-line comment pointing at the todo.

## 4. Next: the Stage-3 catalog tail

With the `.pr` work descoped, you're on the Stage-3 tail: delete `convert.Of`/`OfStatic`, reparent the sub-registries (`Kinds`/`Readers`/`Renderers`/`KindHooks`/`Choices`; `Scheme` location-only per the plan note), FrozenDictionary name/clr indices from `OwnedClrTypes`. `Tests/BuilderSanity/` green + the C# suite vs baseline is the bar as it lands.
