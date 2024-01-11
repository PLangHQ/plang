using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
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
		protected Goal? goal = null;
		protected GoalStep? goalStep;
		protected Instruction? instruction;
		protected PLangAppContext context;
		protected VariableHelper variableHelper;
		protected ITypeHelper typeHelper;
		protected GenericFunction? function;
		private ILlmService llmService;
		private ILogger logger;
		private IServiceContainer container;
		private IAppCache appCache;

		public HttpListenerContext? HttpListenerContext { get { return _listenerContext; } }
		public Goal? Goal { get { return goal; } }

		public BaseProgram()
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

			_listenerContext = httpListenerContext;

			this.goal = goal;
			this.goalStep = step;
			this.instruction = instruction;
			
			variableHelper = new VariableHelper(context, memoryStack, settings);
			this.typeHelper = typeHelper;
			this.llmService = llmService;
		}

		public virtual async Task Run()
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			var functions = instruction.GetFunctions();
			foreach (var function in functions)
			{
				await RunFunction(function);

			}
			if (this is IDisposable)
			{
				var disposableSteps = new List<IDisposable>();
				if (context.ContainsKey("DisposableSteps"))
				{
					disposableSteps = context["DisposableSteps"] as List<IDisposable>;
				}
				disposableSteps.Add((IDisposable)this);
				//context.AddOrReplace("DisposableSteps", disposableSteps);
			}

		}

		public async Task RunFunction(GenericFunction function)
		{
			this.function = function; // this is to give sub classes access to current function running.

			var methodHelper = new MethodHelper(goalStep, variableHelper, typeHelper, llmService);
			(MethodInfo? method, Dictionary<string, object> parameterValues) = await methodHelper.GetMethodAndParameters(this, function);
			logger.LogTrace("Method:{0}", method);
			logger.LogTrace("Parameters:{0}", parameterValues);

			//TODO: Should move this caching check up the call stack. code is doing to much work before returning cache
			if (await LoadCached(method, function)) return;

			if (method.ReturnType != typeof(Task) && method.ReturnType.BaseType != typeof(Task))
			{
				throw new RuntimeException($"The method {method.Name} does not return Task. Method that are called must return Task");
			}
			try
			{
				var invokeResult = method.Invoke(this, parameterValues.Values.ToArray());
				Task? task = invokeResult as Task;

				if (task == null)
				{
					logger.LogWarning("Method called is not an async function. Make sure it is marked as 'public async Task' or 'public async Task<YourReturnType>'");
					return;
				}


				// when calling ScheduleModule.Sleep, system always must wait for the execution
				if (goalStep.WaitForExecution || this.GetType() == typeof(PLang.Modules.ScheduleModule.Program) && function.FunctionName == "Sleep")
				{
					try
					{
						await task;
					}
					catch { }
				}
				if (task.Status == TaskStatus.Faulted)
				{
					HandleException(task);
				}
				if (method.ReturnType == typeof(Task))
				{
					return;
				}

				if (goalStep.WaitForExecution)
				{
					object? result = await (dynamic)task;


					if (function.ReturnValue == null || function.ReturnValue.Count == 0) return;

					if (function.ReturnValue.Count == 1)
					{
						memoryStack.Put(function.ReturnValue[0].VariableName, result);
					}
					else if (result == null)
					{
						foreach (var returnValue in function.ReturnValue)
						{
							memoryStack.Put(returnValue.VariableName, null);
						}
					}
					else
					{
						var dict = (IDictionary<string, object>)result;
						foreach (var returnValue in function.ReturnValue)
						{
							var key = dict.Keys.FirstOrDefault(p => p.ToLower() == returnValue.VariableName.Replace("%", "").ToLower());
							if (key == null) continue;

							memoryStack.Put(returnValue.VariableName, dict[key]);
						}
					}
					await SetCachedItem(result);
				}
			}
			catch (RuntimeUserStepException) { throw; }
			catch (RuntimeGoalEndException) { throw; }
			catch (RunGoalException) { throw; }
			catch (Exception ex)
			{
				string str = $@"
Step: {goalStep.Text}
Goal: {goal.GoalName} at {goal.GoalFileName}
Calling {this.GetType().FullName}.{function.FunctionName} 
	Parameters:
		{JsonConvert.SerializeObject(function.Parameters)} 
	";
				if (AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isEnabled) && isEnabled)
				{
					str += @$"Parameter values:
{JsonConvert.SerializeObject(parameterValues)}
";
				}
				str += $"\nReturn value {JsonConvert.SerializeObject(function.ReturnValue)}";

				throw new RuntimeException(str, goal, ex);
			}
		}

		private void HandleException(Task task)
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
				if (task.Exception != null && task.Exception.InnerException != null)
				{
					throw task.Exception.InnerException;
				}

				throw task.Exception;
			}
		}

		private async Task SetCachedItem(object result)
		{
			if (goalStep?.CacheHandler?.CacheKey == null || goalStep.CacheHandler?.TimeInMilliseconds == null) return;

			long time = (long)goalStep.CacheHandler?.TimeInMilliseconds;

			if (goalStep.CacheHandler?.CachingType == 0)
			{
				await appCache.Set(goalStep.CacheHandler?.CacheKey, result, TimeSpan.FromMilliseconds((double)time));
			}
			else
			{
				await appCache.Set(goalStep.CacheHandler?.CacheKey, result, DateTime.Now.AddMilliseconds((double)time));
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
				var obj = await appCache.Get(goalStep.CacheHandler.CacheKey);
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
	}
}
