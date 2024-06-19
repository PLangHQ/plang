using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Container;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using static PLang.Modules.BaseBuilder;
using Instruction = PLang.Building.Model.Instruction;
using PLang.Errors;
using PLang.Errors.Runtime;
using System;
using PLang.SafeFileSystem;
using OpenQA.Selenium.DevTools.V120.Emulation;
using PLang.Errors.AskUser;

namespace PLang.Modules
{

	public abstract class BaseProgram
	{
		private HttpListenerContext? _listenerContext = null;

		protected MemoryStack memoryStack;
		protected Goal goal;
		protected GoalStep goalStep;
		protected Instruction instruction;
		protected PLangAppContext context;
		protected VariableHelper variableHelper;
		protected ITypeHelper typeHelper;
		protected GenericFunction function;
		private ILlmServiceFactory llmServiceFactory;
		private ILogger logger;
		private IServiceContainer container;
		private IAppCache appCache;
		private IOutputStreamFactory outputStreamFactory;
		private IPLangFileSystem fileSystem;
		private MethodHelper methodHelper;
		private FileAccessHandler fileAccessHandler;
		private IAskUserHandlerFactory askUserHandlerFactory;
		private ISettings settings;
		public HttpListenerContext HttpListenerContext
		{
			get
			{
				if (_listenerContext == null) throw new NullReferenceException("_listenerContext is null. It should not be null");

				return _listenerContext;
			}
		}
		public Goal Goal { get { return goal; } }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public BaseProgram()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		{
		}

		public void Init(IServiceContainer container, Goal goal, GoalStep step,
			Instruction instruction, MemoryStack memoryStack, ILogger logger,
			PLangAppContext context, ITypeHelper typeHelper, ILlmServiceFactory llmServiceFactory, ISettings settings,
			IAppCache appCache, HttpListenerContext? httpListenerContext)
		{
			this.container = container;

			this.logger = logger;
			this.memoryStack = memoryStack;
			this.context = context;
			this.appCache = appCache;
			this.outputStreamFactory = container.GetInstance<IOutputStreamFactory>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
			this.askUserHandlerFactory = container.GetInstance<IAskUserHandlerFactory>();
			this.settings = container.GetInstance<ISettings>();
			_listenerContext = httpListenerContext;

			this.goal = goal;
			this.goalStep = step;
			this.instruction = instruction;

			variableHelper = new VariableHelper(context, memoryStack, settings);
			this.typeHelper = typeHelper;
			this.llmServiceFactory = llmServiceFactory;
			methodHelper = new MethodHelper(goalStep, variableHelper, memoryStack, typeHelper, llmServiceFactory);
			fileAccessHandler = container.GetInstance<FileAccessHandler>();
		}

		public virtual async Task<IError?> Run()
		{
			var functions = instruction.GetFunctions();
			foreach (var function in functions)
			{
				var error = await RunFunction(function);
				if (error != null) return error;
			}
			return null;
		}

		public async Task<IError?> RunFunction(GenericFunction function)
		{
			Dictionary<string, object?> parameterValues = null;
			this.function = function; // this is to give sub classes access to current function running.
			try
			{
				MethodInfo method = await methodHelper.GetMethod(this, function);
				logger.LogDebug("Method:{0}.{1}({2})", goalStep.ModuleType, method.Name, method.GetParameters());

				//TODO: Should move this caching check up the call stack. code is doing to much work before returning cache
				if (await LoadCached(method, function)) return null;

				if (method.ReturnType != typeof(Task) && method.ReturnType.BaseType != typeof(Task))
				{
					return new Error($"The method {method.Name} does not return Task. Method that are called must return Task");
				}

				parameterValues = methodHelper.GetParameterValues(method, function);
				logger.LogTrace("Parameters:{0}", parameterValues);


				// This is for memoryStack event handler. Should find a better way
				context.AddOrReplace(ReservedKeywords.Goal, goal);

				var task = method.Invoke(this, parameterValues.Values.ToArray()) as Task;
				if (task == null)
				{
					logger.LogWarning("Method called is not an async function. Make sure it is defined as 'public async Task' or 'public async Task<YourReturnType>'");
					return null;
				}

				if (goalStep.WaitForExecution)
				{
					try
					{
						await task;
					}
					catch { }
				}

				if (task.Status == TaskStatus.Faulted && task.Exception != null)
				{
					var ex = task.Exception.InnerException ?? task.Exception;
					if (ex is FileAccessException fa)
					{
						return await HandleFileAccess(fa);
					}

					return new ProgramError(ex.Message, goalStep, function, parameterValues, Exception: ex);
				}

				if (!goalStep.WaitForExecution || method.ReturnType == typeof(Task))
				{
					return null;
				}

				(object? result, var error) = GetValuesFromTask(task);
				if (error != null)
				{
					if (error is AskUserError aue)
					{
						(var isHandled, var handlerError) = await HandleAskUser(aue);
						
						if (isHandled) return await RunFunction(function);

						return ErrorHelper.GetMultipleError(error, handlerError);
					}
					return error;
				}

				SetReturnValue(function, result);

				await SetCachedItem(result);
				return null;

			}
			catch (Exception ex)
			{
				if (ex is MissingSettingsException mse)
				{
					var settingsError = new AskUserError(mse.Message, async (object[]? result) =>
					{
						var value = result?[0] ?? null;
						if (value is Array) value = ((object[])value)[0];

						await mse.InvokeCallback(value);
						return (true, null);
					});

					(var isHandled, var handlerError) = await askUserHandlerFactory.CreateHandler().Handle(settingsError);
					if (isHandled) return await RunFunction(function);
				}
				return new ProgramError(ex.Message, goalStep, function, parameterValues, "ProgramError", 500, Exception: ex);
			}
		}

