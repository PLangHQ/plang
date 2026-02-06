using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Core;

public interface IAction
{
    string Class { get; }
    string Method { get; }
    List<Data> Parameters { get; }
    Return Return { get; }
}
