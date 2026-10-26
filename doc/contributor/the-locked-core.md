# The locked core

For someone changing the runtime. The question this page answers: what may I not change on my own judgement, and how do I ask?

## What is locked

The core's surface is the load-bearing shape the rest of the system is built on. Its surface does not change without the project owner's say-so. The test `SurfaceTests.TheCoresSurface_IsThePinnedOne` (`PLang.Tests/Wire/App/Surface/SurfaceTests.cs:131`) pins it. It reads, from the built assembly, every public type in these namespaces and each type's own public and protected members with their plang marks (`SurfaceTests.cs:12-18`):

- exactly: `app` (the app's own types), `app.module`, `app.module.list` (the module registry);
- and everything below: `app.type` (the type system, the serializer and format, the core value types, variable memory), `app.data`, `app.channel`, `app.call`, `app.event`.

It also pins what a plang program sees: every type's members, and every module's actions with their properties (`SurfaceTests.cs:44-61`). The pin is `PLang.Tests/Wire/App/Surface/surface_golden.txt`, 4,569 lines, 142 of them `action …` entries.

## What you may do

- **Behaviour fixes and private refactors** inside a locked part.
- **New needs go to the edge**: a module owns its own types and declares its own events. Reach for a module-owned type or a module-declared event before the core.

## What is an addition

A member others call, a virtual, an attribute, an interface member, an `app` property, a new core type, a plang-visible member, a new action, a new property on an action. The surface test lists them all, because it pins the plang-visible actions too. A new action is therefore an addition: see [add an action](add-an-action.md).

## How you ask

An addition is a short proposal to the architect, who brings it to the project owner. Nothing is built before the yes. The proposal says what is added and why the edge cannot hold it.

## What fails if you do not

When the surface changes, the test fails with its own message:

```
the core's surface changed: an addition needs Ingi's yes, then AcceptTheSurface re-pins it and the commit names the decision
```

(`SurfaceTests.cs:136`). `AcceptTheSurface` (`SurfaceTests.cs:190-191`) is an `Explicit` test that rewrites the golden. Running it is the re-pin; the commit that holds the re-pinned golden names the decision that allowed it. An unapproved surface change cannot merge.

## How to see the failure yourself

Add a throwaway action, run `./dev.sh test SurfaceTests`, and it reports 1 failed of 3. This was run for [add an action](add-an-action.md) and then reverted.

## Read next

`Documentation/v0.2/type-system.md:31` lists the three locked-core additions one branch made, each with its approving decision and re-pin. It is the record of how an approved addition looks.
