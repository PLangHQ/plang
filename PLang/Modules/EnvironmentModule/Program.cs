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
using static PLang.Modules.MockModule.Program;

namespace PLang.Modules.EnvironmentModule;

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

	[Description("Get current process memory usage details")]
	public async Task<MemoryInfo> GetMemoryInfo(bool forceGarbageCollection = false)
	{
		var process = Process.GetCurrentProcess();

		if (forceGarbageCollection)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		return new MemoryInfo
		{
			RssMB = process.WorkingSet64 / 1024 / 1024,
			PrivateMB = process.PrivateMemorySize64 / 1024 / 1024,
			VirtualMB = process.VirtualMemorySize64 / 1024 / 1024,
			GcHeapMB = GC.GetTotalMemory(false) / 1024 / 1024,
			Gen0 = GC.CollectionCount(0),
			Gen1 = GC.CollectionCount(1),
			Gen2 = GC.CollectionCount(2),
			Timestamp = DateTime.UtcNow
		};
	}
	public class MemoryInfo
	{
		public long RssMB { get; set; }
		public long PrivateMB { get; set; }
		public long VirtualMB { get; set; }
		public long GcHeapMB { get; set; }
		public int Gen0 { get; set; }
		public int Gen1 { get; set; }
		public int Gen2 { get; set; }
		public DateTime Timestamp { get; set; }
	}
}
