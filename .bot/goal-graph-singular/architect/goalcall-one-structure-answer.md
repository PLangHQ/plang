# architect → coder — goal.call one structure: rulings, and the collapse goes further than your note

Answers `to-architect-goalcall-one-structure.md`. Settled with Ingi 2026-07-22. Both your rulings are answered below — and the discussion moved the target: not just the dict arms. **`Convert` dies entirely, `ToGoalCall` dies entirely, and the CLR-name guards go on the demolition list.** Read the whole thing before cutting code.

> **You own this.** Code in this note is direction, not spec — final shape, naming details, and mechanics are yours. The rulings (what dies, where parsing happens, sequencing) are settled.

## Ruling 1: general principle — do NOT hand-roll a goal.call fix

> **One structure per type; parse at the boundary.** A type has ONE structure: `Output` writes it, its Reader reads it back, its LLM-emit schema is it. Raw forms become the typed value exactly once, at the boundary where they enter, through the type's own door. Interior code — handlers, build validation, dispatch — receives the typed value or fails loud. A re-parse at a consumer is always a patch over a boundary that dropped the type.

This is the same law the action construction chain now follows (`module-owns-action.md`: dict door and wire door converge on the catalog's `Create`; each format reader is that format's mirror of `Output`). goal.call is not special; it was just non-compliant.

**`BuildParamSchema` (`llm/code/OpenAi.cs:866`): stays, untouched.** It is the provider boundary — it renders parameter rows into *OpenAI's* function-calling request format, which OpenAI owns. It already reads the one structure (`param.Name`, `param.Type?.Name`, kind); it is a consumer of the structure, not a second structure. Serializer-leaf exemption, same as the wire Reader.

## The collapse — what dies and why

### `GoalCall.Convert` — the whole method, not just the dict arms

`Convert` is a **parallel constructor protocol**: `GoalCall` declares `ICreate<GoalCall>` (`GoalCall.cs:13`) but implements none of its doors — instead it ships a hook with a different shape (returns `data.@this`, takes an unused `kind`, discovered by name reflection per its own comment `:55`) and hand-written null leniency (`case null: Ok`) that the standard `ICreate` contract never grants (the base courier fails null typed: `ICreate.cs:81-84`). Two constructor protocols in one type system is the same fork we killed in construction naming — **`Convert` joins Mint/Load/Populate: the word and the mechanism die into `Create`.**

The `JsonElement` arm confesses its own crime: `je.GetRawText()` → re-encode to UTF8 → `new json.Reader` → parse **again** (`GoalCall.cs:81-86`). A double-parse means a boundary upstream held raw content too long and shipped it interior as a format blob. `System.Text.Json.JsonElement` in an entity door couples the type to one serializer's CLR artifact — dict = structure with the type dropped, JsonElement = structure with the format still attached. Both fail loud now.

### The replacement — the standard door, two arms and a refusal

```csharp
// GoalCall — the courier override. Overriding Create(raw, data) rather than the pure core
// is DELIBERATE: the ICreate base courier has a container fallback (ICreate.cs:78-79) that
// Clr-deserializes a dict into record hosts (step, …). goal.call must refuse that — a dict
// reaching this door means a boundary dropped the type, and it fails HERE, loudly, instead
// of the base quietly rebuilding it.
public static GoalCall? Create(object? raw, global::app.data.@this data)
{
    if (raw is GoalCall gc) return gc;
    if (raw is string name) return new GoalCall { Name = name };
    data.Fail(new global::app.error.Error(
        $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
        $"'goal.call' cannot be created from it. A structured goal.call arrives typed from its reader.",
        "CreateItemDeclined", 400));
    return null;
}
```

That override choice is load-bearing: override only the pure core and the base courier's dict-deserialize branch silently reconstructs GoalCalls from dicts — the fail-loud ruling undone by inheritance. Blocking the fallback IS the ruling.

**Three entry forms, each complete — parameters accounted for:**
- **Reader** (structured `{name, parallel, parameter, prPath}`): the ONLY route that carries `Parameter`. .pr load and build ingest both dispatch type-tagged content to the registered `goal/call/Reader` at the serializer boundary.
- **`Create(string)`**: the degenerate *name-only* lift (`call %goalName%`). Never carries parameters — `call Process a=1` builds as the structured form and never reaches this arm. A string arm that parsed parameters would be a mini-parser, i.e. the thing being deleted.
- **C# initializer**: `new GoalCall { Name = …, Parameter = … }` — programmatic composition needs no door.

### `ToGoalCall` — dies entirely (not "reads via the Reader")

