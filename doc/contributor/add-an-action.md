# Add an action

For someone changing the runtime. The question this page answers: what does it take to add a small action, and what does it trip?

This page records a real run. A throwaway module `greeter` with one action `hello` was added, built, used from a goal and tested, then removed. `git status` was clean afterwards and `./dev.sh test SurfaceTests` passed again (3 of 3).

**Short answer: you can write, build, run and test an action on your own machine. You cannot merge one without a yes**, because a new action changes the pinned surface of the [locked core](the-locked-core.md). Propose it first.

## 1. The handler

`PLang/app/module/greeter/hello.cs`:

```csharp
namespace app.module.greeter;

[Action("hello")]
public partial class Hello : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Who { get; init; }

    public async Task<data.@this<global::app.type.item.text.@this>> Start()
        => Context.Ok((global::app.type.item.text.@this)$"Hello, {await Who.Value()}!");
}
```

- The folder name is the module (`greeter`); `[Action("hello")]` is the action. The class is `partial`; the generator writes the rest ([how a step runs](how-a-step-runs.md)).
- A parameter is a `partial` property of `data.@this<T>`. Any other kind fails the build with `PLNG001`.
- `Start()` returns the result as `Data<T>`; T is the action's declared result.
- Open a parameter with `await x.Value()` where you use it.

## 2. The teaching

The builder learns the action from markdown in `os/system/modules/greeter/`:

- `module.description.md`: `Greet someone by name`
- `hello.description.md`: one line saying what the action answers. The one used: `Answer a greeting text for a name: Hello, <name>!`
- `hello.examples.md`: step texts and the properties they build to:

```
Step text: `greet Ada`
Properties: `{"Who": "Ada"}` — the person to greet, as the step writes it.

Step text: `greet Ada, write to %greeting%`
Properties: `{"Who": "Ada"}` — the trailing `write to %greeting%` is its own action, never the person.
```

## 3. Build and use

`./dev.sh build` built it with no error (the first build 112 s, the next 52 s). Nothing is registered by hand: the generator and the folder name make the action appear (`os/system/modules/catalog.md:38-39`: "Add a C# action and it appears automatically"). A property that is not `Data<T>` or `[Code]`, or a `System.IO` reach, fails this same build (`PLNG001`, `PLNG002`; [the rules](rules.md)).

Make a scratch folder outside the repository with the goal below, and run the new binary there. `plang build` in a folder that is not yet an app asks to create it; pass `--app.setting.create=true`. A build needs the decider key ([install](../install.md#the-key-a-build-needs)).

```plang
Start
- greet Ada, write to %greeting%
- write out %greeting%
- greet Grace
```

`plang build` made the steps `greeter.hello; variable.set`, `output.write`, `greeter.hello`. `plang` printed `Hello, Ada!`.

## 4. A misfire to avoid

The first version named the parameter `Name` and had one example. The builder read the step `greet Ada, write to %greeting%` as `Name="%greeting%"` and the run printed `Hello, !`. Renaming the property to `Who` and adding the example with the trailing `write to` fixed it. The examples file teaches the builder which words are the parameter; a parameter named like a common word, with one example, is easy to misread. Read the built `.pr` (`jq '.step[0].code' .build/start.pr`) before you trust a new action.

## 5. What it tripped

- `SurfaceTests.TheCoresSurface_IsThePinnedOne` failed: 1 of 3. A new action is an addition to the pinned surface.
- `PickListTests.TheStageOneRequest_IsThePinnedOne` failed: 1 of 8. The decider's first request lists the catalog, and the catalog grew.
- Passed unchanged: `ModulePageTests` (8), `ConfirmTemplateTests` (2), `LineTwinTests` (1), `PickOptionTests` (18).

Both failures are fixed by re-pinning: `AcceptTheSurface` for the surface, `AcceptTheFixture` for the decider fixture. Re-pinning the surface needs the approving decision named in the commit ([the locked core](the-locked-core.md)). Read the diff of each pin before you commit it.

## Where a new action's logic goes

Keep the handler thin. `math.add` calls a provider (`[Code] IMath Math`, `module/math/add.cs:19-22`) that does the work, so another implementation can replace it. A value's own operation belongs on the value, not in the handler ([the rules](rules.md)).
