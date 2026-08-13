
using LightInject;
using Microsoft.Extensions.Logging;
using PLang;
using PLang.Container;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static PLang.Executor;


(var builder, var runtime) = RegisterStartupParameters.Register(args);

// Set by the runtime block below. Clearing KeepAlive lets Engine.KeepAlive() unwind so
// Execute returns and container.Dispose() closes the db connections. Killing the process
// outright (Environment.Exit / SIGKILL) can leave the sqlite files corrupted.
Action? gracefulShutdown = null;

Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;

	if (gracefulShutdown != null)
	{
		gracefulShutdown();
		return;
	}

	Environment.Exit(0);
};


if (builder)
{
	AppContext.SetSwitch("Builder", true);

	var container = new ServiceContainer();
	container.RegisterForPLangBuilderConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());


	var pLanguage = new Executor(container);
	var result = pLanguage.Execute(args, ExecuteType.Builder).GetAwaiter().GetResult();
	if (result.Error != null)
	{
		var logger = container.GetInstance<ILogger>();
		logger.LogError(result.Error.ToString());

		// A failed build used to exit 0, so `plang build && deploy` deployed a broken app and CI
		// stayed green. The build summary is printed by the builder; this makes it machine readable.
		Environment.ExitCode = 1;
	}

	container.Dispose();
}

if (runtime)
{
	(string currentDirectory, args) = GetCurrentDirectory(args);

	var container = new ServiceContainer();
	container.RegisterForPLangConsole(currentDirectory, Path.DirectorySeparatorChar.ToString());

	var context = container.GetInstance<PLangAppContext>();

	var fileAccessHandler = container.GetInstance<PLang.SafeFileSystem.IFileAccessHandler>();
	fileAccessHandler.GiveAccess(Environment.CurrentDirectory, Path.Join(AppContext.BaseDirectory, "os"));
	var engine = container.GetInstance<IEngine>();
	engine.Name = "Console";

	// Ctrl+C only reaches Console.CancelKeyPress when a terminal is attached, and SIGTERM
	// (kill, systemctl stop, container stop) was not handled at all - so a running webserver
	// could not be stopped without SIGKILL.
	int shuttingDown = 0;
	gracefulShutdown = () =>
	{
		if (Interlocked.Exchange(ref shuttingDown, 1) == 1) return;

		Console.WriteLine("Shutdown signal received, stopping...");
		context.Remove("KeepAlive");

		Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromSeconds(20));
			Console.WriteLine("Shutdown did not complete in 20s, exiting.");
			Environment.Exit(0);
		});
	};

	using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
	{
		ctx.Cancel = true;
		gracefulShutdown();
	});
	using var sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx =>
	{
		ctx.Cancel = true;
		gracefulShutdown();
	});

	var pLanguage = new Executor(container);
	var result = pLanguage.Execute(args, ExecuteType.Runtime).GetAwaiter().GetResult();
	if (result.Error != null)
	{
		var logger = container.GetInstance<ILogger>();
		logger.LogError(result.Error.ToFormat("text").ToString());
	}
	container.Dispose();
}


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