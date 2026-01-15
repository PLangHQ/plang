using LightInject;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using PLang.Building;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules;
using PLang.Resources;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Reflection;
using static PLang.Executor;

namespace PLang
{
	public class Executor
	{
		private readonly IServiceContainer container;
		private readonly PrParser prParser;
		private readonly IPLangFileSystem fileSystem;
		private readonly IErrorHandler errorHandler;
		private IEngine engine;

		private IBuilder builder;

		private static IFileSystemWatcher? watcher = null;

		public Executor(IServiceContainer container)
		{
			this.container = container;

			this.prParser = container.GetInstance<PrParser>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();

		}

		public enum ExecuteType
		{
			Runtime = 0,
			Builder = 1
		}

		public async static Task<(IEngine Engine, object? Variables, IError? Error)> RunGoal(string goalName, Dictionary<string, object?>? parameters = null)
		{
			throw new NotImplementedException("This needs to be fixed, RunGoal needs to take in context. Since only one method calls this, maybe delete this method?");
			/*
			AppContext.SetSwitch("InternalGoalRun", true);
			AppContext.SetSwitch("Runtime", true);
			using (var container = new ServiceContainer())
			{
				container.RegisterForPLangConsole(Environment.CurrentDirectory, System.IO.Path.DirectorySeparatorChar.ToString());
				
				var engine = container.GetInstance<IEngine>();
				engine.Init(container, nu);

				var context = new PLangContext(container.GetInstance<MemoryStack>(), engine);

				if (parameters != null)
				{
					foreach (var param in parameters)
					{
						engine.GetMemoryStack().Put(param.Key, param.Value);
					}
				}
				var prParser = container.GetInstance<PrParser>();
				var fileAccessHandler = container.GetInstance<IFileAccessHandler>();
				var fileSystem = container.GetInstance<IPLangFileSystem>();

				await prParser.GoalFromGoalsFolder(fileSystem.RootDirectory, fileAccessHandler);

				var allGoals = prParser.GetAllGoals();
				var goal = allGoals.FirstOrDefault(p => p.RelativeGoalPath.Equals(goalName.AdjustPathToOs(), StringComparison.OrdinalIgnoreCase));
				if (goal == null) return (engine, null, new Error($"Goal {goalName} could not be found"));

				var (vars, error) = await engine.RunGoal(goal, context);
				AppContext.SetSwitch("InternalGoalRun", false);
				return (engine, vars, error);
			}*/
		}

		

		public async Task<(object? Variables, IError? Error)> Execute(string[] args, ExecuteType executeType)
		{
			var version = args.FirstOrDefault(p => p == "--version") != null;
			if (version)
			{
				var assembly = Assembly.GetAssembly(this.GetType());

				Console.WriteLine("plang version: " + assembly.GetName().Version.ToString());
				return (null, null);
			}

			if (args.Length > 0 && args[0] == "p")
			{
				if (executeType == ExecuteType.Runtime)
				{
					var result2 = await Run2(args[1..]);
					return (result2.Variables, result2.Error);
				} else if (executeType == ExecuteType.Builder)
				{
					var buildResult = await Build2(args[1..]);
					return (buildResult.Variables, buildResult.Error);
				}
					
			}

			var debug = args.FirstOrDefault(p => p == "--debug") != null;
			var validate = args.FirstOrDefault(p => p == "--validate") != null;
			var test = args.FirstOrDefault(p => p == "--test") != null;
			var watch = args.FirstOrDefault(p => p == "--watch") != null;

			LoadParametersToAppContext(args);
			if (args.FirstOrDefault(p => p == "exec") != null)
			{
				var list = args.ToList();
				list.Remove("exec");
				args = list.ToArray();
			}

			AppContext.SetSwitch("Builder", (executeType == ExecuteType.Builder));
			AppContext.SetSwitch("Runtime", (executeType == ExecuteType.Runtime));
			AppContext.SetSwitch("Validate", validate);

			if (executeType == ExecuteType.Builder)
			{
				AppContext.SetSwitch(ReservedKeywords.DetailedError, true);
				await Build(args);
				if (watch)
				{
					WatchFolder(fileSystem.GoalsPath, "*.goal");
					Console.Read();
				}
				return (null, null);
			}

			if (watch)
			{
				WatchFolder(fileSystem.GoalsPath, "*.goal");
			}
			
			var result = await Run(debug, test, args);
			return (result.Variables, result.Error);

		}

