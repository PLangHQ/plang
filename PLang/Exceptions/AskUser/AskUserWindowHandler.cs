using PLang.Errors;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Exceptions.AskUser
{

	public class AskUserWindowHandler : IAskUserHandler
	{
		private readonly IAskUserDialog dialog;

		public AskUserWindowHandler(IAskUserDialog dialog)
		{
			this.dialog = dialog;
		}

		public async Task<bool> Handle(AskUserError ex)
		{
			var result = dialog.ShowDialog(ex.Message, "Ask");
			
			if (ex.InvokeCallback != null)
			{
				await ex.InvokeCallback(result);
			}
			return true;

		}

	}
}
