using app.variable;
using app.module.builder.code;

namespace app.module.builder;

/// <summary>
/// Build-time only handler. Invoked by os/system/builder/ApplyStep.goal during
/// `plang build`. Copies LLM-derived fields (Actions and their Modifiers,
/// Errors, Warnings) from <see cref="StepFromLlm"/> onto <see cref="Step"/>;
/// structural fields (Text, Index, Indent, LineNumber) on the target are
/// preserved. Used to fold a freshly-built step result back onto the parser's
/// step shape before persistence.
/// </summary>
[Action("merge")]
public partial class merge : IContext
{
    /// <summary>Target step from the parser — keeps its Text, Index, Indent, LineNumber.</summary>
    [IsNotNull]
    public partial data.@this<Step> Step { get; init; }

    /// <summary>Source step from the LLM — its Actions/Errors/Warnings overwrite the target's.</summary>
    [IsNotNull]
    public partial data.@this<Step> StepFromLlm { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public Task<data.@this> Run() => Task.FromResult(Builder.Merge(this));
}
