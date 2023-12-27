using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Runtime.Startup.ModuleLoader;

namespace PLang.Exceptions.AskUser
{

	public class AskUserFileAccess : AskUserException
	{
		private readonly string app;
		private readonly string path;

		public AskUserFileAccess(string app, string path, string message, Func<string, string, string, Task>? callback = null) : base(message, CreateAdapter(callback))
		{
			this.app = app;
			this.path = path;
		}

		public override async Task InvokeCallback(object answer)
		{
			await Callback?.Invoke(new object[] { app, path, answer });
	
		}
	}

}
