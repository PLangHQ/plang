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

var executor = new Executor(Path.GetFullPath(Environment.CurrentDirectory));
var result = executor.Start(args, cts.Token).GetAwaiter().GetResult();
// Process-boundary last resort (the permitted Console.* exception): a failed run the app could not
// show (the error show itself failed, or the app never started) must surface here, or it exits
// silently and nothing reports why. A shown failure is not printed twice.
if (!result.Success && result.Error != null && !result.Property.Contains("shown"))
{
	Console.Error.WriteLine(result.Error.ToString());
}
return result.Success ? 0 : 1;
