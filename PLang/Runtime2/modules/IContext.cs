using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.modules;

public interface IContext
{
    PLangContext Context { get; set; }
}
