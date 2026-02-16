using PLang.Runtime2.Memory;

namespace PLang.Runtime2;

public interface IAction
{
    string Class { get; }
    string Method { get; }
    List<Data> Parameters { get; }
    List<Data>? Return { get; }
}
