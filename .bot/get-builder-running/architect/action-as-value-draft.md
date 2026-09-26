# Action as a property value — DRAFT, parked

With Ingi, 2026-09-25. **Parked** ("we might be going over too much"); not for coder. Picked up after the builder runs.

## Why

`if %file% exists` is `file.exists` + `condition.if`. Today the test action has to run in front of the `if` in the same step, so in `if A exists call X, else if B exists call Y` every branch's test runs up front. Ingi: "an action taking in an action as property would be very powerful."

## Settled with Ingi

1. **(B): the property's declared type decides.** A property declared `action` HOLDS the action (the callbacks today: `event.on Goal`, `channel.set Goal`, `environment.run Action`, `http.* OnProgress/OnStream`, `llm.query OnToolCall/OnValidateResponse/OnStream`). Any other property given an action RUNS it and gets its result. Nothing new is declared in C#: a property says only what it needs (`file/exists.cs:19` `data.@this<path> Path`), where the value comes from is the `.pr`'s business, as with `%var%` today (`type/item/source.cs:120-132`).
2. **It runs during execution, lazily:** when the handler asks for the property's value, and only if it asks. Never at `.pr` load. An `elseif` never reached never runs its action.
3. **The LLM is taught one general rule** in `Properties.llm`: a property can take an action instead of a value; its result is the value. The exception is taught by type: a `variable` property (a name to write to) cannot take one.
4. **Truthiness belongs to the type.** Each type's description carries one line on what true means for it (path: exists; bool: itself; number: not zero; text/list: not empty). The LLM sees it when `condition.if` is on the menu. The value must be typed right: `%file%` as `path` is existence, as `text` only "not empty".
5. **`Operator` becomes optional:** `if` with only `Left` tests Left's truthiness.
6. **Any action can be a value, side effects included** (Ingi: "`if (file.delete x)` should work fine"). No list of "question" actions; its result is the value.

## Sketch (NEW, not traced)

```csharp
// goal/step/action/this.cs — NEW: an action's value is its result, unless the property asking holds actions
public override async ValueTask<item.@this> Value(data.@this data)
{
    if (/* the asking property is declared `action` — TO TRACE */) return this;
    var result = await Run(data.Context);             // Run exists: goal/step/action/this.cs:154
    if (!result.Success) { data.Fail(result.Error!); return Absent; }
    return await result.Value();
}
```

```json
{"name": "Left", "type": {"name": "action"},
 "value": {"module": "file", "name": "exists",
           "property": [{"name": "Path", "type": {"name": "path"}, "value": "%file%"}]}}
```

## Added 2026-09-26 (builder-formal, unparked for design)

- **Ingi: a slot can name the modules whose actions it takes:** "can't value be data<item|math>, it tells it can call an action in the math module", then "it can take both item|math|file….". First case: `set %total% = %total% + %item%` → `variable.set(Name=%total%, Value=math.add(A=%total%, B=%item%))`, the sum stored (AddItem's arithmetic waits on this).
- **Ingi: `- run %action%`**, a plang action that runs an action held as a value (the explicit door; `action.Run` already exists). Its own property is declared `action`, so by rule 1 it holds the action, and its Run runs it. A polymorphic forwarder: bare `Task<Data>`.
- **Found building the builder:** in a Value or a step's first `if`, the held form and the sequence form (`A; variable.set(Value=%!data%)`) are the same program, so the builder can save one form. In a conditional position (an elseif's Left) only the lazy run is correct.

**To settle tomorrow:**
1. Is `item|action<math>|action<file>` a **restriction** (only the listed modules' actions are accepted) or **teaching** (rule 6 stands, any action runs, and the signature shows nano the likely ones)? Rule 1 said "nothing new is declared in C#"; a union is a new declaration.
2. How does a variable come to HOLD an action for `run %action%`? variable.set's Value isn't declared `action`, so by rule 1 an action given to it RUNS and stores the result. Holding needs its own door (a goal parameter typed action? `set %a% as action = …`?).
3. Names: the module and action for `run` (e.g. `action.run`, the partner of `goal.call`).
4. **Ingi: "I would like him call it from a goal".** The runtime runs each action by calling a plang goal (e.g. `/system/action/Run.goal`: `- run %action%`), so action execution is plang. It needs a base case: `run` itself dispatches straight to its handler, or it calls the goal forever. The cost: about 4 frames per action instead of 1. It overlaps with the BeforeAction/AfterAction events (`action/this.cs:189`, `:213`; mock.intercept and event.skipAction live there). The deciding question: what can the goal do that an event can't? If it's replacing how actions run, that's a reason; if it's observing or wrapping, events already do that.

## Open

1. How the asking Data knows its declared property type (callbacks must get the action, not its result).
2. The result is never cached: an action runs on every read.
3. Where each type's truthiness line lives and how it reaches the LLM (depends on #22: stage 3 sees no notes today).
4. **Properties typed `action` never reach the LLM today** (found in the child eval, F3): building `%action.Property%`, `type/property/list/this.cs:82` drops every property whose type is `clr`, `goal`, `step`, `action` or `modifier`, so `channel.set`'s `Goal` is never listed under `channel.set` in the stage-3 user message. Action-as-value must let action-typed properties through (and teach them).
