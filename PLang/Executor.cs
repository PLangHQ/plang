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
		private readonly ServiceContainer container;
		private readonly PrParser prParser;
		private readonly IErrorHelper errorHelper;
		private readonly IPLangFileSystem fileSystem;

		private static FileSystemWatcher? watcher = null;

		public Executor()
		{

			container = new ServiceContainer();
			container.RegisterForPLangConsole(Environment.CurrentDirectory, Environment.CurrentDirectory);
			
			this.settings = container.GetInstance<ISettings>();
			this.prParser = container.GetInstance<PrParser>();
			this.errorHelper = container.GetInstance<IErrorHelper>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
		}



		public async Task Execute(string[] args)
		{
			if (args.Length == 0) args = new string[1] { "build" };
			
			var debug = args.FirstOrDefault(p => p == "--debug") != null;
			var test = args.FirstOrDefault(p => p == "--test") != null;
			var build = args.FirstOrDefault(p => p == "build") != null;
			var watch = args.FirstOrDefault(p => p == "watch") != null;
			var run = args.FirstOrDefault(p => p == "run") != null;

			if (debug)
			{
				SetupDebug();
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
					WatchFolder(container, settings.GoalsPath, "*.goal");
					Console.Read();
				}

			}

			if (run)
			{
				if (watch)
				{
					WatchFolder(container, settings.GoalsPath, "*.goal");
				}
				await Run(debug, test, args);
			}

		}

		public void SetupDebug()
		{
			var eventsPath = Path.Join(settings.GoalsPath, "Events");
			var sendDebugPath = Path.Join(eventsPath, "SendDebug.goal");

			Console.WriteLine("-- Debug mode");
			AppContext.SetSwitch(ReservedKeywords.Debug, true);
			if (!Debugger.IsAttached)
			{
				Debugger.Launch();
			}


			if (fileSystem.File.Exists(sendDebugPath)) return;

			if (!fileSystem.Directory.Exists(eventsPath))
			{
				fileSystem.Directory.CreateDirectory(eventsPath);
				using (MemoryStream ms = new MemoryStream(InternalApps.Debugger))
				using (ZipArchive archive = new ZipArchive(ms))
				{
					archive.ExtractToDirectory(settings.GoalsPath);
				}
				return;
			}

			

		}

		private static void WatchFolder(ServiceContainer container, string path, string filter)
		{
			if (watcher == null)
			{
				watcher = new FileSystemWatcher();
			}
			var fileSystem = container.GetInstance<IPLangFileSystem>();
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
				var pLanguage = new Executor();
				await pLanguage.Build();
			};


			watcher.Renamed += async (object sender, RenamedEventArgs e) =>
			{
				var pLanguage = new Executor();
				await pLanguage.Build();
			}; ;

			// Begin watching.
			watcher.EnableRaisingEvents = true;

		}

		public async Task Build()
		{

			try
			{
				var builder = container.GetInstance<IBuilder>();
				await builder.Start(container);
				prParser.LoadAllGoals();
			}
			catch (Exception ex)
			{
				await errorHelper.ShowFriendlyErrorMessage(ex, callBackForAskUser: Build);				
			}
		}


		public async Task<IEngine> Run(bool debug = false, bool test = false, string[] args = null)
		{
			IEngine engine = container.GetInstance<IEngine>();
			engine.Init(container);
		
			if (test) AppContext.SetSwitch(ReservedKeywords.Test, true);

			var goalsToRun = new List<string>();
			for (int i = 0; args != null && i < args.Length; i++)
			{
				if (args[i].StartsWith("--")) continue;
				if (args[i].Contains("="))
				{
					var stack = engine.GetMemoryStack();
					var value = args[i].Split('=')[1];
					if (value.StartsWith("\"")) value = value.Substring(1);
					if (value.EndsWith("\"")) value = value.Substring(0, value.Length - 1);

					stack.Put(args[i].Split('=')[0], value);
				}
				else if (args[i].ToLower() != "run" && !string.IsNullOrEmpty(args[i]))
				{
					goalsToRun.Add(args[i]);
				}
			}

			await engine.Run(goalsToRun);
			return engine;
		}


	}

}