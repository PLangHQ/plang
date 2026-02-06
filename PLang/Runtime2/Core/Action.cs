using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public sealed class Action : IAction
{
    public string Class { get; init; } = "";
    public string Method { get; init; } = "";
    public List<Data> Parameters { get; init; } = new();
    public Return Return { get; init; } = new();
}
