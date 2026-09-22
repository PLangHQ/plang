# Step B — the `recovery` slot is written, but the modifier can't reach it without an OBP violation

**Branch:** `goal-graph-singular` · **Author:** coder · **Blocks:** Step B, and stage 3 of the new builder

## What is done

Per your ruling, `recovery` as a structural slot on the action base, documented like `Child`:

```csharp
// goal/step/action/this.cs
/// <summary>The actions that run when this one's recovery fires — the body of an `on error`
/// clause. Empty on every action but <c>error.handle</c>. A structural slot like Modifier and
/// Child, not a parameter value: an action is program, not data, so it is read at load through
/// the same door as any other action and is born holding the enclosing step.</summary>
[Store, Debug, Default]
public global::app.goal.step.action.list.@this Recovery { get; set; } = new();
```

Reader (recovery actions born holding the SAME step as the action they recover) and writer both
done. What remains is `error.handle` reading the slot instead of its `Action` parameter.

## The blocker

`error.handle` cannot see its own declaration:

```csharp
// module/IModifier.cs
Func<Task<data.@this>> Wrap(Func<Task<data.@this>> next, actor.context.@this context);
```

It is handed `next` and `context` and nothing else. The recovery actions rode as a PARAMETER
precisely because a parameter was the only thing it could reach. The slot fixes the wire shape but
the handler still can't get at it.

## The obvious fix is not OBP, and I am not taking it

```csharp
Wrap(Func<Task<data.@this>> next, actor.context.@this context, modifier.@this modifier);
// modifier/this.cs:53  ->  return (mod.Wrap(inner, context, this), null);
```

Handing an object its own declaration is shuttling data across to where the behaviour happens to
live. That is the **one action, two objects** problem you named in Addendum 3 — `modifier.@this`
holds the structure, `Handle` holds the behaviour — and this parameter widens the seam rather than
closing it. Three implementors, two of which would ignore the argument: a signature change that
exists only so one implementor can reach round to its own data.

## What I think the shape is

Ask who owns "run the recovery actions". Not `Handle`. `Recovery` is an `action.list`, and an
`action.list` runs itself. `Handle`'s actual job is the **verdict**: does this error match my
filters, and given that, do I retry, recover, or ignore.

So the entity runs its own slot and asks the handler only for the decision:

```csharp
// modifier/this.cs — the entity already holds Recovery and already resolves the handler
public Func<Task<data.@this>> Wrap(Func<Task<data.@this>> next, context)
{
    return async () =>
    {
        var result = await next();
        if (result.Success) return result;

        var verdict = await mod.Verdict(result.Error, context);   // the handler decides
        return verdict switch
        {
            Recover  => await Recovery.Run(context),              // the SLOT runs itself
            Retry    => await Retry(next, context),
            Ignore   => context.Ok(),
            _        => result,
        };
    };
}
```

`IModifier` keeps a two-argument surface; what changes is that one of its members answers a
question instead of performing the work. `cache.wrap` and `timeout.after` do not have a recovery
body at all, so this does not distort them — though it does mean `IModifier` may want splitting
(a modifier that wraps vs a modifier that decides), which is the part I want your read on.

I have NOT written this. Three questions:

1. Is the verdict split the shape you want, or is there a reading where `Handle` legitimately owns
   running the chain and the interface change is fine?
2. If verdict: does `Retry` stay on the entity too (it also only needs `next`), leaving the handler
   purely a policy object with no execution at all?
3. Does this want to wait for the one-action-one-object cleanup you queued (the record grows its
   own run surface and `action.@this` stops re-describing it)? If that lands first, the question
   dissolves — there is one object and it has both the slot and the behaviour.

## Why it is urgent now

Beyond the birth-fact work, this is the ceiling on the new builder's third stage. Measuring a
typesafe-decider + llm pipeline against the builder's own goals: stages 1 and 2 (which modules,
which action) are at 97.7% on action chains; stage 3 (fill the properties) sits at ~94% on values
the step actually states. Every remaining chain error is one shape:

```
call Plan, on error call HandleBuildFailure
   want: goal.call(Plan) | error.handle | goal.call(HandleBuildFailure)
```

Two `goal.call`s in a flat chain, and nothing in the shape says the second one runs INSIDE the
handler. Three different promptings each traded one error for another — over-duplicating the
wrapper, then dropping the recovery, then duplicating the main call. It is not a prompt problem:
the information is absent from the shape. With `recovery` as a slot the chain is
`goal.call | error.handle{…}` and there is nothing left to guess.

## Also found while measuring, for the record

Four `.pr` files were silently dropping arguments the developer wrote (`bc12da1b1`):

```
build.pr      call EmitBuildEvent kind="build-path", path=%path%     lost path
build.pr      call EmitBuildEvent kind="goals-found", goals=%goals%  lost goals
start.pr      call …/EmitBuildEvent kind="goalHeader", goal=%goal%   lost goal
buildgoal.pr  call BuildGoal/Start goal=%goal%                       lost goal
```

`BuildGoal/Start` has been called with no goal at all. Fixed by hand with Ingi's go-ahead; a scan
of every `.pr` under `os/` and `Tests/` finds no others.
