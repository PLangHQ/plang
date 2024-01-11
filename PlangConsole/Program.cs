
using PLang;
using System.Diagnostics;


var debug = args.FirstOrDefault(p => p == "--debug") != null;
if (debug && !Debugger.IsAttached)
{
	Debugger.Launch();
}

var pLanguage = new Executor();
	pLanguage.Execute(args).GetAwaiter().GetResult();

