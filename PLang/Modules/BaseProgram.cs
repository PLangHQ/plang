using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using static PLang.Modules.BaseBuilder;
using Instruction = PLang.Building.Model.Instruction;

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
		private ILlmService llmService;
		private ILogger logger;
		private IServiceContainer container;
		private IAppCache appCache;
		private IOutputStream outputStream;
		private IPLangFileSystem fileSystem;
		private MethodHelper methodHelper;
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
			PLangAppContext context, ITypeHelper typeHelper, ILlmService llmService, ISettings settings,
			IAppCache appCache, HttpListenerContext? httpListenerContext)
		{
			this.container = container;

			this.logger = logger;
			this.memoryStack = memoryStack;
			this.context = context;
			this.appCache = appCache;
			this.outputStream = container.GetInstance<IOutputStream>();
			this.fileSystem = container.GetInstance<IPLangFileSystem>();
			_listenerContext = httpListenerContext;

			this.goal = goal;
			this.goalStep = step;
			this.instruction = instruction;

			variableHelper = new VariableHelper(context, memoryStack, settings);
			this.typeHelper = typeHelper;
			this.llmService = llmService;
			methodHelper = new MethodHelper(goalStep, variableHelper, memoryStack, typeHelper, llmService);
		}

		public virtual async Task Run()
		{
			var functions = instruction.GetFunctions();
			foreach (var function in functions)
			{
				await RunFunction(function);
			}
		}

		public async Task RunFunction(GenericFunction function)
		{
			this.function = function; // this is to give sub classes access to current function running.

			MethodInfo method = await methodHelper.GetMethod(this, function);
			logger.LogDebug("Method:{0}", method);

			//TODO: Should move this caching check up the call stack. code is doing to much work before returning cache
			if (await LoadCached(method, function)) return;

			if (method.ReturnType != typeof(Task) && method.ReturnType.BaseType != typeof(Task))
			{
				throw new RuntimeException($"The method {method.Name} does not return Task. Method that are called must return Task");
			}

			var parameterValues = methodHelper.GetParameterValues(method, function);
			logger.LogDebug("Parameters:{0}", parameterValues);

			try
			{
				// This is for memoryStack event handler. Should find a better way
				context.AddOrReplace(ReservedKeywords.Goal, goal);

				var task = method.Invoke(this, parameterValues.Values.ToArray()) as Task;
				if (task == null)
				{
					logger.LogWarning("Method called is not an async function. Make sure it is marked as 'public async Task' or 'public async Task<YourReturnType>'");
					return;
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
					if (task.Exception.InnerException != null) throw task.Exception.InnerException;
					throw task.Exception;
					//await HandleException(task, function, goalStep);
				}

				if (!goalStep.WaitForExecution || method.ReturnType == typeof(Task))
				{
					return;
				}

				object? result = await (dynamic)task;

				SetReturnValue(function, result);

				await SetCachedItem(result);

			}
			catch (FileAccessException) { throw; }
			catch (AskUserException) { throw; }
			catch (RuntimeUserStepException) { throw; }
			catch (RuntimeGoalEndException) { throw; }
			catch (RunGoalException) { throw; }
			catch (Exception ex)
			{
				throw new RuntimeProgramException(ex.Message, goalStep, function, parameterValues, ex);
			}
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
					var returnType = returnValue.Type;
					if (result.GetType().Name.StartsWith("List"))
					{
						var list = (System.Collections.IList)result;
						if (list.Count == 0 && !returnType.StartsWith("List"))
						{
							memoryStack.Put(returnValue.VariableName, null);
							continue;
						}
					}
					memoryStack.Put(returnValue.VariableName, result);
				}
			}
		}

		private async Task HandleException(Task task, GenericFunction function, GoalStep step)
		{
			if (task.Exception == null) return;

			bool throwException = true;
			if (goalStep.ErrorHandler != null)
			{
				var except = goalStep.ErrorHandler.OnExceptionContainingTextCallGoal;
				if (except != null)
				{
					if (goalStep.ErrorHandler.IgnoreErrors && except.Count == 0) return;

					foreach (var error in except)
					{
						if (error.Key == "*" || task.Exception.ToString().ToLower().Contains(error.Key.ToLower()))
						{
							throw new RunGoalException(error.Value, task.Exception);
						}
					}
				}
			}

			if (throwException)
			{
				// just realising after doing this that this should be done in plang
				// an event that binds to on error on step, 
				string error = (task.Exception.InnerException != null) ? task.Exception.InnerException.ToString() : task.Exception.ToString();


				await outputStream.Write(error, "error", 400);
				await outputStream.Write("\n\n.... Asking LLM to explain, wait few seconds ....\n", "error", 400);
				var result = await AssistWithError(error, step, function);
				await outputStream.Write("\n\n#### Explanation ####\n" + result, "error", 400);

				throw new RuntimeException(error, goal, task.Exception);

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

		protected void RegisterForPLangUserInjections(string type, string pathToDll, bool globalForWholeApp = false)
		{
			this.container.RegisterForPLangUserInjections(type, pathToDll, globalForWholeApp);
		}
		public virtual async Task<string> GetAdditionalAssistantErrorInfo()
		{
			return "";
		}
		public virtual async Task<string> GetAdditionalSystemErrorInfo()
		{
			return "";
		}


		protected string GetPath(string path)
		{
			if (path == null)
			{
				throw new ArgumentNullException("path cannot be empty");
			}
			path = path.Replace("/", Path.DirectorySeparatorChar.ToString()).Replace("\\", Path.DirectorySeparatorChar.ToString());
			if (!Path.IsPathRooted(path) || path.StartsWith(Path.DirectorySeparatorChar))
			{
				path = path.TrimStart(Path.DirectorySeparatorChar);
				if (this.Goal != null)
				{
					path = Path.Combine(this.Goal.AbsoluteGoalFolderPath, path);
				}
				else
				{
					path = Path.Combine(fileSystem.GoalsPath, path);
				}
			}
			return path;
		}

		protected async Task<string?> AssistWithError(string error, GoalStep step, GenericFunction function)
		{
			AppContext.TryGetSwitch("llmerror", out bool isEnabled);
			if (!isEnabled) return null;

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
			string additionalInfo = await GetAdditionalAssistantErrorInfo();
			string assistant = @$"
## plang source code ##
{step.Text}
## plang source code ##
## function info ##
{typeHelper.GetMethodsAsString(this.GetType(), function.FunctionName)}
## function info ##
" + additionalInfo;

			try
			{
				var promptMesage = new List<LlmMessage>();
				promptMesage.Add(new LlmMessage("system", system));
				promptMesage.Add(new LlmMessage("assistant", assistant));
				promptMesage.Add(new LlmMessage("user", error));

				var llmRequest = new LlmRequest("AssistWithError", promptMesage);

				var result = await llmService.Query<string>(llmRequest);

				return result?.ToString();
			}
			catch
			{
				return "Could not connect to LLM service";
			}


		}
	}
}
