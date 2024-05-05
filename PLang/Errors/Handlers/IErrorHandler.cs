using PLang.Errors;

namespace PLang.Errors.Handlers
{
	public interface IErrorHandler
	{
		Task<bool> Handle(IError error);
		Task ShowError(IError error, Building.Model.GoalStep? step = null);
	}
}
