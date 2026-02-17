using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine;

public interface IAction
{
    string Class { get; }
    string Method { get; }
    List<Data> Parameters { get; }
    List<Data>? Return { get; }
}
