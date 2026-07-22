using app.Attributes;

namespace app.warning;

/// <summary>
/// A build-time diagnostic about a program node — a {Key, Message} pair the builder attaches to a
/// goal/step/action (and, in a later pass, Data results). A "warning" by definition: a build error
/// that aborts never produces a node to hang itself on, and one that doesn't abort IS a warning.
/// Runtime failures are a different thing — they are errors, and they land on the run (Call.Errors,
/// App.Errors, the Data result), never on the graph, because the same node can be mid-flight in many
/// concurrent actors at once. Replaces the old shared <c>app.Info</c> pair on the graph nodes.
/// </summary>
[PlangType]
public sealed class @this
{
    [LlmBuilder] public string Key { get; init; } = "";
    [LlmBuilder] public string Message { get; init; } = "";
}
