using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Exceptions.Handlers;
using PLang.Interfaces;
using PLang.Resources;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.Diagnostics;
using System.IO.Compression;

namespace PLang
{
    public class Executor
	{
		private readonly IServiceContainer container;
		private readonly PrParser prParser;
		private readonly IPLangFileSystem fileSystem;
		private readonly IExceptionHandler exceptionHandler;
		private IEngine engine;

		private IBuilder builder;

		private static FileSystemWatcher? watcher = null;

		public Executor(IServiceContainer container)
		{
			this.container = container;
			
			this.prParser = container.GetInstance<PrParser>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();

		}



		public async Task Execute(string[] args)
		{
			if (args.Length == 0) args = new string[1] { "run" };
			if (args.FirstOrDefault(p => p == "run") == null && args.FirstOrDefault(p => p == "build") == null) args = args.Append("run").ToArray();

			var debug = args.FirstOrDefault(p => p == "--debug") != null;
			if (debug)
			{
				SetupDebug();
			}

			var test = args.FirstOrDefault(p => p == "--test") != null;
			var build = args.FirstOrDefault(p => p == "build") != null;
			var watch = args.FirstOrDefault(p => p == "watch") != null;
			var run = args.FirstOrDefault(p => p == "run") != null;

			LoadParametersToAppContext(args);
			

			if (args.FirstOrDefault(p => p == "exec") != null)
			{
				var list = args.ToList();
				list.Remove("exec");
				args = list.ToArray();

				watch = true;
				build = true;
				run = true;
			}

			if (build)
			{
				await Build();
				if (watch && !run)
				{
					WatchFolder(fileSystem.GoalsPath, "*.goal");
					Console.Read();
				}

			}

			if (run)
			{
				if (watch)
				{
					WatchFolder(fileSystem.GoalsPath, "*.goal");
				}
				await Run(debug, test, args);
			}

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

			AppContext.SetSwitch("skipCode", args.FirstOrDefault(p => p.ToLower() == "--skipcode") != null);
			
		}

		public void SetupDebug()
		{
			var eventsPath = Path.Join(fileSystem.GoalsPath, "events");
			var sendDebugPath = Path.Join(eventsPath, "SendDebug.goal");

			Console.WriteLine("-- Debug mode");
			AppContext.SetSwitch(ReservedKeywords.Debug, true);
			
			if (fileSystem.File.Exists(sendDebugPath)) return;

			if (!fileSystem.File.Exists(sendDebugPath))
			{
				if (!fileSystem.Directory.Exists(eventsPath))
				{
					fileSystem.Directory.CreateDirectory(eventsPath);
				} else
				{
					var logger = container.GetInstance<ILogger>();
					logger.LogError("Installed debugger and may have overwritten your events/Events.goal file. Sorry about that :( Will fix in future.");
				}

				using (MemoryStream ms = new MemoryStream(InternalApps.Debugger))
				using (ZipArchive archive = new ZipArchive(ms))
				{
					archive.ExtractToDirectory(fileSystem.GoalsPath, true);
				}
				return;
			}

			

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

			try
			{

				this.builder = container.GetInstance<IBuilder>();
				await builder.Start(container);
				prParser.LoadAllGoals();
			}
			catch (Exception ex)
			{
				var factory = container.GetInstance<IExceptionHandlerFactory>();
				await factory.CreateHandler().Handle(ex, 500, "error", ex.Message);
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