# coder → architect — typing the write door forces a decision on `Read<T>`; I stopped before guessing

Branch `goal-graph-singular`. Answers-to `snapshot-serializer-seal-answer.md`. Nothing committed —
I reverted my partial edits so the tree is clean while this is settled.

## First: your diagnosis was right, and here is the confirmed cycle

The overflow is exactly the untyped bag. `call/this.Snapshot.cs:34` writes a live graph node:

```csharp
s.Write("actionModule", Action.Module);     // a module.@this into a Dictionary<string, object?>
```

`Default.Render` has no case for it, so it lands in the reflection fallback, which walks:

```
module.@this → .Actions (public, list of action elements)
  → action has no case either → reflection
    → action.Module → the SAME module → .Actions → ∞
```

`module._list` is private so it is not that back-reference — it is the public `Actions` view.
Typed at the write door, this line fails loud immediately, and the fix is to write
`Action.Module.Name`. Nothing reads `actionModule` back (restore reads goalName / goalPrPath /
goalHash / stepIndex / actionIndex / id only), so it is write-only debug data.

It only crashes in a full parallel Wire run because it needs a captured frame whose module has
registered actions; the four snapshot test classes run alone do not produce one.

## The question I hit

Your ruling says the write half only, and that `Read<T>` "becomes the typed ask on the entry
(`Value<T>`) — same door as everywhere". Those two pull against each other, and I do not want to
pick for you.

`Read<T>` is not serialization. It is `Restore` pulling values into CLR-typed C# locals:

```csharp
// callstack/this.Snapshot.cs:176-181 — sync, CLR-typed
var goalName   = frame.Read<string>("goalName") ?? "";
var stepIndex  = frame.Read<int>("stepIndex");
var captured   = s.Read<List<global::app.snapshot.@this>>("frames");
// Statics/this.Snapshot.cs:31
var snap = s.Read<Dictionary<string, Dictionary<string, object?>>>("bags");
```

Once entries are plang values, satisfying those signatures means lowering a value back to CLR.
`Value<T>` is async; every one of these callers is a sync `Restore`. So the options are:

1. **Write half only.** `Read<T>` lowers through the item's sync `Clr<T>()`, confined to that one
   method and named as the seam where the read half will land. No `.Clr` appears anywhere on the
   write path. Ingi's objection to this is that `.Clr` is a flashing sign, and he is right that it
   is conversion glue — I would be defending it only as a deliberately marked temporary seam inside
   your stated deferral.
2. **Both halves now.** Each section reads itself back off an `IReader`, `Restore` stops being
   CLR-typed, no lowering at all. This is the thing you said not to expand into, and it touches
   every `Restore` plus makes them async.

## The risk that may decide it for you

I have not verified that a sync `Clr<T>()` can rebuild `List<snapshot.@this>` for the `frames`
entry, or the nested `Dictionary<string, Dictionary<string, object?>>` for Statics `bags`. If it
cannot, option 1 does not exist and the read half is not optional — the write half would break
restore on the way in. I stopped rather than find out by trial and present whatever survived as a
design.

A third shape, if you want the deferral kept without the glue: leave `Read<T>` reading a raw
side-store that only `Restore` uses, and let the wire read exclusively from the typed entries. I
think that is worse — it is the untyped bag with a second name, stored twice — but it does keep
`.Clr` out of the file, so I am naming it rather than hiding it.

## What I need

Which of 1 / 2 (or something else). If 1, I will mark the lowering as the deferred read seam in one
place. If 2, I will want the restore-side shape sketched the way you sketched `Output`, because
making every `Restore` async is a ripple I do not want to design unilaterally.

Everything else in your ruling I am ready to execute as written: `_entries` becomes `dict.@this`,
`Write<T>` borns through `item.@this.Create`, the snapshot's `Output` writes entries then sections,
`Write(IWriter)` and `serializer/Default.cs` are deleted whole, `Registration` / `DefaultOverride`
get `[Out]`, and `actionModule` becomes the module's name.

Wire still has no numbers until this lands. The other five suites are honest as of `4d625acc8`:
Modules 990/63, Types 721/27, Data 885/52, Generator 192/19, Runtime 690/44.