		private async Task<(bool, IError?)> HandleAskUser(AskUserError aue)
		{
			(var isHandled, var handlerError) = await askUserHandlerFactory.CreateHandler().Handle(aue);
			if (handlerError is AskUserError aueSecond)
			{
				return await HandleAskUser(aueSecond);
			}
			return (isHandled, handlerError);
		}

		private async Task<IError?> HandleFileAccess(FileAccessException fa)
		{
			var fileAccessHandler = container.GetInstance<FileAccessHandler>();
			var askUserFileAccess = new AskUserFileAccess(fa.AppName, fa.Path, fa.Message, fileAccessHandler.ValidatePathResponse);

			(var isHandled, var handlerError) = await askUserHandlerFactory.CreateHandler().Handle(askUserFileAccess);
			if (isHandled) return await RunFunction(function);

			return ErrorHelper.GetMultipleError(askUserFileAccess, handlerError);
		}

		private (object? returnValue, IError? error) GetValuesFromTask(Task task)
		{

			Type taskType = task.GetType();
			var returnArguments = taskType.GetGenericArguments().FirstOrDefault();
			if (returnArguments == null) return (null, null);

			if (returnArguments == typeof(IError))
			{
				var resultTask = task as Task<IError?>;
				return (null, resultTask?.Result);
			}

			if (!returnArguments.FullName!.StartsWith("System.ValueTuple"))
			{
				var resultProperty = taskType.GetProperty("Result");
				return (resultProperty.GetValue(task), null);

			}

			var fields = returnArguments.GetFields();
			if (fields[0] == typeof(IError))
			{
				// It's a Task<Error?>
				var resultTask = task as Task<IError?>;
				return (null, resultTask?.Result);
			}

			else if (fields.Count() > 1)
			{
				var resultProperty = taskType.GetProperty("Result");
				var result = (dynamic)resultProperty.GetValue(task);

				//var item1 = result.GetType().GetProperties()[0].GetValue(result);
				//var item2 = result.GetType().GetProperties()[1].GetValue(result);

				return (result.Item1, result.Item2 as IError);
			}
			else
			{
				var resultTask = task as Task<object?>;
				return (resultTask, null);
			}

			return (null, new Error("Could not extract return value or error"));
		}

		private void SetReturnValue(GenericFunction function, object? result)
		{
			if (function.ReturnValue == null || function.ReturnValue.Count == 0) return;


			if (result == null)
			{
				foreach (var returnValue in function.ReturnValue)
				{
					memoryStack.Put(returnValue.VariableName, null);
				}
			}
			else if (result is IReturnDictionary || result.GetType().Name == "DapperRow")
			{
				var dict = (IDictionary<string, object>)result;
				foreach (var returnValue in function.ReturnValue)
				{
					var key = dict.Keys.FirstOrDefault(p => p.Replace("%", "").ToLower() == returnValue.VariableName.Replace("%", "").ToLower());
					if (key == null)
					{
						memoryStack.Put(returnValue.VariableName, dict);
						continue;
					}

					memoryStack.Put(returnValue.VariableName, dict[key]);
				}
			}
			else
			{
				foreach (var returnValue in function.ReturnValue)
				{
					memoryStack.Put(returnValue.VariableName, result);
				}
			}
		}

		private async Task SetCachedItem(object? result)
		{
			if (result == null) return;
			if (goalStep?.CacheHandler?.CacheKey == null || goalStep.CacheHandler?.TimeInMilliseconds == null) return;

			long time = goalStep.CacheHandler?.TimeInMilliseconds ?? 0;
			if (time == 0) return;

			if (goalStep.CacheHandler?.CachingType == 0)
			{
				await appCache.Set(goalStep.CacheHandler?.CacheKey!, result, TimeSpan.FromMilliseconds((double)time));
			}
			else
			{
				await appCache.Set(goalStep.CacheHandler?.CacheKey!, result, DateTime.Now.AddMilliseconds((double)time));
			}
		}

