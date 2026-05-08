using System;
using System.Diagnostics;

namespace App.Utils
{
	public class RegisterStartupParameters
	{

		public static (bool builder, bool runtime) Register(string[] args)
		{
			AppContext.SetData(App.Variables.Reserved.ParametersAtAppStart, args.Where(p => p.StartsWith("--")).ToArray());
			if (args.FirstOrDefault(p => p == "--debug") != null)
			{
				AppContext.SetSwitch(App.Variables.Reserved.Debug, true);
				AppContext.SetSwitch(App.Variables.Reserved.DetailedError, true);
			}
			if (args.FirstOrDefault(p => p == "--env") != null)
			{
				AppContext.SetSwitch(App.Variables.Reserved.Environment, true);
			}

			var csdebug = args.FirstOrDefault(p => p == "--csdebug") != null;
			if (csdebug && !Debugger.IsAttached)
			{
				Debugger.Launch();
				AppContext.SetSwitch(App.Variables.Reserved.CSharpDebug, true);
				AppContext.SetSwitch(App.Variables.Reserved.DetailedError, true);
			}
			var strictbuild = args.FirstOrDefault(p => p == "--strictbuild") != null;
			if (strictbuild)
			{
				AppContext.SetSwitch(App.Variables.Reserved.StrictBuild, true);
			} 
			var detailerror = args.FirstOrDefault(p => p == "--detailerror") != null;
			if (detailerror) 
			{
				AppContext.SetSwitch(App.Variables.Reserved.DetailedError, true);
			}
			var loggerLovel = args.FirstOrDefault(p => p.StartsWith("--logger"));
			if (loggerLovel != null)
			{
				AppContext.SetData("--logger", loggerLovel.Replace("--logger=", ""));
			}
			bool builder = false;
			bool runtime = false;

			var build = args.FirstOrDefault(p => p == "build" || p.StartsWith("--builder") || p.StartsWith("--build")) != null;
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
					throw new ArgumentException("Parameter --llmservice can only be 'plang' or 'openai'. For example --llmservice=openai");
				}
				AppContext.SetData("llmservice", serviceName);
			}

			return (builder, runtime);
		}
	}
}
