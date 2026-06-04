using PLang;
using app.Utils;
using Path = System.IO.Path;

using var cts = new CancellationTokenSource();

RegisterStartupParameters.Register(args);

Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	cts.Cancel();
	Environment.Exit(0);
};

(string currentDirectory, args) = GetCurrentDirectory(args);

var executor = new Executor(Path.GetFullPath(currentDirectory));
var result = executor.Run(args, cts.Token).GetAwaiter().GetResult();
// Process-boundary last resort (the permitted Console.* exception): a failed run
// must surface its error here, or it exits silently and nothing reports why.
if (!result.Success && result.Error != null)
{
	Console.Error.WriteLine(result.Error.Format());
}
return result.Success ? 0 : 1;

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
