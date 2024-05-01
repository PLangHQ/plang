using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Utils;

namespace PLang.Errors.Handlers
{
    public class UiErrorHandler : BaseErrorHandler, IErrorHandler
	{
        private readonly IErrorDialog dialog;

        public UiErrorHandler(IErrorDialog dialog, IAskUserHandlerFactory askUserHandlerFactory) : base(askUserHandlerFactory)
        {
            this.dialog = dialog;
        }
        public async Task<bool> Handle(IError error, int statusCode, string statusText, string message)
        {
            return await base.Handle(error);
        }
        public async Task ShowError(IError error, int statusCode, string statusText, string message, GoalStep? step)
        {
            dialog.ShowDialog(error, "Error");
        }
    }
}
