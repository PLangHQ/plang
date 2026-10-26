# The core is locked

**Chosen.** The surface of the core does not change without the project owner's say-so: `app`'s members, the type system and the serializer, `data`, `channel`, `call` and variable memory, events, the module registry and catalog, and the core value types' public surface. Inside them, only behaviour fixes and private refactors. An addition is a short proposal to the architect, who brings it to the owner; nothing is built before the yes (`CLAUDE.md`, "The locked core"). New needs go to the edge: a module owns its own types and declares its own events.

**Why, as recorded.** `CLAUDE.md` calls them "the load-bearing shapes the rest of the system is built on". The surface test says what a change here is: "An addition to the core: it fails until it is re-pinned through AcceptTheSurface, and the commit that re-pins names the decision that allowed it" (`PLang.Tests/Wire/App/Surface/SurfaceTests.cs:7-9`). A fuller reason, and the date and the person who decided it: reason not recorded.

**Rejected.** Not recorded.

**The decisions that re-pinned it.** `Documentation/v0.2/type-system.md:31-34` lists three additions one branch made, each with its approving decision number: `OwnInternal` (831), `file.read`'s `Type` slot (832) and the `name<kind>` face (834).

**Held by.** `SurfaceTests.TheCoresSurface_IsThePinnedOne` and `PLang.Tests/Wire/App/Surface/surface_golden.txt`. A new action also fails it; see [add an action](../contributor/add-an-action.md).
