using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Utils;

namespace PLang.Errors.Handlers
{
    public class UiErrorHandler : BaseErrorHandler, IErrorHandler
	{
        private readonly IErrorDialog dialog;

        public UiErrorHandler(IErrorDialog dialog) : base()
        {
            this.dialog = dialog;
        }
        public async Task<(bool, IError?)> Handle(IError error)
        {
            return await base.Handle(error);
        }
        public async Task ShowError(IError error, GoalStep? step)
        {
            dialog.ShowDialog(error, "Error");
        }
    }
}
