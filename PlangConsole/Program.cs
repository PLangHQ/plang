
using LightInject;
using PLang;
using PLang.Container;
using PLang.Interfaces;
using PLang.Utils;

using var cts = new CancellationTokenSource();

(var builder, var runtime) = RegisterStartupParameters.Register(args);

Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	cts.Cancel();
	Environment.Exit(0);
};

// Both builder and runtime go through the same v3 engine path
(string currentDirectory, args) = GetCurrentDirectory(args);

var container = new ServiceContainer();
if (builder)
{
	AppContext.SetSwitch("Builder", true);
	container.RegisterForPLangBuilderConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());
}
else
{
	container.RegisterForPLangConsole(currentDirectory, Path.DirectorySeparatorChar.ToString());
}

var pLanguage = new Executor(container);
var result = pLanguage.Run(args, cts.Token).GetAwaiter().GetResult();
if (!result.Success && result.Error != null)
{
	Console.Error.WriteLine(result.Error.Format());
}
container.Dispose();


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
