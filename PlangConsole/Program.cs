using LightInject;
using PLang;
using PLang.Container;
using PLang.Utils;
using static PLang.Executor;


var (builder, runtime) = RegisterStartupParameters.Register(args);

if (builder)
{
    var container = new ServiceContainer();
    container.RegisterForPLangBuilderConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());

    var pLanguage = new Executor(container);
    pLanguage.Execute(args, ExecuteType.Builder).GetAwaiter().GetResult();
}

if (runtime)
{
    var container = new ServiceContainer();
    container.RegisterForPLangConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());

    var pLanguage = new Executor(container);
    pLanguage.Execute(args, ExecuteType.Runtime).GetAwaiter().GetResult();
}