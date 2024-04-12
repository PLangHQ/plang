
using LightInject;
using PLang;
using PLang.Container;
using PLang.Utils;


RegisterStartupParameters.Register(args);
var container = new ServiceContainer();
container.RegisterForPLangConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());

var pLanguage = new Executor(container);
	pLanguage.Execute(args).GetAwaiter().GetResult();

