using Org.BouncyCastle.Bcpg;
using PLang.Attributes;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PLang.Modules.EnvironmentModule
{
	[Description("Information about the environment that the software is running, such as machine name, os user name, process id, settings, debug modes, set culture, open file in default app, npm install")]
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
		public async Task<string> GetAllSettings()
		{
			return settings.SerializeSettings();
		}
		public async Task<string> GetOSDescription()
		{
			return RuntimeInformation.OSDescription;
		}

		public async Task<bool> IsInDebugMode()
		{
			AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool result);
			return result;
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
	}
}
