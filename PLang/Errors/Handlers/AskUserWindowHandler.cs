using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Errors.Handlers
{

    public class AskUserWindowHandler : IAskUserHandler
    {
        private readonly IAskUserDialog dialog;

        public AskUserWindowHandler(IAskUserDialog dialog)
        {
            this.dialog = dialog;
        }

        public async Task<(bool, IError?)> Handle(AskUser.AskUserError ex)
        {
            var result = dialog.ShowDialog(ex.Message, "Ask");

            return await ex.InvokeCallback([result]);
        }

    }
}
