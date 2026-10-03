using PLang;
using app.Utils;
using Path = System.IO.Path;

using var cts = new CancellationTokenSource();

RegisterStartupParameters.Register(args);

// The first Ctrl+C cancels the run: its actors' tasks end Cancelled and the run ends with them. A second one exits at once.
Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;
	if (!cts.IsCancellationRequested) { cts.Cancel(); return; }
	Environment.Exit(130);
};

var executor = new Executor(Path.GetFullPath(Environment.CurrentDirectory));
var result = executor.Start(args, cts.Token).GetAwaiter().GetResult();
// Process-boundary last resort (the permitted Console.* exception): a failed run the app could not
// show (the error show itself failed, or the app never started) must surface here, or it exits
// silently and nothing reports why. A shown failure is not printed twice.
if (!result.Success && result.Error != null && !result.Properties.Contains("shown"))
{
	Console.Error.WriteLine(result.Error.ToString());
}
return result.Success ? 0 : 1;
