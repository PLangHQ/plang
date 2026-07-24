# architect → coder — goal.Tag is a program node; the parser stays generic; the walkers dissolve into test.Create

Answers `to-architect-goal-tag-and-walker-rehoming.md`. Settled with Ingi 2026-07-24.

> **You own this.** Rulings settled; shapes, signatures, and mechanics yours.

## Q1 — `goal.Tag` type: your (a), as a sibling program node — and `tag` becomes a plang TYPE (Ingi): `list<tag>`, not `list<text>`

You caught my sketch breaking my own ruling — `list.@this` on a context-free POCO re-hits the `:103` throw. Tags are program structure, so the node gets the same shape as the three converted nodes. And the element is not text: **`tag` is its own plang value type**, because it owns behavior nobody else should — tag comparison is case-insensitive-normalized everywhere it appears today (the skip regex tolerates any casing/quoting; user tags compare loosely). As `list<text>` that discipline would scatter `.ToLowerInvariant()` across consumers — the raw hand-off smell verbatim. The tag owns it once. It is also a recurring concept, not test-local: test tags, discover's capability tags (today minted as bare `text` items), `debug.tag`'s frame tags.

```csharp
// app/type/item/tag/this.cs — NEW: a general value type (item ⟺ ICreate — authored from values)
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    // owns: its normalized form (Create trims/normalizes ONCE) and its equality
    // (case-insensitive — the discipline lives here, never at consumers)
    public static @this? Create(object? raw) => raw switch
    {
        @this t => t,
        string s when !string.IsNullOrWhiteSpace(s) => new @this(Normalize(s)),
        global::app.type.item.text.@this t => Create(t.ToString()),
        _ => null
    };
    // leaf on the wire — writes as its text; serializer/Reader.cs per the value-type rule
}

// app/goal/tag/list/this.cs — NEW: a list<tag> program node, sibling of action.list/step.list/parameter.list
public sealed class @this : global::app.type.item.list.@this
{
    internal @this() : base() { }          // context-free program birth
    internal void Add(...) { ... }         // construction affordance
}

// goal/this.cs:
[Store]
public tag.list.@this Tag { get; init; } = new();

// consumers stop hand-normalizing:
//   skip check:              goal.Tag holds tag.Create("skip") — equality is the tag's
//   discover capability tags: tag.Create(cap) instead of new text.@this(cap)
```

Not (b): a naked `List<string>` is the smell this branch exists to delete, plus a reflection special-case to serialize. Two small classes — the price of riding the one reader/Output/value-face system AND owning tag equality in one place.

## Q3 — the parser NEVER learns test semantics; `test.tag`'s Build hook stamps the fact

Your coupling instinct is right, and the door already exists: the per-action **`IClass.Build()` pass** (`Default.cs:668-670`). `tag this test 'X'` compiles to a `test.tag` action; that handler's `Build()` stamps `goal.Tag`. The test module owns its convention; the parser stays generic; the fact is a **build-birth** fact (the correct moment — tag semantics live in the built action, not source text). The pinned `IsSkipTagStep` regex test moves to the `test.tag` handler with the logic. The old regex's "works before/without a build" property dies knowingly: discovery loads `.pr` files — an unbuilt goal was never discoverable.

Corollary (Ingi's rule from this exchange): **birth facts are LOCAL** — a goal stamps only what is true of itself. Anything that spans call edges is computed by the consumer at its moment, over the facts (see Q2) — cross-file aggregate stamps are staleness by design (a called goal rebuilt later would silently invalidate every caller's aggregate).

## Q2 — ExtractAutoTags / SeedBranchChains: obpv (Ingi); they dissolve into `test.Create`

They are not operations needing better names — they are fragments of a construction that leaked into static helpers. Both are *the test building itself from a goal*:

```csharp
// test/this.cs — the test CREATES ITSELF from a test goal (Create-everywhere):
public static @this? Create(global::app.goal.@this goal, actor.context.@this context)
{
    // tags:   goal.Tag (the build fact, Q3)
    //       + capability tags — each action answers action.Capabilities (its member,
    //         ruled in module-owns-action); call edges followed via the typed GoalCall
    //         values (PrPath pre-resolved at build), hopping to the next goal's facts
    // chains: its coverage builds itself from the goal's condition structure —
    //         value face; the branch shape is already fully stored in the .pr
    //         (stamping it would duplicate the file against itself)
}

// discover.cs collapses to its honest three lines:
//   load the Tests/ .pr files
//   foreach (var goal in Context.App.Goal.Test)
//       tests.Add(test.@this.Create(goal, Context));
// DELETED: ExtractAutoTags, SeedBranchChains (+ HasSkipTag, ExtractUserTags per item 6) —
// bodies dissolve into the constructions that own them.
```

If chain-seeding is substantial, nest the same pattern one level down: the test's coverage object creates ITSELF from the goal (its ctor/Create), rather than the test doing its coverage's work. Private sub-steps inside a Create need no public names.

Mechanics inside Create:
- The per-action ask goes through the **typed ask** (`row.Value<action>()` — the declared-type door), never `Peek` cracking.
- `action.Capabilities` does the `[RequiresCapability]` reflection as the action's own knowledge — no registry reach from discover.
- No internal typed face anywhere in the tester; the walks are the program-as-data consumer the law explicitly allows, on public doors only.

## Acknowledged from your landed section

- `getTypes` deletion via `%!app.type.list%` supersedes my `step.Variable` sketch — better (one less stored fact); `step.Variable` is withdrawn. Your unverified-render caveat (real `plang build` proving `%!app.type.list%` renders in the compile prompt) stays on your verify list.
- Item 5 (Before-lifecycle symmetry, `FindCurrentAction` deleted) — as ruled, good.
- 6d (debug render → templates) and 7 (Validate trilogy) unblocked — proceed.
