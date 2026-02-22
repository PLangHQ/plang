# TDD Bot Pipeline Design

## Summary for Bot Creator

### Current Pipeline
architect → coder → tester

### Proposed Pipeline
architect → scaffolder → pretests → coder → tester

### Why

In TDD, the person writing the failing test and the person writing the implementation share a mental model — they agree on method signatures, type shapes, and API surface. When you split that across bots, the shared mental model must become an explicit artifact, or the bots drift apart.

The architect thinks at the design level — "add a Kind property to Type that lazily resolves through the engine graph." That's the right level for architecture. But two downstream bots reading that narrative will imagine different method signatures, different conventions, different file placements.

The **scaffolder** bridges this gap. It takes the architect's design and produces the precise type skeletons — empty classes, interfaces, method signatures, file placements — that all downstream bots work against. It's the single bot that knows both the architect's design language AND every code convention.

---

## Bot Roles

### Architect (unchanged)
- **Input**: Raw idea or design seed from user
- **Output**: `plan.md` — narrative design document describing what to build, why, and the key design decisions
- **Does NOT need to know**: Code conventions, file naming rules, OBP enforcement details, source generator constraints
- **Thinks in**: Systems, tradeoffs, design intent

### Scaffolder (new)
- **Input**: Architect's `plan.md`
- **Output**: Compilable type skeletons — empty classes, interfaces, method signatures, file structure, `using` statements. The code compiles but does nothing.
- **Must know**: ALL code conventions — OBP patterns, `@this` convention, naming rules (nouns, one word), source generator constraints, file placement, `partial class` patterns, navigation-not-passing, behavior-on-owner. This is the bot that enforces the codebase's structural rules.
- **Key principle**: The scaffolder's output is the **contract**. Every downstream bot works against these types. If the scaffolder puts `Kind` as a property on `Type`, the pretests bot writes `Assert.Equal(Kind.Text, type.Kind)` and the coder implements that exact shape. No drift.

### Pretests (new)
- **Input**: Scaffolder's type skeletons
- **Output**: Failing unit tests — both C# xUnit tests and PLang `.goal` tests where applicable. All tests must compile and fail (red phase of TDD).
- **Must know**: Testing conventions, assertion patterns, how to construct test fixtures against the skeleton types
- **Key principle**: Tests are written against the scaffolder's contracts, not against imagined APIs. The tests define the expected behavior; the coder's job is to make them green.

### Coder (adjusted)
- **Input**: Scaffolder's type skeletons + pretests' failing tests
- **Output**: Implementation that makes all tests pass (green phase)
- **Adjustment**: The coder no longer invents the API surface — it's already defined by the scaffolder. The coder fills in method bodies, wires navigation, implements lazy resolution. Its success criteria is clear: all tests green.

### Tester (unchanged)
- **Input**: Coder's implementation
- **Output**: Additional integration tests, edge case coverage, validation that the full pipeline works (builder → .pr generation → GoalMapper → runtime)

---

## The Contract Flow

```
Architect          Scaffolder              Pretests           Coder
   |                   |                      |                 |
   | plan.md           |                      |                 |
   |------------------>|                      |                 |
   |                   | type skeletons       |                 |
   |                   | (compiles, empty)    |                 |
   |                   |--------------------->|                 |
   |                   |                      | failing tests   |
   |                   |                      |---------------->|
   |                   |                      |                 | implementation
   |                   |                      |                 | (tests go green)
```

Each handoff has a concrete, verifiable artifact. The scaffolder's skeletons compile. The pretests' tests compile and fail. The coder's implementation makes them pass. No ambiguity at any boundary.

---

## Why This Works Better Than Alternatives

**Why not have the architect write contracts?**
The architect would need to know every code convention — file naming, OBP enforcement, source generator rules. That pulls it out of design thinking and into code thinking. Keep the architect high-level.

**Why not have the pretests bot also scaffold?**
Then the pretests bot makes architectural decisions (type shapes, API surface) that the architect should own. The scaffolder translates the architect's intent faithfully; it doesn't redesign.

**Why not skip pretests and just have coder + tester?**
Without pretests, the coder decides what "done" looks like. With pretests, the failing tests define done before implementation starts. The coder has clear, mechanical success criteria: make the red tests green.

---

## Scaffolder Conventions It Must Enforce

This is the critical list. The scaffolder is the single point where code conventions are enforced structurally:

1. **`@this` convention** — every folder's primary class is `@this` in `this.cs`
2. **OBP: behavior on owner** — methods go on the object that owns the data
3. **OBP: navigate, don't pass** — dependencies reached through object graph, not parameters
4. **OBP: one-word noun names** — `Types` not `TypeMapping`, `Kind` not `Category`
5. **Partial class pattern** — one object, multiple roles via partial classes
6. **No static classes** — instances on engine, not static helpers
7. **No verbs/prepositions in APIs** — `data.Type.Kind` not `TypeMapping.GetCategory(ext)`
8. **Lazy navigation** — properties resolve on first access through the graph
9. **Source generator awareness** — knows which interfaces trigger generation, what `ICodeGenerated` means
10. **File system abstraction** — never `System.IO`, always `IPLangFileSystem`
11. **`System.Text.Json`** — never Newtonsoft in Runtime2
12. **Strong typing** — never weaken to `object`
