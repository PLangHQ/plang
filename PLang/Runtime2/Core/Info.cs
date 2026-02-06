namespace PLang.Runtime2.Core;

public sealed class Info
{
    public (int Start, int End) Lines { get; init; }
    public string Text { get; init; } = "";
}