		private void LoadParametersToAppContext(string[] args)
		{
			var loggerLovel = args.FirstOrDefault(p => p.StartsWith("--logger"));
			if (loggerLovel != null)
			{
				AppContext.SetData("--logger", loggerLovel.Replace("--logger=", ""));
			}
			var llmerror = args.FirstOrDefault(p => p.ToLower().StartsWith("--llmerror"));
			if (llmerror != null)
			{
				AppContext.SetSwitch("llmerror", true);
			}
			var sharedPath = args.FirstOrDefault(p => p.ToLower().StartsWith("--sharedpath"));
			if (sharedPath != null)
			{
				AppContext.SetData("sharedPath", sharedPath);
				var fileAccessHandler = container.GetInstance<IFileAccessHandler>();
				fileAccessHandler.GiveAccess(Environment.CurrentDirectory, fileSystem.Path.Join(AppContext.BaseDirectory, "os"));
			}

			// This is only for development of plang as it is hardcoded to point to http://localhost:10000
			// It will overwrite the default PlangLLMService class to use the local url
			var llmurl = args.FirstOrDefault(p => p.ToLower().StartsWith("--localllm"));
			if (llmurl != null)
			{
				AppContext.SetSwitch("localllm", true);
			}


			AppContext.SetSwitch("skipCode", args.FirstOrDefault(p => p.ToLower() == "--skipcode") != null);

		}

		public void SetupDebug()
		{

			Console.WriteLine("-- Debug mode");
			AppContext.SetSwitch(ReservedKeywords.Debug, true);
			/*
			var eventsPath = fileSystem.Path.Join(fileSystem.GoalsPath, "events", "external", "plang", "runtime");

			if (fileSystem.Directory.Exists(eventsPath)) return;

			fileSystem.Directory.CreateDirectory(eventsPath);

			using (MemoryStream ms = new MemoryStream(InternalApps.Runtime))
			using (ZipArchive archive = new ZipArchive(ms))
			{
				archive.ExtractToDirectory(fileSystem.GoalsPath, true);
			}*/
			return;

		}


		private void WatchFolder(string path, string filter)
		{
			if (watcher == null)
			{
				watcher = fileSystem.FileSystemWatcher.New();
			}

			if (!fileSystem.Directory.Exists(path))
			{
				fileSystem.Directory.CreateDirectory(path);
			}

			watcher.Path = path;

			watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
			watcher.IncludeSubdirectories = true;
			// Only watch text files.
			watcher.Filter = filter;

			// Add event handlers.
			watcher.Changed += async (object sender, FileSystemEventArgs e) =>
			{
				using (var container = new ServiceContainer())
				{
					container.RegisterForPLangConsole(Environment.CurrentDirectory, Environment.CurrentDirectory);

					var pLanguage = new Executor(container);
					await pLanguage.Build(null);

					prParser.ForceLoadAllGoals();
				}
			};


			watcher.Renamed += async (object sender, RenamedEventArgs e) =>
			{
				using (var container = new ServiceContainer())
				{
					container.RegisterForPLangConsole(Environment.CurrentDirectory, Environment.CurrentDirectory);

					var pLanguage = new Executor(container);
					await pLanguage.Build(null);

					prParser.ForceLoadAllGoals();
				}
			}; ;

			// Begin watching.
			watcher.EnableRaisingEvents = true;

		}

