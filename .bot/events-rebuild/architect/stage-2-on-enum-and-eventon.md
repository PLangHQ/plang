# Stage 2: `event.on` action rebuilt + scope:goal|app parameter

**Goal:** The PLang `event.on` action writes to the new `Event.@this` registries instead of the old `Events.@this`. Adds the `scope:goal|app` parameter. Variable events become registrable via `%var.path%` patterns.

**Scope:**
- Rewrite `PLang/App/modules/event/on.cs` to produce bindings of the new shape and register on either `Context.Event` or `App.Event` based on `scope:`.
- Add `Scope` property (`EventScope` enum) and translate to the new registry.
- Map the old `Type` parameter (an `EventType` value like `BeforeStep`) to the new `(On, Phase)` pair. Both shapes accepted during the migration window — the LLM/builder will emit either depending on training.
- Add support for the variable event shape: `Type=GetBefore`/`SetBefore`/etc. with a `Path` parameter for the variable expression. Alternatively, infer `On.Variable` when a `%var.path%` pattern is supplied.
- Update the action's `[Example]` attributes so the builder sees the new shape.

**Out of scope:**
- The catalog mechanism for validating `%step.Text%` paths at build time. Filed as a Stage 2 sub-task but acceptable to defer to a follow-up if it slips — see "Catalog" below.
- Fire sites (Stage 3).
- Data.Value firing (Stage 4).

**Deliverables:**
- `PLang/App/modules/event/on.cs` — rewritten handler. Branches on `Scope` to pick the registry. Accepts new variable-event parameters.
- `PLang/App/Event/Scope.cs` — `EventScope { Goal, App }` enum.
- Validation: `Actor != null` + `Scope.App` rejected (build-time preferred, runtime fallback acceptable).
- Tests in `PLang.Tests/App/modules/event/` and `Tests/event/`:
  - Default `Scope=Goal` registers on `Context.Event`.
  - `Scope=App` registers on `App.Event`.
  - `Actor + Scope=App` rejected.
  - Old `Type=BeforeStep` shape maps to `(On.Step, Phase.Before)`.
  - New `On=Variable, Name="step", Path="Text"` registers a variable binding.

**Dependencies:** Stage 1.

## Design

### The rewritten handler

```csharp
public Task<Data> Run()
{
    var targetActor = Actor?.Value ?? Context.Actor ?? Context.App!.User;

    if (Scope.Value == EventScope.App && Actor is not null)
        return Task.FromResult(Data.Fail(
            new Error("Actor and scope:app are mutually exclusive — pick one.")));

    var (on, phase) = MapTypeToOnPhase(Type.Value);

    var binding = new Binding(
        Id: Guid.NewGuid().ToString("N"),
        On: on,
        Name: GoalPattern?.Value ?? StepPattern?.Value ?? ActionPattern?.Value ?? Name?.Value,
        Path: Path?.Value,
        Type: phase,
        IsRegex: IsRegex.Value,
        Priority: Priority.Value,
        Handler: async (source) => await Context.App!.RunGoalAsync(GoalToCall.Value!, targetActor.Context));

    var registry = Scope.Value == EventScope.App
        ? Context.App!.Event
        : targetActor.Context.Event;

    var id = registry.Register(binding);
    return Task.FromResult(Data(id));
}
```

### Mapping old `EventType` to new `(On, Phase)`

A small switch:

```csharp
private static (On, Phase) MapTypeToOnPhase(EventType t) => t switch
{
    EventType.BeforeGoal     => (On.Goal,     Phase.Before),
    EventType.AfterGoal      => (On.Goal,     Phase.After),
    EventType.BeforeStep     => (On.Step,     Phase.Before),
    EventType.AfterStep      => (On.Step,     Phase.After),
    EventType.BeforeAction   => (On.Action,   Phase.Before),
    EventType.AfterAction    => (On.Action,   Phase.After),
    EventType.BeforeWrite    => (On.Write,    Phase.Before),
    EventType.AfterWrite     => (On.Write,    Phase.After),
    EventType.BeforeRead     => (On.Read,     Phase.Before),
    EventType.AfterRead      => (On.Read,     Phase.After),
    EventType.OnAsk          => (On.Ask,      Phase.Before),  // see note
    EventType.BeforeAppStart => (On.App,      Phase.Before),
    EventType.AfterAppStart  => (On.App,      Phase.After),
    _ => throw new NotSupportedException($"Unknown EventType: {t}")
};
```

**`OnAsk` note:** today's `OnAsk` is single-phase ("when channel is asked"). Maps to `(On.Ask, Phase.Before)` — treat "On" as "Before the ask is processed." If `(On.Ask, Phase.After)` is needed later, it's a clean addition.

### New parameters

```csharp
[Default(EventScope.Goal)]
public partial Data<EventScope> Scope { get; init; }

/// <summary>Unified name pattern. Goal pattern for On.Goal, step pattern for On.Step,
/// action pattern for On.Action, variable name for On.Variable, etc.
/// Falls back to the type-specific pattern fields (GoalPattern/StepPattern/...) if not set.</summary>
public partial Data<string>? Name { get; init; }

/// <summary>Sub-path within the variable, for On.Variable. e.g. "Text" for %step.Text%.</summary>
public partial Data<string>? Path { get; init; }

/// <summary>Direct On override. Overrides the implicit On from Type.</summary>
public partial Data<On>? On { get; init; }
```

Keeping the old `GoalPattern`/`StepPattern`/`ActionPattern` accepted for builder compatibility. The handler reads whichever is non-null.

### Catalog for `%step.Text%` path validation

The builder needs to know:
- Variable names that exist as "core types" (step, goal, action, channel, app).
- The properties on those types so `%step.Text%` validates.

Options:
- **Reflection at build time** — slow on startup, drift-free.
- **Hand-maintained catalog** — fast, drift-prone.
- **Source-generated catalog** — emit from PLang.Generators alongside action scanning. Drift-free, fast.

**Recommended:** source-generated. Add a `[CoreType("step")]` attribute to `Step.@this`, `Goal.@this`, `Action.@this`, `Channel.@this`, `App.@this`. Generator emits a registry of `{ plang_name → CLR type → properties[] }`. Builder reads this when validating `%var.path%` in `event.on` patterns.

If the catalog mechanism slips (it's its own design surface — the public properties of a class are not all suitable as PLang property names), Stage 2 can ship without it. Variable events still register; the builder just doesn't validate path strings until the catalog lands. File the catalog as a Stage 2 sub-task; OK to defer.

### What NOT to do in this stage

- Don't migrate fire sites. The OLD `Events.@this` is still where today's lifecycle calls write. After Stage 2, new bindings go to the new registry — but fire sites still read the old one. **Fire sites don't see the new bindings yet.** That's expected; Stage 3 closes that gap.
- During the gap between Stage 2 and Stage 3, existing PLang event tests will fail because bindings register to a registry that nothing fires from. Test suite stays red between stages. Acceptable for an internal-development branch.

Alternative shape (avoids the red gap): bridge the old and new registries during Stage 2 — `event.on` registers to BOTH registries until Stage 3 fully migrates fire sites. Slightly more code but keeps tests green between stages.

**Recommended:** the bridge approach. Coder writes once to both registries during Stage 2; Stage 3 migrates fire sites; Stage 5 removes the bridge along with the old registries.
