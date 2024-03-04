namespace PLang.Exceptions.Handlers
{
	public interface IExceptionHandler
	{
		Task<bool> Handle(Exception exception, int statusCode, string statusText, string message);
		Task<bool> ShowError(Exception exception, int statusCode, string statusText, string message, Building.Model.GoalStep? step);
	}
}
