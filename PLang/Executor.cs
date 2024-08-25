using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors.Handlers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Resources;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using PLang.Errors;
using PLang.Errors.Builder;

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

		private static FileSystemWatcher? watcher = null;

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

		public async Task Execute(string[] args, ExecuteType executeType)
		{
			var version = args.FirstOrDefault(p => p == "--version") != null;
			if (version)
			{
				var assembly = Assembly.GetAssembly(this.GetType());

				Console.WriteLine("PLang version: " + assembly.GetName().Version.ToString());
				return;
			}

			var debug = args.FirstOrDefault(p => p == "--debug") != null;
			if (debug)
			{
				SetupDebug();
			}

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
				await Build();
				if (watch)
				{
					WatchFolder(fileSystem.GoalsPath, "*.goal");
					Console.Read();
				}
				return;
			}

			if (watch)
			{
				WatchFolder(fileSystem.GoalsPath, "*.goal");
			}
			await Run(debug, test, args);


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

			var eventsPath = fileSystem.Path.Join(fileSystem.GoalsPath, "events", "external", "plang", "runtime");

			if (fileSystem.Directory.Exists(eventsPath)) return;

			fileSystem.Directory.CreateDirectory(eventsPath);

			using (MemoryStream ms = new MemoryStream(InternalApps.Runtime))
			using (ZipArchive archive = new ZipArchive(ms))
			{
				archive.ExtractToDirectory(fileSystem.GoalsPath, true);
			}
			return;

		}


		private void WatchFolder(string path, string filter)
		{
			if (watcher == null)
			{
				watcher = new FileSystemWatcher();
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
				var container = new ServiceContainer();
				container.RegisterForPLangConsole(Environment.CurrentDirectory, Environment.CurrentDirectory);

				var pLanguage = new Executor(container);
				await pLanguage.Build();

				prParser.ForceLoadAllGoals();
			};


			watcher.Renamed += async (object sender, RenamedEventArgs e) =>
			{
				var container = new ServiceContainer();
				container.RegisterForPLangConsole(Environment.CurrentDirectory, Environment.CurrentDirectory);

				var pLanguage = new Executor(container);
				await pLanguage.Build();

				prParser.ForceLoadAllGoals();
			}; ;

			// Begin watching.
			watcher.EnableRaisingEvents = true;

		}

		public async Task Build()
		{
			var factory = container.GetInstance<IErrorHandlerFactory>();
			var handler = factory.CreateHandler();
			try
			{

				this.builder = container.GetInstance<IBuilder>();
				var error = await builder.Start(container);
				if (error != null)
				{
					(var isHandled, var errorHandler) = await handler.Handle(error);
					if (errorHandler != null && errorHandler is not ErrorHandled)
					{
						await handler.ShowError(error);
					}
				}
				else
				{
					prParser.LoadAllGoals();
				}
			}
			catch (Exception ex)
			{
				var error = new ExceptionError(ex);

				await handler.ShowError(error);
			}
		}


		public async Task<IEngine> Run(bool debug = false, bool test = false, string[]? args = null)
		{
			if (test) AppContext.SetSwitch(ReservedKeywords.Test, true);

			this.engine = container.GetInstance<IEngine>();

			// should create new instance of the container, but for now just clear memoryStack
			container.GetInstance<MemoryStack>().Clear();

			this.engine.Init(container);

			var goalsToRun = new List<string>();
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
					goalsToRun.Add(args[i].Trim());
				}
			}

			await engine.Run(goalsToRun);
			return engine;
		}


	}

}