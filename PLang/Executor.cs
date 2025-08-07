using LightInject;
using PLang.Building;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Interfaces;
using PLang.Models;
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
			AppContext.SetSwitch("InternalGoalRun", true);
			AppContext.SetSwitch("Runtime", true);
			using (var container = new ServiceContainer())
			{
				container.RegisterForPLangConsole(Environment.CurrentDirectory, System.IO.Path.DirectorySeparatorChar.ToString());

				var engine = container.GetInstance<IEngine>();
				engine.Init(container);

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

				var (vars, error) = await engine.RunGoal(goal);
				AppContext.SetSwitch("InternalGoalRun", false);
				return (engine, vars, error);
			}
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

			var debug = args.FirstOrDefault(p => p == "--debug") != null;
			var test = args.FirstOrDefault(p => p == "--test") != null;
			var watch = args.FirstOrDefault(p => p == "watch") != null;

			LoadParametersToAppContext(args);
			if (args.FirstOrDefault(p => p == "exec") != null)
			{
				var list = args.ToList();
				list.Remove("exec");
				args = list.ToArray();
			}

			AppContext.SetSwitch("Builder", (executeType == ExecuteType.Builder));
			AppContext.SetSwitch("Runtime", (executeType == ExecuteType.Runtime));

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
			
			try
			{
				this.engine = container.GetInstance<IEngine>();
				container.GetInstance<MemoryStack>().Clear();
				this.engine.Init(container);

				LoadArgsToMemoryStack(args);

				this.builder = container.GetInstance<IBuilder>();
				var error = await builder.Start(container);
				if (error != null)
				{
					Console.WriteLine(error);
					await this.engine.GetEventRuntime().AppErrorEvents(error);
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


		public async Task<(IEngine? Engine, object? Variables, IError? Error)> Run(bool debug = false, bool test = false, string[]? args = null)
		{
			if (test) AppContext.SetSwitch(ReservedKeywords.Test, true);

			this.engine = container.GetInstance<IEngine>();

			// should create new instance of the container, but for now just clear memoryStack
			container.GetInstance<MemoryStack>().Clear();

			this.engine.Init(container);

			var goalToRun = LoadArgsToMemoryStack(args);

			(var vars, var error) = await engine.Run(goalToRun);
			return (engine, vars, error);
		}

		private string LoadArgsToMemoryStack(string[]? args)
		{
			string goalToRun = "Start.goal";
			for (int i = 0; args != null && i < args.Length; i++)
			{
				if (args[i].StartsWith("--")) continue;
				if (args[i].Contains("="))
				{
					var stack = engine.GetMemoryStack();
					var value = args[i].Split('=')[1];
					if (value.StartsWith("\"")) value = value.Substring(1).Trim();
					if (value.EndsWith("\"")) value = value.Substring(0, value.Length - 1).Trim();

					stack.Put(args[i].Split('=')[0].Trim(), value.Trim());
				}
				else if (args[i].ToLower() != "run" && !string.IsNullOrEmpty(args[i]))
				{
					goalToRun = args[i].Trim();
				}
			}
			return goalToRun;
		}
	}

}