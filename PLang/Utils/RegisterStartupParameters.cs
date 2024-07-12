using PLang.Exceptions;
using System;
using System.Diagnostics;

namespace PLang.Utils
{
	public class RegisterStartupParameters
	{

		public static (bool builder, bool runtime) Register(string[] args)
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
			bool builder = false;
			bool runtime = false;

			var build = args.FirstOrDefault(p => p == "build") != null;
			if (build)
			{
				builder = true;
				runtime = false;
			}
			else
			{
				builder = false;
				runtime = true;
			}
			var exec = args.FirstOrDefault(p => p == "exec") != null;
			if (exec)
			{
				builder = true;
				runtime = true;
			}

			var llmservice = args.FirstOrDefault(p => p.ToLower().StartsWith("--llmservice")) ?? Environment.GetEnvironmentVariable("PLangLllmService");
			if (!string.IsNullOrEmpty(llmservice))
			{
				var serviceName = llmservice.ToLower();
				if (llmservice.IndexOf("=") != -1)
				{
					serviceName = llmservice.Substring(llmservice.IndexOf("=") + 1).ToLower();
				}

				if (serviceName != "plang" && serviceName != "openai")
				{
					throw new RuntimeException("Parameter --llmservice can only be 'plang' or 'openai'. For example --llmservice=openai");
				}
				AppContext.SetData("llmservice", serviceName);
			}

			return (builder, runtime);
		}
	}
}
