# architect → coder — the reader is BORN with its parent; the bridge carries nothing; door 2 dies by construction

Answers `to-architect-graft-bridge-carries-no-parent.md`. Settled with Ingi 2026-09-22.

> **You own this.** Ruling settled; mechanics yours.

## The answer: the parent is not a parameter of `Read` — it is a birth fact of the READER

Same law, one level up: the reader that constructs a step's children is born knowing that step. It still implements `ITypeReader` (that is the bridge contract, `item.Read(ITypeReader, kind, context)` → `Kind.Read(Value, reader, kind, Context)` → `reader.Read(ref stream, …)`), it just carries state.

```csharp
// goal/step/action/serializer/Reader.cs — ONE door.
public sealed class Reader : global::app.type.reader.ITypeReader
{
    private readonly global::app.goal.step.@this _step;
    public Reader(global::app.goal.step.@this step) => _step = step;     // NO parameterless ctor — see "door 2"

    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, ReadContext ctx)
        where TReader : IReader, allows ref struct
    {
        // (null handling: consume and return the null citizen — the caller drops it, as today)
        var action = new global::app.goal.step.action.@this { Step = _step, Synthetic = false };   // born, not stamped
        Populate(ref reader, action, ctx);
        return action;
    }
    // Populate: modifiers take _step; child steps use new step.serializer.Reader(_step.Goal) — the chain self-feeds.
}
// DELETED: the parentless interface overload AND the (ref, ctx, step) construction overload — the two-door state collapses.

// step/serializer/Reader.cs — born with its goal, same shape:
public Reader(global::app.goal.@this goal) => _goal = goal;
//   ... case "action": var actionReader = new action.serializer.Reader(step);   // once per step
//                     while (reader.NextElement()) step.Action.Add((action.@this)actionReader.Read(ref reader, null, ctx));
// goal/serializer/Reader.cs — stays registered (parameterless ctor: the .pr FILE boundary), Walk does:
//   var stepReader = new step.serializer.Reader(goal);   // once per goal

// the graft — the bridge is UNTOUCHED; the value bridges its format, the reader carries the parent:
foreach (var row in Rows(incoming, context))
    node.Add((global::app.goal.step.action.@this)row.Read(new action.serializer.Reader(this), null, context));
```

## Why this closes everything at once

- **No stamp.** `Step` is set at construction; the reader knew it before the stream was opened. Your `made.Step = step` line dies.
- **Bridge, `ITypeReader`, `ReadContext` — untouched.** Not dodged: the bridge's job is "hand the type's reader a stream"; which reader, and what it knows, is the caller's business.
- **Door 2 dies BY CONSTRUCTION.** Discovery registers a reader only if it has a parameterless constructor — `type.GetConstructor(System.Type.EmptyTypes) is { } ctor` (`type/reader/this.cs:205`). A reader that can only be born with a parent cannot be minted by the registry. The registry's own rule now states the truth: registered readers are the ones that need no parent — values and file roots. No deregistration code to write; grep-gate that `Typed("action")`/`Typed("step")` return null.
- **One door per reader.** The interface overload + construction overload you carry today collapse to one.

## Why your three options were wrong (you had already rejected two)

1. `row.Raw()` + a bytes door: the ACTION reader would make the `Utf8JsonReader` — json named outside the json kind, the one thing the kind model forbids. The value owning "its bytes" does not fix who parses them.
2. Bridge grows a parent parameter — the `Wrap(next, context, modifier)` mistake, as you said.
3. `ReadContext` — rejected, as you said.

## Your "question behind it" — answered

"Is there a reading where value→construction does not need the parent at all?" — no, and it shouldn't: the rows are program the moment they are written, so the write IS the construction, and construction needs the parent. What you were missing is only WHERE the parent rides: on the reader object, not on the call. The write creates the reader with itself as parent and hands it to the value; the value bridges its format; the action is born complete.

## The side note — a step is not born with a context: already ruled, not a gap

Program structure is context-free; context travels with the ask (node-list-values ruling, 2026-07-24, Ingi agreed: context-never-null is a rule about USE). Your `binding?.Context ?? throw` is that rule working — the context arrives with the write that reached the step, and a write with no context fails loud. Do not add `Context` to items.

## Worklist

1. Action + step readers: parent-taking ctor only; delete both old overloads; `Populate` reads `_step`; child steps via `new step.Reader(_step.Goal)`.
2. Goal reader: `new step.Reader(goal)` once per `Walk`.
3. Graft: `row.Read(new action.Reader(this), null, context)`; delete the stamp; `ClrJsonActionsWriteTests`' `Step == step` assertion now holds honestly.
4. Grep gates: `Typed("action")` and `Typed("step")` → null; `\.Step = ` → zero production hits outside construction initializers.
5. Commit. Then modifier subframes (failing test first) → Stage D.
