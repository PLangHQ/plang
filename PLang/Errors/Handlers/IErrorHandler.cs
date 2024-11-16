using PLang.Building.Model;

namespace PLang.Errors.Handlers;

public interface IErrorHandler
{
    Task<(bool, IError?)> Handle(IError error);
    Task ShowError(IError error, GoalStep? step = null);
}