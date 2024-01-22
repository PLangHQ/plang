
using LightInject;
using PLang;
using System.Diagnostics;
using PLang.Utils;

var debug = args.FirstOrDefault(p => p == "--csdebug") != null;
if (debug && !Debugger.IsAttached)
{
	Debugger.Launch();
}
var container = new ServiceContainer();
container.RegisterForPLangConsole(Environment.CurrentDirectory, Environment.CurrentDirectory);

var pLanguage = new Executor(container);
	pLanguage.Execute(args).GetAwaiter().GetResult();

