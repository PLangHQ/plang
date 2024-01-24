using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building;
using PLang.Building.Parsers;
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
		private readonly ISettings settings;
		private readonly IServiceContainer container;
		private readonly PrParser prParser;
		private readonly IErrorHelper errorHelper;
		private readonly IPLangFileSystem fileSystem;
		private IEngine engine;

		private IBuilder builder;

		private static FileSystemWatcher? watcher = null;

		public Executor(IServiceContainer container)
		{
			this.container = container;
			this.settings = container.GetInstance<ISettings>();
			this.prParser = container.GetInstance<PrParser>();
			this.errorHelper = container.GetInstance<IErrorHelper>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
			
		}



		public async Task Execute(string[] args)
		{
			if (args.Length == 0) args = new string[1] { "build" };
			
			var debug = args.FirstOrDefault(p => p == "--debug") != null;
			if (debug)
			{
				SetupDebug();
			}

			var test = args.FirstOrDefault(p => p == "--test") != null;
			var build = args.FirstOrDefault(p => p == "build") != null;
			var watch = args.FirstOrDefault(p => p == "watch") != null;
			var run = args.FirstOrDefault(p => p == "run") != null;

			var loggerLovel = args.FirstOrDefault(p => p.StartsWith("--logger"));
			if (loggerLovel != null)
			{
				AppContext.SetData("--logger", loggerLovel.Replace("--logger=", ""));
			}

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

		public void SetupDebug()
		{
			var eventsPath = Path.Join(fileSystem.GoalsPath, "Events");
			var sendDebugPath = Path.Join(eventsPath, "SendDebug.goal");

			Console.WriteLine("-- Debug mode");
			AppContext.SetSwitch(ReservedKeywords.Debug, true);
			
			if (fileSystem.File.Exists(sendDebugPath)) return;

			if (!fileSystem.Directory.Exists(eventsPath))
			{
				fileSystem.Directory.CreateDirectory(eventsPath);
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
				await errorHelper.ShowFriendlyErrorMessage(ex, callBackForAskUser: Build);				
			}
		}


		public async Task<IEngine> Run(bool debug = false, bool test = false, string[]? args = null)
		{
			
		
			if (test) AppContext.SetSwitch(ReservedKeywords.Test, true);

			this.engine = container.GetInstance<IEngine>();
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