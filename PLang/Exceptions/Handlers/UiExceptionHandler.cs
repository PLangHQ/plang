using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Utils;

namespace PLang.Exceptions.Handlers
{
	public class UiExceptionHandler : ExceptionHandler, IExceptionHandler
	{
		private readonly IErrorDialog dialog;

		public UiExceptionHandler(IErrorDialog dialog, IAskUserHandlerFactory askUserHandlerFactory) : base(askUserHandlerFactory)
		{
			this.dialog = dialog;
		}
		public async Task<bool> Handle(Exception exception, int statusCode, string statusText, string message)
		{
			return await base.Handle(exception);
		}
		public async Task<bool> ShowError(Exception exception, int statusCode, string statusText, string message, GoalStep? step)
		{
			//if (await base.Handle(exception)) { return true; }
			dialog.ShowDialog(exception, $"Step: {step.Text} in goal {step.Goal.GoalName}\n\n{message}\n\n{exception.ToString()}", "Error");
			return false;
		}
	}
}
