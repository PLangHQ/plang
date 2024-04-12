using PLang.Exceptions;
using System;
using System.Diagnostics;

namespace PLang.Utils
{
	public class RegisterStartupParameters
	{

		public static void Register(string[] args)
		{
			if (args.FirstOrDefault(p => p == "--debug") != null)
			{
				AppContext.SetSwitch(ReservedKeywords.Debug, true);
			}
			var csdebug = args.FirstOrDefault(p => p == "--csdebug") != null;
			if (csdebug && !Debugger.IsAttached)
			{
				Debugger.Launch();
				AppContext.SetSwitch(ReservedKeywords.CSharpDebug, true);
			}
			var build = args.FirstOrDefault(p => p == "build") != null;
			if (build)
			{
				AppContext.SetSwitch("Builder", true);
			}
			else
			{
				AppContext.SetSwitch("Runtime", true);
			}

			var llmservice = args.FirstOrDefault(p => p.ToLower().StartsWith("--llmservice"));
			if (llmservice != null)
			{
				if (llmservice.IndexOf("=") == -1)
				{
					throw new RuntimeException("Parameter --llmservice can only be 'plang' or 'openai'. For example --llmservice=openai");
				}
				var serviceName = llmservice.Substring(llmservice.IndexOf("=") + 1).ToLower();
				if (string.IsNullOrWhiteSpace(serviceName)) serviceName = "plang";
				if (serviceName != "plang" && serviceName != "openai")
				{
					throw new RuntimeException("Parameter --llmservice can only be 'plang' or 'openai'. For example --llmservice=openai");
				}
				AppContext.SetData("llmservice", serviceName);
			}
		}
	}
}
