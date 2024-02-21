
using LightInject;
using PLang;
using System.Diagnostics;
using PLang.Container;
using PLang.Utils;

var debug = args.FirstOrDefault(p => p == "--csdebug") != null;
if (debug && !Debugger.IsAttached)
{
	Debugger.Launch();
	AppContext.SetSwitch(ReservedKeywords.CSharpDebug, true);
}
var build = args.FirstOrDefault(p => p == "build") != null;
if (build)
{
	AppContext.SetSwitch("builder", true);
} else
{
	AppContext.SetSwitch("runtime", true);
}
var container = new ServiceContainer();
container.RegisterForPLangConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());

var pLanguage = new Executor(container);
	pLanguage.Execute(args).GetAwaiter().GetResult();

