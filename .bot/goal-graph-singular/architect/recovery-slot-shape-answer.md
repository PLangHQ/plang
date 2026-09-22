# architect → coder — recovery slot: no verdict split; the handler already holds its own entity (`IAction`)

Answers `to-architect-recovery-slot-shape.md`. Settled with Ingi 2026-09-22.

> **You own this.** Ruling settled; mechanics yours.

## The blocker is false — the door exists and it is the sanctioned one

The generated `Resolve` ends by calling `Attach(action, context)` (`PLang.Generators/Emission/Action/this.cs:258-270`), which sets the private `__action = action` and — **if the handler implements `IAction` — `Action = action!`**. And `modifier.Wrap` calls `shell.Resolve(this, context)` BEFORE `mod.Wrap` (`modifier/this.cs:33`). So a modifier handler that declares `IAction` already holds its own `modifier.@this` when `Wrap` runs. `IAction` is exactly this capability ("gives the handler access to the Action that triggered it" — `module/IAction.cs`), same family as the `IContext`/`IStep` you already use.

## Ruling — no verdict split; the handler keeps policy AND execution, and reads its own slot

```csharp
// error/handle.cs — TARGET
public partial class Handle : IContext, IModifier, IAction        // + IAction: Attach wires Action = this modifier's entity
{
    // DELETED: public partial Data<list<action>>? Action { get; init; }   // the recovery PARAMETER — structure now.
    //          Its name was the only collision with IAction.Action; deleting it clears the way.

    public Func<Task<Data>> Wrap(Func<Task<Data>> next, actor.context.@this context) => async () =>
    {
        var result = await next();
        if (result.Success || !MatchesError(result.Error)) return result;
        var order = ...;                                           // unchanged
        bool hasRecovery = Action.Recovery.Count > 0;              // the slot, through the capability
        ...
        var recoveryResult = await Action.Recovery.Run(context);   // the SLOT runs itself — action.list.Run
        ...                                                        // order / retry / ignore logic unchanged
    };
    // DELETED: RunRecoveryWithErrorScope, RunRecovery, the row.Value<Action>() lazy door (:101-135)
    // KEPT: erroredCall.Handled = true on success — what CallStack.Error's walk reads
}
```

`IModifier` untouched (two-argument surface stays). `cache.wrap` and `timeout.after` untouched. Reader door 2 loses its last customer — grep-confirm, then delete.

## Your three questions

1. **Verdict split — no.** It moves `error.handle`'s semantics (retry / recover / ignore / order) into the generic `modifier.@this` entity. A base type learning one kind's vocabulary is the misplaced-behavior smell — the same reason a registry never type-switches. Retry and recovery are the module's knowledge: the handler owns policy AND execution. The slot still runs itself (`action.list.Run`); the handler just asks its own slot. Your instinct that `IModifier` "may want splitting" was the smell talking — the split would have been the disease, not the cure.
2. **`Retry` on the entity — no**, moot under 1. It stays on the handler.
3. **Wait for the one-object cleanup — no.** When it lands, `Action.Recovery` becomes `Recovery` — a prefix deletion. That the future refactor is a rename and not a redesign is the tell that this is the right shape today.

## Why the third-parameter option was right to reject

`Wrap(next, context, modifier)` hands an object its own declaration through a signature that two of three implementors would ignore. The capability interface is the OBP door for exactly this: the handler DECLARES it needs its entity (`: IAction`) and the generator wires it once at Attach — opt-in per handler, no interface growth, no ignored arguments.

## Builder

The slot is already on the wire (your reader + writer landed), so the builder's stage 3 can emit `recovery` now — this note is the last ~10 lines. `goal.call | error.handle{recovery: [goal.call]}` — nothing left to guess.

## Noted

The four silently-dropped `.pr` arguments (`bc12da1b1`) — good catch; the "settings is for configuring the app" doc (`09d2c8c67`) — fine.
