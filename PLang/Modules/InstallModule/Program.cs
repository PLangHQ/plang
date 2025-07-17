using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using static PLang.Modules.HttpModule.Program;

namespace PLang.Modules.InstallModule
{
	[Description("Install external utility from url")]
	public class Program : BaseProgram
	{

		public Program()
		{
		}

		[Description("Install external app(python, bash, go, etc.) from a url, such as github. It uses the README to install and creates examples at build time.")]
		public async Task<IError?> InstallFromUrl(HttpModule.Program.HttpRequest request)
		{
			return null;


		}
	}
}
