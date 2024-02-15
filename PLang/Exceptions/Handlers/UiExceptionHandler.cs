using PLang.Interfaces;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions.Handlers
{
	public class UiExceptionHandler : ExceptionHandler, IExceptionHandler
	{
		private readonly IErrorDialog dialog;

		public UiExceptionHandler(IErrorDialog dialog, IAskUserHandler askUserHandler) : base(askUserHandler)
		{
			this.dialog = dialog;
		}

		public async Task<bool> Handle(Exception exception, int statusCode, string statusText, string message)
		{
			if (await base.Handle(exception)) { return true; }
			dialog.ShowDialog(exception, message + "\n\n" + exception.ToString(), "Error");
			return false;
		}
	}
}
