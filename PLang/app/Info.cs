using app.Attributes;

namespace app;

[PlangType]
public sealed class Info
{
    [LlmBuilder] public string Key { get; init; } = "";
    [LlmBuilder] public string Message { get; init; } = "";
}
