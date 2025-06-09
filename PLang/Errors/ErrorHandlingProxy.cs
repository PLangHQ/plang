using Castle.DynamicProxy;
using PLang.Errors.AskUser;
using PLang.Errors.Types;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules;
using System.Reflection;
using System.Runtime.CompilerServices;
using static PLang.Utils.VariableHelper;

namespace PLang.Errors;
public class ErrorHandlingInterceptor : IInterceptor
{
	private readonly IEventRuntime eventRuntime;
	private readonly PLangAppContext context;

	// LightInject will inject IEventRuntime here
	public ErrorHandlingInterceptor(IEventRuntime eventRuntime, PLangAppContext context)
	{
		this.eventRuntime = eventRuntime;
		this.context = context;
	}

	public void Intercept(IInvocation invocation)
	{
		var method = invocation.MethodInvocationTarget ?? invocation.Method;
		var target = invocation.InvocationTarget!;
		var args = invocation.Arguments;

		if (typeof(Task).IsAssignableFrom(method.ReturnType))
		{
			// async
			invocation.ReturnValue = method.ReturnType.IsGenericType
				? InvokeAsyncGeneric(method, target, args)
				: InvokeAsync(method, target, args);
		}
		else
		{
			throw new NotImplementedException("syncronized method is not supporteds");
		}
		// sync exceptions are thrown by Proceed() so caught by caller if needed
	}


	private async Task InvokeAsync(MethodInfo method, object target, object[] args)
	{
		while (true)
		{
			try
			{
				var task = (Task)method.Invoke(target, args)!;
				await task.ConfigureAwait(false);
				return;
			}
			catch (Exception ex)
			{
				var repl = eventRuntime.RunOnModuleError(method, null, ex);
				if (repl is Task t)
				{
					await t.ConfigureAwait(false);
					return;
				}
				if (repl == null) continue;
				throw;
			}
		}
	}

	private Task InvokeAsyncGeneric(MethodInfo method, object target, object[] args)
	{
		// dispatch to the generic version
		var gen = GetType()
		  .GetMethod(nameof(InvokeAsyncGenericImpl), BindingFlags.NonPublic | BindingFlags.Instance)!
		  .MakeGenericMethod(method.ReturnType.GenericTypeArguments[0]);

		return (Task)gen.Invoke(this, new object[] { method, target, args })!;
	}

	private async Task<T> InvokeAsyncGenericImpl<T>(MethodInfo method, object target, object[] args)
	{
		while (true)
		{
			try
			{
				var task = (Task<T>)method.Invoke(target, args)!;
				var result = await task.ConfigureAwait(false);

				if (TryFindError(result, out var err))
				{
					var repl = await eventRuntime.RunOnModuleError(method,  err, null);
					if (repl.Error != null) throw new ExceptionWrapper(repl.Error);
					if (repl.Variables != null)
					{
						if (repl.Variables != null)
						{
							throw new Exception("How to handle variablses, see commented out code??");
						}
						/*
						foreach (var variable in repl.Variables)
						{
							if (!context.ContainsKey("!" + variable.Key))
							{
								context.AddOrReplace("!" + variable.Key, variable.Value);
							}
						}*/
						if (err is IExternalCallbackError ce)
						{
							context.AddOrReplace("!callback", ce.Callback);
						}
						

						task = (Task<T>)method.Invoke(target, args)!;
						result = await task.ConfigureAwait(false);


						return result;
					}
					
				}
				return result;
			}
			catch (TargetInvocationException tie) when (tie.InnerException is Exception ex)
			{
				var repl = eventRuntime.RunOnModuleError(method,null, ex);
				if (repl is T r) return r;
				if (repl == null) continue;
				throw;
			}
		}
	}

	private async Task WrapAsync(Task task, MethodInfo method)
	{
		try
		{
			await task.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			HandleError(ex, method);
			throw;
		}
		int i = 0;
	}

	private async Task<T> WrapAsync<T>(Task<T> task, MethodInfo method)
	{
		try
		{
			var result = await task.ConfigureAwait(false);
			await CheckForError(result, method);
			return result;
		}
		catch (Exception ex)
		{
			HandleError(ex, method);
			throw;
		}
		int i = 0;
	}

	private void HandleError(Exception ex, MethodInfo m)
	{
		// central logging/retry logic
		Console.WriteLine($"[{m.DeclaringType?.Name}.{m.Name}] ERR: {ex.Message}");

		var result = eventRuntime.RunOnModuleError(m, null, ex);
	}


	private async Task<(object? Variables, IError? Error)> CheckForError(object? result, MethodInfo method)
	{
		if (TryFindError(result, out var error))
		{
			return await eventRuntime.RunOnModuleError(method, error, null);
		}
		return (null, null);
	}

	private bool TryFindError(object? value, out IError? error)
	{
		if (value is IError e)
		{
			error = e;
			return true;
		}

		if (value is ITuple tuple)   // ValueTuple<…> implements this :contentReference[oaicite:0]{index=0}
		{
			for (int i = 0; i < tuple.Length; i++)
			{
				if (TryFindError(tuple[i], out error))
					return true;
			}
		}

		error = null;
		return false;
	}
}
