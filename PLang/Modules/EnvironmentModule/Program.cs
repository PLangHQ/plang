using PLang.Interfaces;
using PLang.Utils;
using System.ComponentModel;
using System.Globalization;

namespace PLang.Modules.EnvironmentModule
{
	[Description("Information about the environment that the software is running, such as machine name, os user name, process id, settings, debug modes, set culture")]
	public class Program : BaseProgram
	{
		private readonly ISettings settings;
		private readonly IPLangFileSystem fileSystem;

		public Program(ISettings settings, IPLangFileSystem fileSystem)
		{
			this.settings = settings;
			this.fileSystem = fileSystem;
		}

		public async Task<string> GetAppName()
		{
			string appName = fileSystem.GoalsPath.Substring(fileSystem.GoalsPath.LastIndexOf(Path.DirectorySeparatorChar)+1);
			return appName;
		}

		public async Task EndApp()
		{
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
	}
}
