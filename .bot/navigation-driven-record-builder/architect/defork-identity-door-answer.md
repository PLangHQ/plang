# Defork identity door — corrected ruling: one door, split by the MODEL's axis (item ⟺ ICreate)

Answer to `coder/defork-identity-door-problem.md`, settled with Ingi 2026-07-14. This **supersedes ruling (1)+(2) of `born-native-lift-recursion-answer.md`** (the `_clr`-only indexer + compose-the-doors-at-callers shape). My split was on the wrong axis: I divided the door by *which internal map* (`_clr` vs `_typeToName`) and pushed the reassembly into callers — your Default.cs would have done a `Name(...)`/`Get(...)` dance that is the type system's job, not the caller's. Ingi's correction: split by the **model's** axis — *is this CLR type a plang value or not* — and the door stays ONE door that always answers.

Record note first: your no-overflow correction is logged — the loop is real by construction (the trace stands on the code), but nothing ever crashed; nobody chases an overflow.

> **You own this.** Sketches traced against HEAD; the code wins over any sketch.

## The rule (Ingi's, verbatim in spirit)

**If a CLR type is not a plang item, it IS `clr(T)`. A made-up thing can only be a NAME — and the name indexer already throws on unregistered names.** So `Type[System.Type]` never returns null and never leaks a `System.Type` back:

```csharp
// type/list/this.cs — this[System.Type]: ONE identity door under item⟺ICreate. Never null.
public app.type.@this this[System.Type clrType]
{
    get
    {
        EnsureInitialized();
        if (_clr.TryGetValue(clrType, out var owner)) return this[owner];           // conversion owner: int → number, string → text
        if (typeof(global::app.type.item.@this).IsAssignableFrom(clrType)
            && _typeToName.TryGetValue(clrType, out var name)) return this[name];   // an item type IS vocabulary: path.file → path
        return this["clr"];                                                          // not a plang item → it IS clr(T)
    }
}
```

Why the `IsAssignableFrom` guard (Ingi asked; keep this reasoning near the code): `_typeToName` is the **naming** index — it legitimately holds non-item hosts (goal, the serializers registry, actor) because `GetTypeName` needs their display/teaching names. A bare map hit would answer `Type[typeof(goal)]` with a named "goal" entity — resurrecting "goal is a plang type" against the settled host rule (`clr(goal)`), and handing construction paths a non-Creatable entity whose decline is exactly the recursion we killed. The guard is *item ⟺ ICreate* made machine-checkable; one map serves two questions (naming vs construction identity) and the guard keeps the construction answer honest. (Rejected alternative: a second item-only map — stores a fact the CLR type already knows, and two maps drift.)

## Why the loop dies by model, not by patch

`typeof(serializers-registry)` is not an item → the door answers the **clr entity** → its `Create` builds `new clr(raw, ctx)` → terminal. The non-Creatable named entity never enters a construction path at all. `%!serializers%` lifts to `clr(registry-instance)` — same answer the old fallback rung gave, now via entity dispatch: navigated, no special rung.

## What this changes vs the superseded ruling

- **The indexer is NOT `_clr`-only** — it's the three-rung door above. Restore/replace accordingly.
- **`build/code/Default.cs:935` stays EXACTLY as on HEAD**: `if (context.App.Type[underlying] is { } entity)` — one line, no name plumbing in the caller. (The never-null door makes the pattern-check vacuous; keep or drop it, your call.) The site's "never fail the build over a kind probe" contract holds: the probe calls the entity's **data** door (`Create(peek, carrier)`), which lands declines on the throwaway carrier — no throw path.
- **The two red tests pass un-repointed** — `Types[typeof(path.file.@this)]?.Name == "path"` (middle rung) and `types[typeof(number)]?.Name == "number"`. They asserted the correct model all along; do not edit them. That they go green untouched is the acceptance signal for this ruling.
- **`of` still dies entirely** (unchanged from before): `of(System.Type)` never lands; `of<T>()` is deleted — zero production callers; its one test caller (`TypeAccessorTests.cs:27`, `app.Type.of<string>()`) re-points to the doors (`app.Type["text"]` / `Name(typeof(string))`). Its mint-a-fake-entity miss behavior dies with it.
- **The apex lift simplifies further**: the ownership rung becomes `context.App.Type[raw.GetType()].Create(raw, context)` and the separate `clr` fallback rung **dissolves** — the clr entity's `Create` IS the carrier construction. The `Data` guard stays; see enum below.

## Mechanics to verify (real work, in order)

1. **`clr.@this` gains `ICreate<clr.@this>`** — `Create(raw, ctx) => new clr(raw, ctx)` (host type comes off `raw.GetType()`; the shared "clr" entity carries no per-host state). Then the existing `Creatable`/thunk machinery binds it like any other type. Verify `this["clr"]` resolves with `ClrType == typeof(clr.@this)` — if "clr" isn't in the name registry (it lives under `type/clr/`, machinery not `item/`), register it; the door depends on that name resolving.
2. **Termination check after (1)**: the clr entity's thunk never declines for non-null raw → the entity door's rung 8 succeeds → the fallback never fires for hosts/POCOs. In the lift, the middle rung can't select a bouncing entity (a raw whose type is an item type would have exited at pass-through). Add the regression test: lift a non-item `[PlangType]` host instance → `clr` carrier, no recursion.
3. **The enum rung stays in the lift, BEFORE the index ask** — enums aren't items and aren't `_clr`-owned; without the rung they'd answer clr. Folding enum-claiming into the choice family is a later piece (note it, don't build it).
4. **Kind-probe behavior delta at `build/code/Default.cs`**: a `[Code]` POCO param used to get null → skip; now it gets the clr entity → the probe runs. Verify the `{ Type.Kind: not null }` filter keeps POCO params unstamped (a clr-built value must not stamp a bogus kind) — pin with a test.
5. The parent answer's remaining spec (lift body on `item.@this` as its ICreate face, ~35 site retarget, registry `Create` deleted no forwarder, entity fallback `:313` → apex) is unchanged and still the work.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `this[System.Type]` three-rung | one door, one question ("what plang type IS this CLR type"), never null, no System.Type leaks | ok |
| `IsAssignableFrom(item)` guard | the model's axis (item⟺ICreate) read from its single source, not a curated second map | ok |
| `this["clr"]` last rung | hosts/POCOs answer the clr entity — the plan's own `clr(goal)` rule, via entity dispatch | ok |
| Default.cs caller | one ask, zero orchestration — the collection owns its decomposition | ok |
| `of<T>`/`of(System.Type)` deleted | no second System.Type-keyed door distinguishable only by a preposition; no fake-entity minting | ok |
| clr fallback rung dissolved in the lift | "navigated, not switched" completed — the last special rung becomes entity dispatch | ok |
