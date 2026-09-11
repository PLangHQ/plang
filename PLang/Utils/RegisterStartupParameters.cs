using Castle.Core.Logging;
using PLang.Exceptions;
using System;
using System.Diagnostics;

namespace PLang.Utils
{
	public class RegisterStartupParameters
	{
		// Building a step is two llm round trips and the steps of a goal do not feed each other, so
		// waiting for them one at a time is waiting for nothing. Measured on a six step goal with a
		// cold cache: 35995 ms sequential against 8865 ms at 6. Sequential is still one flag away,
		// --buildparallel=1, for an llm account with a tight rate limit.
		public const int DefaultBuildParallel = 6;

		public static (bool builder, bool runtime) Register(string[] args)
		{
			AppContext.SetData(ReservedKeywords.ParametersAtAppStart, args.Where(p => p.StartsWith("--")).ToArray());
			if (args.FirstOrDefault(p => p == "--debug") != null)
			{
				AppContext.SetSwitch(ReservedKeywords.Debug, true);
				AppContext.SetSwitch(ReservedKeywords.DetailedError, true);
			}
			if (args.FirstOrDefault(p => p == "--env") != null)
			{
				AppContext.SetSwitch(ReservedKeywords.Environment, true);
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
			var loggerLovel = args.FirstOrDefault(p => p.StartsWith("--logger"));
			if (loggerLovel != null)
			{
				AppContext.SetData("--logger", loggerLovel.Replace("--logger=", ""));
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

				if (serviceName != "plang" && serviceName != "openai" && serviceName != "poolside" && serviceName != "inception")
				{
					throw new RuntimeException("Parameter --llmservice can only be 'plang', 'openai', 'poolside' or 'inception'. For example --llmservice=openai");
				}
				AppContext.SetData("llmservice", serviceName);
			}

			var parallel = args.FirstOrDefault(p => p.StartsWith("--buildparallel", StringComparison.OrdinalIgnoreCase));
			int buildParallel = DefaultBuildParallel;
			if (parallel != null)
			{
				var value = parallel.Contains("=") ? parallel.Substring(parallel.IndexOf("=") + 1) : DefaultBuildParallel.ToString();
				if (!int.TryParse(value, out buildParallel) || buildParallel < 1)
				{
					throw new RuntimeException("Parameter --buildparallel must be a number of 1 or more, e.g. --buildparallel=4");
				}
			}
			AppContext.SetData("buildparallel", buildParallel);

			return (builder, runtime);
		}
	}
}
