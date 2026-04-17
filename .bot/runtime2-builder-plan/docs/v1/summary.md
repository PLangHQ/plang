# Docs v1 Summary — runtime2-builder-plan

## What this is

Documentation review and gap-filling for the runtime2-builder-plan branch. This branch has four major changes: action results flow via `%__data__%` (not Return), condition.if orchestrates if/elseif/else branches, Data.Compare for structural diffs, and security hardening across HTTP/JSON/Ed25519/Variables.

## What was done

### Architecture docs updated (5 files)

- **goals-steps.md**: Removed `Return` from Action properties table. Updated action run sequence to show `%__data__%` storage. Added `Disabled` to Step properties. Updated execution flow diagram.
- **architecture.md**: Updated dispatch kernel code to show `%__data__%` assignment. Removed `Return` from Action entity description. Updated sub-step control to reference `step.Disabled`.
- **execution-flow.md**: Updated User context action flow from "Return variables Put()" to "%__data__% stored".
- **good_to_know.md**: Rewrote sub-step execution section to explain context-backed `step.Disabled` mechanism. Added new "Condition Orchestration" section explaining if/elseif/else in one step. Added "Data.Compare" section. Added "Security Hardening" table with all defense-in-depth limits.
- **variables.md**: Added `%__data__%` section explaining the action result variable and its role in data flow.

### XML doc comments (3 files)

- **JsonStringNavigator.cs**: Docs on `MaxElementCount` (object amplification) and `MaxDepth` (defense-in-depth).
- **Data/this.cs**: Docs on `Clone()` and `ShallowClone()` explaining why events are intentionally not copied (auditor finding #6).
- **Variables/this.cs**: Documented depth (100) and breadth (100K) guards on `ResolveDeep`.

## Code example — the pattern of doc changes

Before (architecture.md dispatch kernel):
```csharp
if (action.Return != null)
    foreach (var returnVar in action.Return)
        context.Variables.Put(result with Name = returnVar.Name);
```

After:
```csharp
// Result stored as %__data__% — available to the next action or variable.set
result.Name = "__data__";
context.Variables.Put(result);
```
