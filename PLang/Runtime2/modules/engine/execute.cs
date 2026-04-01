using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.engine;

/// <summary>
/// Kernel step dispatch — runs a step's actions via engine.Run().
/// </summary>
[Action("execute")]
public partial class Execute : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;

        Data result = Data.Ok();
        foreach (var action in Step.Actions)
        {
            result = await engine.Run(action, Context);
            if (!result.Success) break;
        }

        // Return the result directly — PLang runtime handles errors
        // Mark as handled so RunSteps doesn't stop execution
        result.Handled = true;
        return result;
    }
}