Routing `ToGoalCall` through the Reader would still be consume-time parsing, just with the right parser. Under parse-at-the-boundary there is nothing left to convert when build validation runs: the build-response ingest reads the response **once** through the channel serializer, and a goal.call-typed parameter row materializes as a `GoalCall` at that read. `Default.cs:573` / `:1230` become `(await p.Value()) is GoalCall` — an **assertion**, not a conversion. Assertion fails ⇒ the response ingest is broken ⇒ it says so there.

### The CLR-name guards — demolition, with verification

`IsClrTypeName` at `Convert:70`, `FromSlots:118`, `GetGoalAsync:169`, `Default.cs:576-580`: each is a tripwire for the historical "Fluid template ToString'd a typed object into a name slot" leak (`GoalCall.cs:122-123` says so verbatim). "Process" has nothing to do with CLR — interior constructors defending against boundary leaks is the dict-arm pathology in miniature. One-structure + typed-interior kills the leak class at the root, and a genuinely wrong name already fails naturally as `GoalNotFound` in `GetGoalAsync` with the bogus name in the message. Retire all guard sites **with verification** (each marks a real past regression — confirm the root cause is dead, then delete; don't delete on faith).

### Also dying, per your own note
- `FromSlots` and its entire symptom catalog (the `null.@this`-ToString-is-"null" guards, the `{scheme, relative}` path-dict repair).
- The `parameter.list.@this(object slot, context)` dict-parsing ctor you added — reverts, as you proposed. Params arrive as the existing `@this(IReadOnlyList<Data>)`.
- Your parked `GoalCall.Parameter` promotion (`08939f599`) stays — correct regardless.

## Your validation plan — redirected

"Prove the arms dead by making them throw" is necessary but it is not the goal. The goal is: **find the boundary that dropped the type.** Each loud failure names an upstream site that degraded a goal.call to a dict/blob; the fix is always at that boundary (preserve the type tag so the registered reader builds it at parse), never a re-added arm.

- **First suspect:** the goal.call-typed row materialization in the build-ingest / `@schema:data` path — if a row whose type is `goal.call` lands its value slot as a born-native dict instead of dispatching through the registered reader, that is the leak that fed `FromSlots`.
- **The foreach regression, re-read (Ingi's point):** the loop *item* (`%product%`) being a dict is just data — untouched, no change needed. The GoalCall in `foreach …, call DoProduct` is built at build time and lives typed in the .pr. The `{scheme, relative}` repair in `FromSlots` marks a wire hop that dropped the type on the GoalCall itself — locate that hop; do not "fix" the loop.
- Exercise BOTH build (`plang build` on a goal.call-bearing goal) and the runtime foreach-calling-a-goal-with-params flow.
- Mutation-test announce rule applies if you temporarily edit production source to force the throws.

## Two verifications while you're in there

1. **The by-name "conversion registry"** that discovers `Convert`: find it, check whether GoalCall is its ONLY client. If yes, the mechanism dies with Convert (a registry serving one type is scaffolding). If other types hang off it, report back — that's the next fork, handled separately.
2. **Teaching layer, same commit:** the LLM must emit the goal.call structure (`{name, parameter: list<data>, prPath}`), so wherever that shape is taught — compile templates, `os/system/modules/**` notes/examples, the catalog text `IsCatalogDescription` skips — must change with the C#. A C#-only change with stale teaching makes the LLM feed the old shape into a now-loud door. This sweep crosses the C#→plang boundary; `os/**` counts as code.

## Ruling 2: sequencing — land NOW, as its own coordinated change

Not with the recovery pass. (1) The Convert-delete and the build-ingest change are atomic by necessity — they can't ride separate passes. (2) The change *narrows* the shapes flowing through `build.code`; every shape killed here is a shape recovery never has to round-trip — it makes the blocked redesign smaller, forecloses nothing. (3) Scope it tightly: Convert/FromSlots/ToGoalCall/guards + the ingest-boundary fix + the teaching sweep. Nothing else in `Default.cs`.

## Net

- goal.call has one structure everywhere: .pr wire = LLM emit = ingest, one Reader at boundaries.
- `GoalCall` construction surface: Reader (structured) / `Create(string)` (name-only) / initializer (C#). Null, dict, JsonElement — all fail typed and loud through the standard `ICreate` contract.
- Demolition: `Convert`, `FromSlots`, `ToGoalCall`, the dict-parsing `parameter.list` ctor, all `IsClrTypeName` guard sites (with verification), possibly the by-name conversion registry.
- `BuildParamSchema` untouched.
