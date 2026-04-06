using App.Actor.Context;

namespace App.modules;

public interface IContext
{
    Actor.Context.@this Context { get; set; }
}
