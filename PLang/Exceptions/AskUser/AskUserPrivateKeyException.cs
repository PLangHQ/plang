using PLang.Building.Model;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions.AskUser
{
	public class AskUserPrivateKeyException : AskUserException
	{

		public AskUserPrivateKeyException(string message, Func<string, Task>? callback = null) : base(message, CreateAdapter(callback))
		{

		}

		public override async Task InvokeCallback(object answer)
		{
			await Callback?.Invoke(new object[] { answer });
		}
	}
}
