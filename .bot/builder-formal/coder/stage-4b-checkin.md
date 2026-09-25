# Stage 4b check-in — the `.pr` envelope

Branch `builder-formal`. Traced, nothing built. **One blocker first (Q1), then the shape.**

## Q1 — the blocker: 583 `.pr` files in the old format

There are **583 `.pr` files** under `Tests/**/.build/` and `os/**/.build/`, all JSON action rows.
- If 4b refuses the old format (`PrFormatOutdated`), every plang test and the builder's own goals fail to load until each `.pr` is rebuilt.
- The plang builder can't rebuild them yet: that's 4c/4d, and `get-builder-running` isn't done.
- A bulk conversion of the 583 files is a tool rewriting `.pr`s. That's what Ingi's rule forbids ("never edit `.pr`"; 4d even says "no shell rewrite of `.pr`").

Options:
- **(a) Read both, write one** (my recommendation). The step reader reads `"action"` as a formal string (the new envelope). While it's still an array of JSON rows, it's read by the old rows branch, marked for deletion in 4d, once every `.pr` has been rebuilt by the builder itself. Writes are formal only, so the builder writes nothing but the new envelope. The refusal of the old format (`PrFormatOutdated`) moves to 4d.
  It is a second read path, deliberately and for a named time: the migration door, not a fork in behaviour. The loaded action is the same object either way.
- (b) A one-time, reviewed conversion (old reader → new writer) of all 583 files. That's against the rule unless you and Ingi say this is the reviewed install.
- (c) Refuse old `.pr` now, and accept that plang tests are red until 4d.

Everything below assumes (a); with (b), the old branch goes in the same commit as the conversion.

## The envelope

**The trace:**
- `goal/serializer/Reader.cs:45-92` reads the goal facts and each `step` through `step/serializer/Reader.cs`.
- That reader reads `index, text, lineNumber, indent, comment, action[], intent, source, waitForExecution` (`:39-53`).
- `step/this.Item.cs:72-92` writes the same keys; `"action"` is `action.list.Output`, the JSON array.

**The new shape** (a step):

```json
{"index": 3, "text": "if %errors% is empty", "lineNumber": 12, "indent": 0,
 "action": "condition.if(Left: item = %errors%, Operator: choice<operator> = \"isempty\")",
 "child": [ {"index": 4, "text": "write out \"No errors this week\"", "lineNumber": 13, "indent": 1,
             "action": "output.write(Data: text = \"No errors this week\")", "waitForExecution": true},
            {"index": 5, "text": "return", "lineNumber": 14, "indent": 1, "action": "goal.return()", "waitForExecution": true} ],
 "warning": [ {"key": "Unsure", "message": "step 3 uses …"} ],
 "waitForExecution": true}
```

- **Stays JSON:**
  - the goal facts (name, description, comment, path, hash, builderVersion, visibility, the flags, tag, child goals);
  - each step's `index, text, lineNumber, indent, comment, intent, source, waitForExecution`;
  - **new: `warning`** (the step's `Warning` list, `{key, message}`; written only when there are any);
  - **new: `child`**, the indented body (below).
- **`"action"` is one formal string**, written by `action.list.Output` through a `formal.Writer` and read at the step reader's `"action"` key through `new action.serializer.Formal(step).Read(text, ctx)`.
- **Indented body vs inline body**, told apart by structure, not a flag:
  - A condition's `Child` step whose `Indent` is deeper than its parent step's came from indentation (build.fold put it there). The step writes it in the envelope's `"child"`, a full step with its own index, text and line, and the formal has no `{ }`.
  - A `Child` step at the parent's own indent was born from `{ }`. The formal writes it inline.
  - On read, the envelope's `"child"` steps attach to the step's condition action that has no inline body: the lone `if` build.fold folded onto, the same rule as `step.list.Body`.
  - Your answer 3 (they never meet in the source) holds: the writer only has to tell them apart.
- **A step whose formal fails to read at load** is the reader perimeter, so it throws, like the other `.pr` reader failures: `AppException("…/start.pr, step 13: its action doesn't read — line 1, column 34: <reason>. Rebuild the goal.", "PrActionInvalid", 400)`, with `FixSuggestion` = the reason.
  - It differs from `PrFormatOutdated`: that one says the `.pr` is a different shape (made by an older builder: rebuild). `PrActionInvalid` says the shape is current but its formal is broken, which only a builder bug or a hand edit can cause.
  - The reader itself still returns an error (4a); the step reader converts it to the throw at this perimeter only.

## What dies (member by member)

**`goal/step/action/serializer/Reader.cs`** (the JSON rows reader):
- its `module / name / property / default / modifier / recovery / child` switch;
- `Property(raw)`, the `{name, type, value, properties}` row read;
- the `parameter(s)` → `PrFormatOutdated` branch.

With (a), all of it stays as the migration door until 4d, then the file goes. Its one other caller is `build.match`, which reads the LLM's JSON answer through it. That caller moves to `Formal.Read` in 4c, when the answer becomes formal.

**`goal/step/action/this.Item.cs` `Output`'s JSON branch** (`writer.BeginObject … "module", "name", "property", "default", "modifier", "child", "recovery" … EndObject`) goes:
- **the `.pr` (Store)** writes the formal string, through the step;
- **the wire, snapshots, Debug:**
  - the Debug view stays on the reflection kind, as today;
  - a snapshot stores positions only (`callstack/call/this.Snapshot.cs:30-39`: goalPrPath, stepIndex, actionIndex, module/name), never an action graph;
  - an action can't be read off the wire as a Data value today (its reader is born with a step; there's no parentless reader in the registry).
  
  So **nothing else needs the JSON shape**. For any non-formal writer, an action writes its formal as one string (`writer.String(formal)`), so a stray write is still one representation, readable by `Formal`.

