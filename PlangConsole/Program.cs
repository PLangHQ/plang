using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang;
using PLang.Building.Model;
using PLang.Container;
using PLang.Interfaces;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using PLang.Services.OutputStream.Messages;
using PLang.Errors;
using PLang.Utils;
using System.Collections;
using System.ComponentModel;
using static PLang.Executor;


(var builder, var runtime) = RegisterStartupParameters.Register(args);

Console.CancelKeyPress += (_, e) =>
{
	e.Cancel = true;

	Environment.Exit(0);
};


if (builder)
{
	AppContext.SetSwitch("Builder", true);

	var container = new ServiceContainer();
	container.RegisterForPLangBuilderConsole(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString());


	var pLanguage = new Executor(container);
	var result = pLanguage.Execute(args, ExecuteType.Builder).GetAwaiter().GetResult();
	if (result.Error != null)
	{
		var logger = container.GetInstance<ILogger>();
		logger.LogError(result.Error.ToString());
	}

	container.Dispose();
}

if (runtime)
{
	(string currentDirectory, args) = GetCurrentDirectory(args);

	var container = new ServiceContainer();

	// Use MinimalContainer for cleaner bootstrap
	// Services can be configured via system/Run.goal using inject command
	container.RegisterBootstrap(currentDirectory, Path.DirectorySeparatorChar.ToString());

	var context = container.GetInstance<PLangAppContext>();

	var fileAccessHandler = container.GetInstance<PLang.SafeFileSystem.IFileAccessHandler>();
	fileAccessHandler.GiveAccess(Environment.CurrentDirectory, Path.Join(AppContext.BaseDirectory, "os"));
	var engine = container.GetInstance<IEngine>();
	engine.Name = "Console";
	engine.Init(container);

	var pLanguage = new Executor(container);
	var result = pLanguage.Execute(args, ExecuteType.Runtime).GetAwaiter().GetResult();
	if (result.Error != null)
	{
		await ErrorHelper.OutputError(engine, result.Error);
	}

	// Output return value directly to sink (avoids CallStack requirement)
	if (result.Variables != null)
	{

		// todo: this is not working, reason is, that context is null here when I get engine back
		// need to find out why that is. We dont want to print out anything if the variable is empty.
		
		object? isEmpty = false;
		(var condition, var error) = result.engine.Modules.Get<PLang.Modules.ConditionalModule.Program>(Goal.EndOfApp);
		if (condition != null)
		{
			(isEmpty, error) = await condition.IsEmpty(result.Variables);
		}

		// so for now I'll check if it's a list and see if it is empty, we should be using conditional module for it
		if (result.Variables is ObjectValue ov && ov.Value is IList list && list.Count == 0)
		{
			isEmpty = true;
		}

		if (isEmpty == null || !(bool)isEmpty)
		{
			JsonSerializerSettings jsonSerializer = new JsonSerializerSettings()
			{
				ObjectCreationHandling = ObjectCreationHandling.Replace,
				Converters = { new JsonObjectValueConverter() }
			};
			var json = JsonConvert.SerializeObject(result.Variables, Formatting.Indented, jsonSerializer);
			var textMessage = new TextMessage(json);
			engine.UserSink.SendAsync(textMessage).GetAwaiter().GetResult();
		}
	}

	container.Dispose();
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