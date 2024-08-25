﻿using PLang.Exceptions;
using System;
using System.Diagnostics;

namespace PLang.Utils
{
	public class RegisterStartupParameters
	{

		public static (bool builder, bool runtime) Register(string[] args)
		{
			AppContext.SetData(ReservedKeywords.ParametersAtAppStart, args.Where(p => p.StartsWith("--")).ToArray());
			if (args.FirstOrDefault(p => p == "--debug") != null)
			{
				AppContext.SetSwitch(ReservedKeywords.Debug, true);
				AppContext.SetSwitch(ReservedKeywords.DetailedError, true);
			}
			var csdebug = args.FirstOrDefault(p => p == "--csdebug") != null;
			if (csdebug && !Debugger.IsAttached)
			{
				Debugger.Launch();
				AppContext.SetSwitch(ReservedKeywords.CSharpDebug, true);
				AppContext.SetSwitch(ReservedKeywords.DetailedError, true);
			}
			var strictbuild = args.FirstOrDefault(p => p == "--strictbuild") != null;
			if (strictbuild)
			{
				AppContext.SetSwitch(ReservedKeywords.StrictBuild, true);
			}
			var detailerror = args.FirstOrDefault(p => p == "--detailerror") != null;
			if (detailerror)
			{
				AppContext.SetSwitch(ReservedKeywords.DetailedError, true);
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
