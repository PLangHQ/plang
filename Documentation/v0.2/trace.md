# Trace

`Context.Trace` is a per-execution identity used to correlate diagnostic output
(trace JSON files, LLM debug files) with one run of an operation — typically a
build invocation, but conceptually any single PLang execution.

## Ownership

C# owns Trace. It is **not** a PLang variable that scripts hand-roll.

- Created by `App.Actor.Context.Trace.@this` in its constructor.
- Owned by `App.Actor.Context.@this` as the property `Trace`.
- One Trace per Context instance. Sub-goal calls share the parent Context, and
  therefore share its Trace. Forking a new Context (e.g. another actor) creates
  a new Trace.

The previous PLang-side line `set %traceId% = %Now.Ticks%_%goal.Name%` is gone
— the builder now reads `%!trace.id%` instead.

## Shape

```csharp
public sealed class @this    // App.Actor.Context.Trace.@this
{
    public string Id { get; }                 // {ticks}_{guid8}
    public DateTimeOffset Started { get; }    // when Context was constructed
}
```

`Id` is sortable + unique:
- Leading ticks make file listings sort by build start time.
- Trailing 8-char guid prevents collisions when two Contexts are constructed in
  the same tick.

Example: `639128355356621271_6f1c8e2a`.

## Access from PLang

Registered alongside `!app`, `!fileSystem`, `!callStack` etc. in
`Context.RegisterContextVariables()`:

```csharp
vars.Set(new Data.DynamicData("!trace", () => Trace));
```

In `.goal` source:

```plang
- save %trace% to file '/.build/traces/%!trace.id%/%goal.Name%.json'
```

`%!trace.id%` resolves to `Trace.Id`. Case-insensitive (`%!trace.Id%` works
too).

## Trace file layout

One folder per Context (per build invocation), one file per goal built within
it:

```
.build/
└── traces/
    └── {trace.id}/
        ├── manifest.json           ← list of goal names built in this run
        ├── Start.json              ← per-goal trace JSON (BuildGoal output)
        ├── DoStuff.json
        └── llm/                    ← optional, when --debug llm.output=file
            ├── Start_goal.txt      ← raw LLM exchange for the goal-level call
            └── Start_3.txt         ← per-step LLM exchange (BuildStep)
```

The folder layout makes it cheap to delete one build's traces (`rm -rf
.build/traces/{trace.id}/`) and groups everything from one execution together.

## Why on Context, not Debug

Trace files are written by the builder PLang **regardless of whether
`--debug` is on**. Trace identity exists independently of debug. Debug merely
*consumes* it for filename composition.

If you need a runtime diagnostic id (correlating logs to a build), use
`%!trace.id%`. If you need a per-LLM-call id, see [debug.md](debug.md) — the
LLM debug feature uses `Trace.Id` as the folder, then disambiguates calls by
goal/step within that folder.

## Extension

`Trace.@this` is a class so it can grow. Future fields could include:

- `ParentId` — for distributed/nested trace correlation.
- `Tags` — caller-supplied labels (`{"build": "incremental"}`).
- `Spans` — nested timing entries inside one Trace.

When extending, only add what's used by a real consumer. Trace is a single
source of truth — it should not accumulate fields that nobody reads.