		public async Task Build(string[]? args)
		{

			PLangContext context = null;
			try
			{
				this.engine = container.GetInstance<IEngine>();

				var msa = container.GetInstance<IMemoryStackAccessor>();
				var memoryStack = MemoryStack.New(container, engine);
				msa.Current = memoryStack;

				context = new PLangContext(memoryStack, this.engine, ExecutionMode.Console);
				var ca = container.GetInstance<IPLangContextAccessor>();
				ca.Current = context;

				this.engine.Init(container);

				LoadArgsToMemoryStack(args, memoryStack);

				this.builder = container.GetInstance<IBuilder>();
				var errors = await builder.Start(container, context);
				if (errors != null && errors.Count > 0)
				{
					foreach (var error in errors)
					{
						await this.engine.GetEventRuntime().AppErrorEvents(error);
					}
				}
				else
				{
					prParser.LoadAllGoals(true);
				}
			}
			catch (Exception ex)
			{
				var error = new ExceptionError(ex);

				await this.engine.GetEventRuntime().AppErrorEvents(error);
			}
		}
		public async Task<(IEngine? Engine, object? Variables, IError? Error)> Build2(string[] args)
		{
			var result = CommandLineParser.Parse(args);

			// parse args => dict<string, object>
			var goalInfo = new GoalToCallInfo("Build")
			{
				Parameters = result.parameters,
				Path = $".build{System.IO.Path.DirectorySeparatorChar}Build{System.IO.Path.DirectorySeparatorChar}00. Goal.pr"
			};
			return await Run2(goalInfo);
		}

		public async Task<(IEngine? Engine, object? Variables, IError? Error)> Run2(string[] args)
		{
			// parse args => dict<string, object>
			var result = CommandLineParser.Parse(args);
			var goalInfo = new GoalToCallInfo(result.goalName)
			{
				Parameters = result.parameters,
				Path = $".build{System.IO.Path.DirectorySeparatorChar}Run{System.IO.Path.DirectorySeparatorChar}00. Goal.pr"
			};

			return await Run2(goalInfo);
		}
		public async Task<(IEngine? Engine, object? Variables, IError? Error)> Run2(GoalToCallInfo goalInfo)
		{
			var engine = container.GetInstance<IEngine>();
			engine.Init(container);
			
			var prParser = container.GetInstance<PrParser>();
			var (goal, error) = prParser.GetGoal(goalInfo);
			if (error != null) return (null, null, error);

			var step = (goal!.GoalSteps.Count > 0) ? goal?.GoalSteps[0] : GetDefaultStep();
			
			if (step.Instruction == null)
			{
				step.Instruction = prParser.ParseInstructionFile(step!)!;
			}

			var msa = container.GetInstance<IMemoryStackAccessor>();
			var memoryStack = MemoryStack.New(container, engine);
			msa.Current = memoryStack;

			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			var contextAccessor = container.GetInstance<IPLangContextAccessor>();
			contextAccessor.Current = context;
			context.CallStack = new CallStack();

			var classInstance = container.GetInstance(typeof(Modules.CallGoalModule.Program)) as BaseProgram;
			classInstance.Init(container, goal, step, step.Instruction, contextAccessor);

			var task = classInstance.Run();
			await task;
			var result = task.Result;

			return (engine, result.ReturnValue, result.Error);
		}

		private GoalStep GetDefaultStep()
		{
			var goalStep = new GoalStep();
			goalStep.Name = "Run";
			goalStep.Instruction = GetDefaultInstruction(goalStep);
			return goalStep;
		}

