
using LightInject;
using PLang;
using System.Diagnostics;
using PLang.Utils;

var debug = args.FirstOrDefault(p => p == "--csdebug") != null;
if (debug && !Debugger.IsAttached)
{
	Debugger.Launch();
	//AppContext.SetSwitch(ReservedKeywords.Debug, true);
}
var container = new ServiceContainer();
container.RegisterForPLangConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());

if (args.FirstOrDefault("stuff") != null)
{
	AppContext.SetData("stuff", "stuff");
}

var pLanguage = new Executor(container);
	pLanguage.Execute(args).GetAwaiter().GetResult();

