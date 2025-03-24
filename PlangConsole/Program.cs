
using LightInject;
using PLang;
using PLang.Container;
using PLang.Interfaces;
using PLang.Utils;
using System.ComponentModel;
using static PLang.Executor;


(var builder, var runtime) = RegisterStartupParameters.Register(args);

if (builder)
{
	var container = new ServiceContainer();
	container.RegisterForPLangBuilderConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());


	var pLanguage = new Executor(container);
	pLanguage.Execute(args, ExecuteType.Builder).GetAwaiter().GetResult();
}

if (runtime)
{
	(string currentDirectory, args) = GetCurrentDirectory(args);

	var container = new ServiceContainer();
	container.RegisterForPLangConsole(currentDirectory, Path.DirectorySeparatorChar.ToString());

	var context = container.GetInstance<PLangAppContext>();

	var fileAccessHandler = container.GetInstance<PLang.SafeFileSystem.IFileAccessHandler>();
	fileAccessHandler.GiveAccess(Environment.CurrentDirectory, Path.Join(AppContext.BaseDirectory, "os"));

	var pLanguage = new Executor(container);
	pLanguage.Execute(args, ExecuteType.Runtime).GetAwaiter().GetResult();
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