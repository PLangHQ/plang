# coder v13 — stage 10e: one form of computed value

Contract: start.goal Stage10e ("%Now% and %!event% are computed on each read, one way"), decisions 148 and 149.

## What is there
`data.DynamicData` (data/this.cs:820) has two constructors, which is the fork 148 ruled out:
- a value factory (`Func<object?>`, plus an optional declared type), lifted per read by `type.item.computed` with the asker's context: Now, NowUtc, GUID, !app, MyIdentity, Identity, !context, !variables, !callStack, !trace, !channels, !goal, !step, !error, !data, !test;
- a Data form (`Func<data?>`) for `!event` only, which forwards the found Data's Properties (`_found?.Invoke()`).

## Shape
- One constructor: `DynamicData(string name, Func<actor.context.@this, data.@this?> found, actor.context.@this context, type? declared = null)`. A Data found per read, built with the asker's context, which keeps the lift `computed` does today. It answers the whole Data it stands for: the value (`Peek`) and the properties. None found is the null value and an empty bag.
- `computed` takes `Func<context, item?>`; `Compute(ctx)` answers the found item, or the null value.
- Every site builds its Data: `ctx => ctx.Ok(DateTimeOffset.Now)`, `ctx => ctx.Ok(app)`, `ctx => CallStack.Event`. The declared type stays an optional label (Now is a datetime before it's computed), not a second form.

## Pins
- `%!event%` keeps its behaviour (the existing 8f pins).
- `%Now%` is fresh per read and a copy captures the moment.
- A value site's properties are its found Data's.
- There is one constructor (the class's public constructors are exactly one).
