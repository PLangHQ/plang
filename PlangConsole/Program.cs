
using LightInject;
using PLang;
using PLang.Container;
using PLang.Interfaces;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using static PLang.Executor;

await MainAsync();

async Task MainAsync()
{
	//first thing to check is for csdebug, we want to enter into debug mode ASAP
	var csdebug = args.FirstOrDefault(p => p == "--csdebug") != null;
	if (csdebug && !Debugger.IsAttached)
	{
		Debugger.Launch();
	}
	//	(var builder, var runtime) = RegisterStartupParameters.Register(args);

	var poolSettings = new AppPoolSettings(Environment.CurrentDirectory, "console");
	using var appPool = new AppPool(poolSettings);

	(var builder, var runtime) = GetBuilderAndRuntime(args);

	var app = await appPool.Rent(args.ToList());
	if (builder)
	{
		var result = await app.Build();
		if (result.Error != null)
		{
			app.Output.Write(result);
		}


		var container = new ServiceContainer();
		container.RegisterForPLangBuilderConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());


		var pLanguage = new Executor(container);
		pLanguage.Execute(args, ExecuteType.Builder).GetAwaiter().GetResult();

		container.Dispose();
	}

	if (runtime)
	{
		(var data, var error) = await app.Start();
		if (error != null)
		{
			app.Output.Write(error);
		}
		if (data != null)
		{
			app.Output.Write(data);
		}

		(string currentDirectory, args) = GetCurrentDirectory(args);

		var container = new ServiceContainer();
		container.RegisterForPLangConsole(currentDirectory, Path.DirectorySeparatorChar.ToString());

		var context = container.GetInstance<PLangAppContext>();

		var fileAccessHandler = container.GetInstance<PLang.SafeFileSystem.IFileAccessHandler>();
		fileAccessHandler.GiveAccess(Environment.CurrentDirectory, Path.Join(AppContext.BaseDirectory, "os"));

		var pLanguage = new Executor(container);
		await pLanguage.Execute(args, ExecuteType.Runtime);

		container.Dispose();
	}

	appPool.Return(app);

	(string, string[]) GetCurrentDirectory(string[] args)
	{
		var goalPath = args.FirstOrDefault(p => p.StartsWith("/apps/"));
		if (goalPath == null) return (Environment.CurrentDirectory, args);

		if (File.Exists(Path.Join(Environment.CurrentDirectory, goalPath + ".goal")))
		{
			return (Environment.CurrentDirectory, args);
		}

		if (File.Exists(Path.Join(AppContext.BaseDirectory, "OS", goalPath + ".goal")))
		{
			var goalPathAdj = goalPath.AdjustPathToOs();
			string appPath = goalPathAdj.Replace("apps" + Path.DirectorySeparatorChar, "").TrimStart(Path.DirectorySeparatorChar);
			string appName = appPath.Substring(0, appPath.IndexOf(Path.DirectorySeparatorChar));
			string goalName = goalPathAdj.Replace(Path.Join("apps", appName), "").TrimStart(Path.DirectorySeparatorChar);
			int idx = Array.IndexOf(args, goalPath);
			args[idx] = goalName;

			return (Path.Join(AppContext.BaseDirectory, "OS", "apps", appName), args);
		}

		return (Environment.CurrentDirectory, args);

	}
}

(bool, bool) GetBuilderAndRuntime(string[] args)
{
	bool builder = false;
	bool runtime = false;

	var build = args.FirstOrDefault(p => p == "build") != null;
	if (build)
	{
		builder = true;
		runtime = false;
	}
	else
	{
		builder = false;
		runtime = true;
	}
	var exec = args.FirstOrDefault(p => p == "exec") != null;
	if (exec)
	{
		builder = true;
		runtime = true;
	}
	return (builder, runtime);
}