		private async Task<bool> LoadCached(MethodInfo method, GenericFunction function)
		{
			if (goalStep?.CacheHandler == null || goalStep.CacheHandler?.CacheKey == null) return false;

			if (method.ReturnType == typeof(Task) || !goalStep.WaitForExecution)
			{
				if (!goalStep.WaitForExecution)
				{
					logger.LogWarning($"It is not possible to cache {method.Name} since it is not waiting for the execution.");
				}
				if (method.ReturnType == typeof(Task))
				{
					logger.LogWarning($"It is not possible to cache {method.Name} since it does not return value.");
				}
			}
			else
			{
				var cacheKey = variableHelper.LoadVariables(goalStep.CacheHandler.CacheKey)?.ToString();
				if (cacheKey == null) return false;

				var obj = await appCache.Get(cacheKey);
				if (obj != null && function.ReturnValue != null && function.ReturnValue.Count > 0)
				{
					foreach (var returnValue in function.ReturnValue)
					{
						logger.LogDebug($"Cache was hit for {goalStep.CacheHandler.CacheKey}");
						memoryStack.Put(returnValue.VariableName, obj);
					}
					return true;
				}
			}


			return false;
		}



		//TODO: should create a cleanup here to kill processes correctly
		protected void KeepAlive(object instance, string key)
		{
			var alives = AppContext.GetData("KeepAlive") as List<Alive>;
			if (alives == null) alives = new List<Alive>();

			var aliveType = alives.FirstOrDefault(p => p.Type == instance.GetType() && p.Key == key);
			if (aliveType == null)
			{
				aliveType = new Alive(instance.GetType(), key);
				alives.Add(aliveType);

				AppContext.SetData("KeepAlive", alives);
			}
		}

		public void RemoveKeepAlive(object instance, string key)
		{
			var alives = AppContext.GetData("KeepAlive") as List<Alive>;
			var aliveType = alives.FirstOrDefault(p => p.Type == instance.GetType() && p.Key == key);
			if (aliveType != null)
			{
				alives.Remove(aliveType);

				AppContext.SetData("KeepAlive", alives);
			}
		}

		protected void RegisterForPLangUserInjections(string type, string pathToDll, bool globalForWholeApp = false, string? environmentVariable = null, string? environmentVariableValue = null)
		{
			this.container.RegisterForPLangUserInjections(type, pathToDll, globalForWholeApp, environmentVariable, environmentVariableValue);
		}

		public virtual async Task<(string info, IError? error)> GetAdditionalAssistantErrorInfo()
		{
			return (string.Empty, null);
		}
		public virtual async Task<string> GetAdditionalSystemErrorInfo()
		{
			return "";
		}


		protected string GetPath(string path)
		{
			return PathHelper.GetPath(path, fileSystem, this.Goal);
		}

		protected async Task<(string?, IError?)> AssistWithError(string error, GoalStep step, GenericFunction function)
		{
			AppContext.TryGetSwitch("llmerror", out bool isEnabled);
			if (!isEnabled) return (null, null);

			string additionSystemErrorInfo = await GetAdditionalSystemErrorInfo();
			string system = @$"You are c# expert developer debugging an error that c# throws.
The user is programming in a programming language called Plang, a pseudo language, that is built on top of c#.

The user is not familiar with c# and does not understand it, he only understands Plang.

You job is to identify why an error occurred that user provides.
You will be provided with function information and the parameters used. 
You will get description of what the function should do.
{additionSystemErrorInfo}

Be straight to the point, point out the most obvious reason and how to fix in plang source code. 
Be Concise";

			var additionalAssistant = await GetAdditionalAssistantErrorInfo();
			if (additionalAssistant.error != null)
			{
				return (null, additionalAssistant.error);
			}
			string assistant = @$"
## plang source code ##
{step.Text}
## plang source code ##
## function info ##
{typeHelper.GetMethodsAsString(this.GetType(), function.FunctionName)}
## function info ##
" + additionalAssistant.info;

			try
			{
				var promptMesage = new List<LlmMessage>();
				promptMesage.Add(new LlmMessage("system", system));
				promptMesage.Add(new LlmMessage("assistant", assistant));
				promptMesage.Add(new LlmMessage("user", error));

				var llmRequest = new LlmRequest("AssistWithError", promptMesage);

				return await llmServiceFactory.CreateHandler().Query<string>(llmRequest);
			}
			catch
			{
				return (null, new Error("ErrorToConnect", "Could not connect to LLM service"));
			}


		}


		public IError? TaskHasError(Task<(IEngine, IError? error)> task)
		{

			if (task.Exception != null)
			{
				var exception = task.Exception.InnerException ?? task.Exception;
				return new Error(exception.Message, Exception: exception);
			}

			return task.Result.error;
		}
	}
}
