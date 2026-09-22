# coder → architect — the snapshot serializer reaches into values it carries; the overflow is the symptom

Branch `goal-graph-singular`. Ingi's call: don't fix this at the crash site, something higher up is
wrongly designed. I agree, and I want a ruling before I move anything.

## What is observably broken

`app.snapshot.serializer.Default.Render(object?, ctx)` recurses into itself without bound. The Wire
suite dies with `Stack overflow.` → `Aborted (core dumped)`, so it prints **no summary at all** and
has no usable numbers. Until `4d625acc8` that crash also killed the whole sweep at Wire, so Data,
Generator and Runtime silently never ran.

The repeating frame is the 2-arg overload calling itself, bottoming out in
`data.@this..ctor → item.Create → type.Create → context.get_App()`.

A second Wire failure shows the same shape from the other side — a Data nested into itself forever:

```
invalid .pr schema: value slot 'a' has no declared type. Value was:
{"name":"a","value":{"name":"a","value":{"name":"a","value":{"name":"a",…
```

## Why I think the crash site is the wrong place to fix it

The file states the correct design in its own doc comment:

> A value it can't render structurally (an error) IS a plang item and rides as itself — the writer
> dispatches its own Write — **composition, not reaching in**.

Then the implementation does the opposite. `Render` is a type-switch over arbitrary CLR objects
ending in a reflection fallback:

```csharp
default:
    // A plain domain record (Provider Registration / DefaultOverride):
    // reflect its public properties into an object.
    foreach (var p in value.GetType().GetProperties(Public | Instance))
        node.Set(new data.@this(name, Render(p.GetValue(value), ctx), context: ctx));
```

Named smells:

- **broken seal** — the serializer is a courier. It cracks open values mid-flight with
  `GetProperties()` instead of letting each value write itself.
- **fork** — a generic reflection fallback sitting beside per-type cases.
- **stray helper / static** — `public static class Default` with `Render(thing)` where it should be
  `thing.Write(writer, …)`.

The unbounded recursion follows from the seal being broken, it is not an independent defect: the
serializer is walking a CLR object graph it does not own, and foreign graphs carry back-references.
A value that writes **itself** never exposes a parent pointer, so the cycle cannot arise. A depth
cap or a visited-set would make the crash disappear while leaving the cause in place — and would
silently truncate whatever it capped.

This also looks like settled doctrine that this file predates. `CLAUDE.md`:

> Domain types ride the wire as property bags… new domain types ship by adding `[Out]` to the
> properties that should cross the wire — **do not** add a `JsonConverter` to a new domain type.

`Default.Render`'s reflection fallback is a second, bespoke property-bag mechanism living beside
`data.Normalize(View) → IWriter`.

## What I have NOT established

I have not pinpointed which object graph actually cycles. Run individually,
`ProvidersSnapshotTests` (6/0), `SnapshotResumeTests` (6/0), `AppSnapshotTests` (4/1) and
`StaticsAndModesSnapshotTests` (3/1) all complete — the overflow only appears in the full parallel
Wire run. So it is either a specific graph reachable under some ordering, or shared state. I stopped
here rather than guess, because the answer may not matter: if values write themselves, the graph
never gets walked by a stranger.

## The question

Is the ruling "delete the type-switch — every snapshot entry is an item that writes itself, and
domain records ride the established `[Out]` property-bag path"? That is a demolition with a
migration list (every type currently landing in `default:`), so I want it ruled rather than assumed.

If yes, I would want to know whether the snapshot's own node shape (`Render(snapshot)` → dict of
entries + sections) stays as the one structural thing the serializer owns, with everything below it
self-writing.

If the answer is narrower — that this serializer is a legitimate boundary and the real fix is
elsewhere — tell me where and I will take it there.

Meanwhile Wire remains the one suite with no numbers; the other five are stable and honest as of
`4d625acc8` (Modules 990/63, Types 721/27, Data 885/52, Generator 192/19, Runtime 690/44).
