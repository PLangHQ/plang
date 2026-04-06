using App.Context;

namespace App.modules;

public interface IContext
{
    Context.@this Context { get; set; }
}
