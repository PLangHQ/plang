# Coder summary — navigation-driven-record-builder

**Version:** v1 (continuing)

## What this is

The born-native-lift **defork**: "build whatever this raw is" moved off the type registry
onto `item.@this.Create` (item's own ICreate face), and the registry keeps only SELECTION.
The blocker was a recursion in the CLR-type selection door.

## What was done (latest — architect ruling `defork-identity-door-answer.md`)

The recursion root was the `this[System.Type]` indexer conflating two questions (conversion
`_clr` vs identity `_typeToName`). Two architect rulings landed:

- **Ruling (1)** (superseded): make the indexer `_clr`-only, compose identity at callers via
  a new `of(System.Type)`. Ingi rejected the new `of` method; I wrote it up
  (`coder/defork-identity-door-problem.md`) and also **corrected the record: there is no live
  StackOverflow** — my earlier "overflow" was a `grep` matching source text in a catch-filter.
  The loop is real *by construction* (the architect confirmed) but nothing ever crashed.

- **Ruling (2)** (current, implemented): **one never-null identity door**, split on the
  MODEL's axis (is this CLR type a plang item?):
  ```
  this[System.Type]:  _clr owner  ->  item-is-vocabulary (IsAssignableFrom guard)  ->  this["clr"]
  ```
  `of<T>`/`of(System.Type)` **deleted**. `clr.@this` gains `ICreate<clr.@this>` so the "clr"
  entity's `Create` builds the carrier — the apex lift's separate clr rung **dissolves** into
  plain entity dispatch, and the entity↔apex bounce dies by the model (a non-item host answers
  the clr entity, whose build is terminal).

### Files modified
- `PLang/app/type/list/this.cs` — three-rung never-null indexer; `of` deleted.
- `PLang/app/type/clr/this.cs` — `ICreate<@this>` + ctx `Create` face (`new clr(raw, ctx)`).
- `PLang/app/type/item/this.cs` — apex lift: enum rung before the index ask; ownership + clr
  rungs collapse to `Type[raw.GetType()].Create(raw, context)` (terminal).
- `PLang/app/module/build/code/Default.cs` — kind probe reverted to `Type[underlying]`; guard
  `built is not clr.@this` so a clr carrier's `item/*` never overwrites a param's declared type.
- Tests: `ClrKindNavigationTests` (+5 regression), `TypeAccessorTests` (of<T> → indexer),
  `NumberValueTests`/`DistributedOwnerOfTests` pass **untouched** (acceptance signal).

### Verification (by-name full-suite diff vs pre-existing HEAD baseline)
- **Zero genuine new reds.** Only "new" name is `SlashName_Resolved_ByRootRelative` — a
  pre-existing flaky red (fails identically on HEAD; goal-load reflection-reader cluster).
- **Net −10 reds** (369 → 359): fixed Report_*, If*_Orchestrate, ErrorAsStringSlot.
- The 2 identity tests pass un-repointed; +5 new regression tests green; build clean.

## Code example (the door)
```csharp
// ONE identity door, never null, split on item ⟺ ICreate:
if (_clr.TryGetValue(clrType, out var owner)) return this[owner];        // int → number
if (typeof(app.type.item.@this).IsAssignableFrom(clrType)
    && _typeToName.TryGetValue(clrType, out var name)) return this[name]; // path.file → path
return this["clr"];                                                       // POCO/host → clr(T)
```

## What's left
- Deferred (architect-logged, not this branch): snapshot RESTORE rebuild; PLNG004 render
  worklist; clr/format/text possibly-legit STJ boundary; the enum→choice folding into the
  choice family (noted in the lift comment, not built).
- The 2 pre-existing `ValidateActions_*` reds and the goal-load reflection-reader cluster are
  HEAD-level, not from this defork.
