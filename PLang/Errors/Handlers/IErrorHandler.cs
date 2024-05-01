using PLang.Errors;

namespace PLang.Errors.Handlers
{
	public interface IErrorHandler
	{
		Task<bool> Handle(IError error, int statusCode, string statusText, string message);
		Task ShowError(IError error, int statusCode, string statusText, string message, Building.Model.GoalStep? step = null);
	}
}
