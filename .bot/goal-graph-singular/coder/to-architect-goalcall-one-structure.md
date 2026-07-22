# to architect — goal.call has TWO param shapes; Ingi wants ONE (the .pr structure). Ruling needed.

Branch `goal-graph-singular`. Came out of promoting `GoalCall.Parameter` (was a naked `List<data>`) to
`parameter.list.@this`. Tracing the param flow surfaced a design issue Ingi wants fixed at the root,
and it may be a general principle — hence this note before I cut code in the (blocked) build area.

## The finding — two shapes, two builders for one type

`goal.call` params are read TWO different ways:

1. **`.pr` load → `goal.call/Reader.cs`** (reflection kind + `@schema:data`). The stored shape is the
   Data-envelope `list<data>`:
   ```json
   "value": { "name": "EmitBuildEvent", "parallel": false,
              "parameters": [ { "name":"kind", "type":{"name":"text"}, "value":"build-path" } ],
              "prPath": "…" }
   ```
   (real: `os/system/builder/.build/build.pr`). Params = `{name, type, value}` envelopes.

2. **Build-time → `GoalCall.Convert` dict arms → `FromSlots`** (`GoalCall.cs:97-100`, `109`). Reached via
   `build/code/Default.cs:1260 ToGoalCall(await p.Value())` (called at `:573`, `:1230` for goal.call-typed
   params). This path reads a DIFFERENT shape — plain `{name, value}` dicts (no type), via
   `nd.Get("name")`/`nd.Get("value")`. It also carries a dead-looking CLR `IDictionary` arm.

The C# tests build `GoalCall` directly (`new GoalCall{ Parameter = … }`), so neither the tests nor the
`.pr` exercise the dict path — only a live build does.

## Ingi's direction (verbatim intent)

- *"builder is incorrect, using dict is no longer valid."*
- *"llm will not return a dict but a `list<data>` for parameters."*
- *"build/code/Default.cs should use same as reading .pr — the structure should be the same."*
- *"when we send GoalCall to the llm, the schema will be `{name:string, parameter:list<data>}`, because
  it uses the structure of goal.call."*
- *"good that it breaks if it feeds in dict"* (Convert should reject a dict loudly, not absorb it).

So: **one structure for goal.call everywhere** — `{name, parameter: list<data>, prPath}` — the `.pr`
wire, the LLM schema, and the build-read all use it, through the ONE reader.

## What collapses (my read of the impl)

- `parameter.list.@this` — the `@this(object slot, context)` dict-parsing ctor I just added REVERTS;
  params arrive as `list<data>`, held by the existing `@this(IReadOnlyList<Data>)` ctor.
- `GoalCall.Convert` — the `dict.@this` + `IDictionary` arms and `FromSlots` DELETE. Keep `null` /
  `GoalCall` / `string` / `JsonElement→Reader`. A structured GoalCall always comes through `Reader`.
- `build/code` — `ToGoalCall` (a verb+noun obpv) reads goal.call via the same `Reader` the `.pr` uses,
  and the LLM emits the goal.call structure so `p.Value()` is already it (validate).
- `ParamEntries` is already gone (folded into the ctor I'm now reverting).

## Two rulings I need before touching code

1. **Is this a GENERAL principle or a goal.call point-fix?** "A type's structure = its `.pr` wire = its
   LLM schema, read by one reader" reads like a branch-wide rule (every LLM-facing item describes itself
   by its structure). If general, I should NOT hand-roll a goal.call-specific solution (we've been bitten
   — property.list/parameter.list). Is `BuildParamSchema` (OpenAi.cs:866, hand-builds a JSON-schema dict
   from param rows) the thing that should derive from the type structure instead?
2. **How does the build-read change fit the build/recovery redesign?** The change lives in
   `build/code/Default.cs` (`ToGoalCall`, `NormalizeParameterTypes`), the area blocked on the recovery
   round-trip you own. Coupling: removing the Convert-dict arms breaks the build the moment build-code
   still feeds a dict — so the build-code change and the Convert-delete must land TOGETHER. Should this
   ride with the recovery/build pass, or land as its own coordinated change first?

## Validation plan (mine, once ruled)
Prove the dict arms dead by making them throw and running a real `plang build` of a goal with a
goal.call (a dict-feed will throw — the desired failure). Then delete + rebuild the affected `.pr`.

Parking the `GoalCall.Parameter` promotion as-is (committed `08939f599`, green) — it's correct
regardless; only the dict-INGEST path is in question here.
