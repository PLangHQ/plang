using PLang.Runtime2.Context;

namespace PLang.Runtime2.modules;

public interface IContext
{
    PLangContext Context { get; set; }
}