		private Building.Model.Instruction GetDefaultInstruction(GoalStep goalStep)
		{
			return JsonConvert.DeserializeObject<Building.Model.Instruction>(@"""
{
  ""Text"": ""call goal %goalName% %parameters%"",
  ""ModuleType"": ""PLang.Modules.CallGoalModule"",
  ""GenericFunctionType"": ""PLang.Modules.BaseBuilder+GenericFunction"",
  ""Function"": {
    ""Reasoning"": ""The user wants to call a goal dynamically with a goal name and parameters provided as variables. The RunGoal function is designed to call goals with a specified name and parameters, matching the user's intent exactly."",
    ""Name"": ""RunGoal"",
    ""Parameters"": [
      {
        ""Type"": ""PLang.Models.GoalToCallInfo"",
        ""Name"": ""goalInfo"",
        ""Value"": {
          ""Name"": ""%goalName%"",
          ""Parameters"": ""%parameters%""
        }
      },
      {
        ""Type"": ""System.Boolean"",
        ""Name"": ""waitForExecution"",
        ""Value"": true
      },
      {
        ""Type"": ""System.Int32"",
        ""Name"": ""delayWhenNotWaitingInMilliseconds"",
        ""Value"": 50
      },
      {
        ""Type"": ""System.UInt32"",
        ""Name"": ""waitForXMillisecondsBeforeRunningGoal"",
        ""Value"": 0
      },
      {
        ""Type"": ""System.Boolean"",
        ""Name"": ""keepMemoryStackOnAsync"",
        ""Value"": false
      },
      {
        ""Type"": ""System.Boolean"",
        ""Name"": ""isolated"",
        ""Value"": false
      },
      {
        ""Type"": ""System.Boolean"",
        ""Name"": ""disableSystemGoals"",
        ""Value"": false
      },
      {
        ""Type"": ""System.Boolean"",
        ""Name"": ""isEvent"",
        ""Value"": false
      }
    ],
    ""ReturnValues"": null
  },
  ""Properties"": null,
  ""BuilderVersion"": ""0.1.18.1"",
  ""Hash"": null,
  ""SignedMessage"": null
}

""");
		}

		public async Task<(IEngine? Engine, object? Variables, IError? Error)> Run(bool debug = false, bool test = false, string[]? args = null)
		{
			if (test) AppContext.SetSwitch(ReservedKeywords.Test, true);

			this.engine = container.GetInstance<IEngine>();

			
			var msa = container.GetInstance<IMemoryStackAccessor>();
			var memoryStack = MemoryStack.New(container, engine);
			msa.Current = memoryStack;

			var context = new PLangContext(memoryStack, this.engine, ExecutionMode.Console);
			var contextAccessor = container.GetInstance<IPLangContextAccessor>();
			contextAccessor.Current = context;

			this.engine.Init(container);

			var goalToRun = LoadArgsToMemoryStack(args, memoryStack);

			(var vars, var error) = await engine.Run(goalToRun, context);
			return (engine, vars, error);
		}

		private string LoadArgsToMemoryStack(string[]? args, MemoryStack memoryStack)
		{
			string goalToRun = "Start.goal";
			for (int i = 0; args != null && i < args.Length; i++)
			{
				if (args[i].StartsWith("--")) continue;
				if (args[i].Contains("="))
				{
					var value = args[i].Split('=')[1];
					if (value.StartsWith("\"")) value = value.Substring(1).Trim();
					if (value.EndsWith("\"")) value = value.Substring(0, value.Length - 1).Trim();

					var valueAsType = GetValueAsType(value);

					memoryStack.Put(args[i].Split('=')[0].Trim(), valueAsType);
				}
				else if (args[i].ToLower() != "run" && !string.IsNullOrEmpty(args[i]))
				{
					goalToRun = args[i].Trim();
				}
			}
			return goalToRun;
		}

		private object GetValueAsType(string value)
		{
			if (int.TryParse(value, out int i))
			{
				return i;
			}

			if (long.TryParse(value, out long l))
			{
				return l;
			}
			if (double.TryParse(value, out double d))
			{
				return d;
			}

			if (bool.TryParse(value, out bool b))
			{
				return b;
			}

			return value;
		}
	}

}