**`property.Output`'s JSON row** (`{name, type, value, properties?}`) is used only for action rows, so it goes with them. Its `properties` sidecar, a value's `Properties` bag, has no formal form today. The golden goals have none; I'll check the 583 `.pr`s for one before deleting the branch.

**Tests with JSON action rows**:
- `MatchTests` (the answer JSON: moves with build.match in 4c);
- `GoalGraphRoundTripTests`, `ClrJsonActionsWriteTests`, `ActionNameWireReadTests`, `RenderStoreViewTests` (the JSON shape: rewritten to the envelope, or deleted where they test the shape itself);
- `DiscoverActionTests` (old-format stale detection: kept).

## step.Nest and modifier.list

**`step.Nest`** (`goal/step/this.cs:66-100`) turns a flat list (a modifier written after its action, the LLM's JSON answer) into modifiers on the preceding action, then sorts them by `Position` (outermost first). Its one caller is `goal.NestRecursive`, called by the build before saving (`module/action/build/code/Default.cs:219`).
- With a formal answer (4c), the parser nests directly (4a), so `Nest` and its sort have nothing to do. They go when build.match reads formal (4c).
- The sort was the only place `[Modifier(Order)]` shaped runtime order. After it, the written nesting is the order, and the `Position` numbers stay only as what the prompt teaches.

**`modifier.list`**: `action.Modifier` is a naked `List<modifier.@this>` today.
- Its wrap loop (right-to-left, adjacent catch clauses grouped into one try/catch) lives in `action.RunAsync` (`action/this.cs:195-219`).
- `Catch` (`:238-260`) is on the action.

The refactor (queued since get-builder-running, lost in the crash) is a new `goal/step/action/modifier/list/this.cs`:

```csharp
public sealed class @this : IReadOnlyList<modifier.@this>     // action.Modifier
{
    private readonly List<modifier.@this> _items = new();
    public void Add(modifier.@this m);                          // the written order (the reader: outermost first)
    public void Wrap(… ) / Task<(Func<Task<Data>>? Wrapped, Error?)> Around(Func<Task<Data>> inner, context)
                                                                // the fold: right-to-left, catch clauses grouped (moved from RunAsync + Catch)
}
```

The action asks `Modifier.Around(execute, context)`; the list owns the composition and the grouping. There's no `Sort` on it: the order is the written one. (While `Nest` lives, until 4c, it sorts its own flat input before handing it over.) The `ModifierAttribute` comment fix goes in with it (error.handle=1, cache.wrap=2, timeout.after=3).

## Tests I'll add

1. **Envelope round trip:** each golden goal written as a `.pr` and read back is equal. The written `.pr` text is compared byte for byte, and read → write gives the same bytes.
2. **Through the engine:**
   - a small goal saved as a formal `.pr` (`set %n% = 5`; `if %n% > 3` with an indented `write out "big"`; `call Done, on error call Fix`), loaded through the real load path (`RealGoalLoad.ViaChannel`) and run;
   - asserted on its output and variables. It goes through the `.pr`, not around it.
3. **An old-format `.pr`:** read through the migration door and equal to its envelope form (with (a)); or refused as `PrFormatOutdated` (with (c)).
4. **A broken formal string** in an otherwise current `.pr` → `PrActionInvalid`, naming the step and the position.
5. **modifier.list:** the existing composition tests (`GroupModifiersTests`, the catch-grouping tests) move onto it unchanged.

## Your calls

1. **Q1: (a), (b) or (c).** This decides the rest.
2. Indented vs inline body told apart by `Indent` (deeper than the parent step → envelope `child`). OK?
3. `PrActionInvalid` as the load-perimeter key. OK?
4. An action writes its formal as one string for any non-formal writer. OK, or should a non-formal write of an action be an error?
5. build.match reading formal, and `Nest`'s deletion, land in 4c, not 4b. OK?
