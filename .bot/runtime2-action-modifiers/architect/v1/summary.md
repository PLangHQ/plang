# v1 Summary — Action Modifiers Design

## What this is

A design to eliminate special-cased step-level properties (`OnError`, `Cache`, `Timeout`) by promoting them to regular per-action modifier actions. Today these require special handling at every layer (model, builder prompt, LLM schema, merge, runtime). The new design treats them as regular `module.action` records with a `[Modifier]` attribute, giving per-action precision and zero-cost extensibility for future modifiers.

## What was done

- Designed the full pipeline: LLM outputs flat actions → builder restructures on save (grouping modifiers onto their preceding executable action, sorting by `Order`) → .pr file stores pre-grouped structure → runtime folds modifiers via `IModifier.Wrap()` right-to-left
- Defined `[Modifier(Order)]` attribute and `IModifier` interface as the only contracts
- Established nesting order: async(0) > timeout(1) > cache(2) > error(3) > action
- Specified .pr schema with `modifiers` array on each action
- Outlined 4-phase migration path from current step-level properties to action-level modifiers
- Identified future modifiers: `async.fire`, `parallel.set`, `throttle.set`

## Code example

Runtime fold — the entire modifier execution model:

```csharp
Func<Task<Data>> execute = () => Dispatch(context);
for (int i = Modifiers.Count - 1; i >= 0; i--)
    execute = Modifiers[i].Wrap(execute, context);
return await execute();
```

## Status

Design and implementation roadmap complete and approved. `roadmap.md` contains the 4-phase implementation spec with file paths, code sketches, test requirements, and coder checklist. Ready for test-designer to create test suites, then coder to implement Phase 1.
