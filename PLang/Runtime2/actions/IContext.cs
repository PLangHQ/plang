using PLang.Runtime2.Engine.Context;

namespace PLang.Runtime2.actions;

public interface IContext
{
    PLangContext Context { get; set; }
}
