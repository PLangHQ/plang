using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions.Handlers
{
	public class UiExceptionHandler : IExceptionHandler
	{
		private readonly IErrorDialog dialog;

		public UiExceptionHandler(IErrorDialog dialog)
		{
			this.dialog = dialog;
		}

		public async Task Handle(Exception exception, int statusCode, string statusText, string message)
		{
			dialog.ShowDialog(exception, message + "\n\n" + exception.ToString(), "Error");
		}
	}
}
