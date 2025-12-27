using Org.BouncyCastle.Bcpg;
using PLang.Attributes;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using static PLang.Modules.MockModule.Program;

namespace PLang.Modules.EnvironmentModule
{
	[Description("Information about the environment that the software is running, such as machine name, os user name, process id, settings, debug modes, set culture, open file in default app, listen to stdin")]
	public class Program : BaseProgram
	{
		private readonly ISettings settings;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettingsRepositoryFactory settingsRepositoryFactory;
		private readonly IEngine engine;

		public Program(ISettings settings, IPLangFileSystem fileSystem, ISettingsRepositoryFactory settingsRepositoryFactory, IEngine engine)
		{
			this.settings = settings;
			this.fileSystem = fileSystem;
			this.settingsRepositoryFactory = settingsRepositoryFactory;
			this.engine = engine;
		}



		public async Task<IError> SetSettingsDbPath(string path)
		{
			return settingsRepositoryFactory.CreateHandler().SetSystemDbPath(path);
		}

		public async Task<string> GetAppName()
		{
			string appName = fileSystem.GoalsPath.Substring(fileSystem.GoalsPath.LastIndexOf(Path.DirectorySeparatorChar)+1);
			return appName;
		}

		public async Task EndApp()
		{
			Environment.Exit(0);
			throw new Exceptions.RuntimeGoalEndException("End app", null);
		}

		public async Task<string?> GetEnvironmentVariable(string key)
		{
			return Environment.GetEnvironmentVariable(key);
		}

		public async Task<string> GetMachineName()
		{
			return Environment.MachineName;
		}

		public async Task<string> GetUserName()
		{
			return Environment.UserName;
		}

		public async Task<int> GetProcessId()
		{
			return Environment.ProcessId;
		}

		public async Task<string> GetOSDescription()
		{
			return RuntimeInformation.OSDescription;
		}

		public async Task<bool> IsInCSharpDebugMode()
		{
			AppContext.TryGetSwitch(ReservedKeywords.CSharpDebug, out bool result);
			return result;
		}

		public async Task<CultureInfo> GetCurrentCulture()
		{
			return CultureInfo.CurrentCulture;
		}

		[Description("Get active mocks on engine, used for testing")]
		public async Task<List<MockData>> GetMocks()
		{
			return context.Mocks;
		}

		public async Task SetEnvironment(string name)
		{
			if (name.Equals("test", StringComparison.OrdinalIgnoreCase))
			{
				AppContext.SetSwitch(ReservedKeywords.Test, true);
			}
			engine.Environment = name;
		}

		[Description("Make sure to convert user code to valid BCP 47 code, language-country")]
		public async Task SetCultureLanguageCode(string code = "en-US")
		{
			var ci = new CultureInfo(code);
			Thread.CurrentThread.CurrentCulture = ci;
			Thread.CurrentThread.CurrentUICulture = ci;
			CultureInfo.DefaultThreadCurrentCulture = ci;
			CultureInfo.DefaultThreadCurrentUICulture = ci;
		}

		[Description("Make sure to convert user code to valid BCP 47 code, language-country")]
		public async Task SetCultureUILanguageCode(string code = "en-US")
		{
			var ci = new CultureInfo(code);
			Thread.CurrentThread.CurrentUICulture = ci;
			CultureInfo.DefaultThreadCurrentUICulture = ci;
		}

		public async Task<IError?> OpenFileInDefaultApp(string filePath)
		{
			var absolutePath = GetPath(filePath);
			using var process = Process.Start(new ProcessStartInfo(absolutePath) { UseShellExecute = true });
			return null;
		}



		public async Task KeepAlive(string message = "App KeepAlive")
		{
			base.KeepAlive(this, message);
		}

		[BuildRunner("InstallNpm")]
		public async Task InstallNpm(string packageName)
		{
			//Install npm is being executed at build time
			return;
		}

		public async Task<List<PLang.Models.Setting>> GetSettings()
		{
			return settings.GetAllSettings().ToList();
		}


		public async Task SetDebugMode()
		{
			context.ShowErrorDetails = true;
			context.DebugMode = true;
		}

		public async Task RemoveDebugMode()
		{
			context.ShowErrorDetails = false;
			context.DebugMode = false;
		}
		public async Task<bool> IsInDebugMode()
		{
			return context.DebugMode;
		}

		public async Task ShowErrorDetails()
		{
			context.ShowErrorDetails = true;
		}

		public async Task HideErrorDetails()
		{
			context.ShowErrorDetails = false;
		}
		public async Task<bool> CanSeeErrorDetails()
		{
			return context.ShowErrorDetails;
		}


		// In EnvironmentModule

		[Description("Listen to stdin asynchronously, calling the specified goal for each complete message")]
		public async Task ListenToStdin(
			[Description("Goal to call when message received")] GoalToCallInfo goalToCall,
			[Description("Character(s) that mark end of message")] string delimiter = "\n")
		{
			var reader = new StreamReader(Console.OpenStandardInput());
			var buffer = new StringBuilder();
			
			_ = Task.Run(async () =>
			{
				var charBuffer = new char[1];
				while (true)
				{
					var read = await reader.ReadAsync(charBuffer, 0, 1);
					if (read == 0) break; // stdin closed

					buffer.Append(charBuffer[0]);
					var content = buffer.ToString();

					if (content.EndsWith(delimiter))
					{
						var message = content[..^delimiter.Length]; // remove delimiter
						buffer.Clear();

						if (!string.IsNullOrWhiteSpace(message))
						{
							goalToCall.Parameters.AddOrReplace("reader", reader);
							goalToCall.Parameters.AddOrReplace("message", message);
							await engine.RunGoal(goalToCall, goal, context);
						}
					}
				}
			});

			KeepAlive(reader, "StdIn");
		}
	}
}
