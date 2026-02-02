using Microsoft.AspNetCore.Mvc.Rendering;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Events;
using PLang.Events.Types;
using PLang.Models;
using PLang.Runtime;
using PLang.Runtime.Actors;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using System.Collections.Concurrent;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.MockModule.Program;
using static PLang.Utils.StepHelper;
using LightInject;

namespace PLang.Interfaces
{

	public interface IPLangContextAccessor
	{
		PLangContext Current { get; set; }
	}

	public class ContextAccessor : IPLangContextAccessor
	{
		private static AsyncLocal<PLangContext> _current = new AsyncLocal<PLangContext>();

		public PLangContext Current
		{
			get => _current.Value;
			set => _current.Value = value;
		}
	}


	public enum ExecutionMode
	{
		Console,
		HttpRequest,
		App,
		ScheduledTask,
		Test
	}

	public record UiOutputProperties(string Path)
	{
		public string? Target { get; set; }
		public string? ErrorTarget { get; set; }
		public List<string>? Actions { get; set; }
	};


	public class PLangContext
	{
		public string Id { get; set; }
		public bool IsAsync { get; set; }
		public bool ShowErrorDetails { get; set; }
		public bool DebugMode { get; set; }
		public string Identity { get; set; }
		public SignedMessage? SignedMessage { get; set; }
		public HttpContext? HttpContext { get; set; }
		public MemoryStack MemoryStack { get; }
		public Callback? Callback { get; set; }
		public GoalStep? CallingStep { get; set; }
		public RuntimeEvent? Event { get; set; }
		public IError Error { get; set; }

		//public ConcurrentDictionary<string, string> ActiveEvents = new();
		public List<MockData> Mocks { get; init; }
		public ConcurrentDictionary<string, object?> Items { get; init; }
		public ConcurrentDictionary<string, object?> SharedItems { get; set; }

		public UiOutputProperties UiOutputProperties { get; set; }
		public IEngine Engine { get; }
		public IOutputSink UserSink { get; set; }
		public IOutputSink SystemSink { get; set; }
		public IModuleRegistry Modules { get; private set; }
		public CallStack CallStack { get; set; }
		public ExecutionMode ExecutionMode { get; set; }
		public IPLangFileSystem FileSystem { get; internal set; }
		public DataSource DataSource { get; internal set; }
		public DataSource SystemDataSource { get; internal set; }

		// Actor-based output routing
		public Actor? UserActor { get; set; }
		public Actor? SystemActor { get; set; }
		public Actor? ServiceActor { get; set; }

		public IOutputSink GetSink(string actor)
		{
			if (string.IsNullOrWhiteSpace(actor)) return SystemSink;

			return actor.Equals("user", StringComparison.OrdinalIgnoreCase) ? UserSink : SystemSink;
		}

		/// <summary>
		/// Gets the Actor by name. Returns UserActor for "user", SystemActor for "system", ServiceActor for "service"
		/// </summary>
		public Actor? GetActor(string? actorName)
		{
			if (string.IsNullOrWhiteSpace(actorName)) return UserActor;

			return actorName.ToLowerInvariant() switch
			{
				"user" => UserActor,
				"system" => SystemActor,
				"service" => ServiceActor ?? SystemActor,
				_ => UserActor
			};
		}

		/// <summary>
		/// Gets the goal handler for an actor/channel combination, if one is registered
		/// </summary>
		public GoalToCallInfo? GetChannelHandler(string? actorName, string? channelName)
		{
			var actor = GetActor(actorName);
			return actor?.GetChannelHandler(channelName);
		}

		/// <summary>
		/// Registers a goal handler for an actor/channel combination
		/// </summary>
		public void RegisterChannelHandler(string actorName, string channelName, GoalToCallInfo goalHandler)
		{
			var actor = GetActor(actorName);
			actor?.RegisterChannelHandler(channelName, goalHandler);
		}

		/// <summary>
		/// Clears goal-scoped channel settings for a goal when it exits.
		/// Called automatically by Engine when a goal completes.
		/// </summary>
		public void ClearGoalScopedSettings(Goal goal)
		{
			if (goal == null) return;

			foreach (var actor in new[] { SystemActor, UserActor, ServiceActor })
			{
				if (actor == null) continue;
				foreach (var channel in actor.GetAllChannels())
				{
					channel.ClearScopedSettings(goal);
				}
			}
		}

		/// <summary>
		/// Configures the output content type for an actor/channel.
		/// If goal is provided, the setting is scoped to that goal and cleared when the goal exits.
		/// If goal is null, the setting persists for the context lifetime.
		/// </summary>
		public void ConfigureOutput(string? actorName, string? channelName, string contentType, Goal? scopeToGoal = null)
		{
			var actor = GetActor(actorName);
			if (actor == null) return;

			var channel = actor.GetOrCreateChannel(channelName ?? "default");

			if (scopeToGoal != null)
			{
				channel.SetScopedContentType(scopeToGoal, contentType);
			}
			else
			{
				channel.ContentType = contentType;
			}
		}

		/// <summary>
		/// Configures the output encoding for an actor/channel.
		/// </summary>
		public void ConfigureOutputEncoding(string? actorName, string? channelName, System.Text.Encoding encoding, Goal? scopeToGoal = null)
		{
			var actor = GetActor(actorName);
			if (actor == null) return;

			var channel = actor.GetOrCreateChannel(channelName ?? "default");

			if (scopeToGoal != null)
			{
				channel.SetScopedEncoding(scopeToGoal, encoding);
			}
			else
			{
				channel.Encoding = encoding;
			}
		}

		/// <summary>
		/// Gets the effective content type for an actor/channel, considering goal-scoped overrides.
		/// </summary>
		public string? GetEffectiveContentType(string? actorName, string? channelName = null)
		{
			var actor = GetActor(actorName);
			if (actor == null) return null;

			var channel = actor.GetChannel(channelName ?? "default");
			if (channel == null) return actor.ContentType;

			return channel.GetEffectiveContentType(CallStack, actor.ContentType);
		}

		/// <summary>
		/// Gets the effective encoding for an actor/channel, considering goal-scoped overrides.
		/// </summary>
		public System.Text.Encoding? GetEffectiveEncoding(string? actorName, string? channelName = null)
		{
			var actor = GetActor(actorName);
			if (actor == null) return null;

			var channel = actor.GetChannel(channelName ?? "default");
			if (channel == null) return actor.Encoding;

			return channel.GetEffectiveEncoding(CallStack, actor.Encoding);
		}

		/// <summary>
		/// Gets explicitly configured content type (via ConfigureOutput), or null if using defaults.
		/// This is used to determine if Accept header should be overridden.
		/// </summary>
		public string? GetExplicitContentType(string? actorName, string? channelName = null)
		{
			var actor = GetActor(actorName);
			if (actor == null) return null;

			var channel = actor.GetChannel(channelName ?? "default");
			if (channel == null) return null;

			return channel.GetExplicitContentType(CallStack);
		}

		public PLangContext(MemoryStack memoryStack, IEngine engine, ExecutionMode executionMode)
		{
			ExecutionMode = executionMode;
			MemoryStack = memoryStack;
			Engine = engine;
			CallStack = new();
			// Clone module registry from engine's default for this context
			Modules = engine.CloneDefaultModuleRegistry();
			// Get default sinks from engine (these will be the defaults since context is not yet set)
			SystemSink = engine.SystemSink;
			UserSink = engine.UserSink;

			// Initialize actors with the sinks
			SystemActor = new SystemActor(identity: null, defaultSink: SystemSink);
			UserActor = new UserActor(identity: null, defaultSink: UserSink, isTrusted: executionMode == ExecutionMode.Console);

			Items = new();
			Mocks = new();
			SharedItems = new();

			Id = Guid.NewGuid().ToString();
		}



		public object? this[string key]
		{
			get
			{
				if (Items.TryGetValue(key, out var value)) return value;
				return null;
			}
			set
			{
				AddOrReplace(key, value);
			}
		}
		public void AddOrReplace(string key, object? value)
		{
			if (key == null || value == null) return;


			Items.AddOrUpdate(key, value, (str, obj1) =>
			{
				return value;
			});

		}

		public void Remove(string key)
		{
			Items.Remove(key, out var _);
		}

		public bool TryGetValue<T>(string key, out T? objInstance)
		{
			var obj = this[key];
			if (obj == null)
			{
				objInstance = default(T);
				return false;
			}
			objInstance = (T)obj;
			return true;
		}
		public (T?, IError?) Get<T>(string key)
		{
			if (key == null) return (default, new Error($"Key ({key}) was empty, searching in context"));

			if (TryGetValue(key, out T? obj))
			{
				return ((T?)obj, null);
			}
			else
			{
				return (default, new Error($"Key ({key}) not found in context"));
			}
		}
		public T? GetOrDefault<T>(string key, T? defaultValue)
		{
			if (key == null) return defaultValue;

			if (ContainsKey(key))
			{
				return (T?)Items[key];
			}
			else
			{
				return defaultValue;
			}
		}

		public new bool ContainsKey(string key)
		{
			try
			{
				key = key.Replace("%", "");
				return Items.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Key != null;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public bool ContainsKey(string key, out object? obj)
		{
			key = key.Replace("%", "");
			var keyValue = Items.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
			if (keyValue.Key == null)
			{
				obj = null;
				return false;
			}
			obj = keyValue;
			return true;
		}

		private readonly Dictionary<string, object> _moduleData = new();

		public T GetModuleData<T>(string key = null) where T : class, new()
		{
			string moduleKey = key ?? typeof(T).Name;

			if (!_moduleData.ContainsKey(moduleKey))
			{
				_moduleData[moduleKey] = new T();
			}

			return _moduleData[moduleKey] as T;
		}

		public void SetModuleData<T>(T data, string key = null) where T : class
		{
			string moduleKey = key ?? typeof(T).Name;
			_moduleData[moduleKey] = data;
		}


		public Dictionary<string, object?> GetReserverdKeywords()
		{
			var dict = new Dictionary<string, object?>();
			var keywords = ReservedKeywords.Keywords;
			foreach (var keyword in keywords)
			{
				if (this.ContainsKey(keyword))
				{
					dict.Add(keyword, Items[keyword]);
				}
			}
			return dict;
		}

		internal PLangContext Clone(MemoryStack memoryStack, IEngine runtimeEngine)
		{
			var context = new PLangContext(memoryStack, runtimeEngine, ExecutionMode);
			context.CallingStep = this.CallingStep;
			context.Identity = this.Identity;
			context.SignedMessage = this.SignedMessage;
			context.CallStack = new CallStack();
			foreach (var item in this.Items)
			{
				context.Items.TryAdd(item.Key, item.Value);
			}
			foreach (var item in this.Mocks)
			{
				context.Mocks.Add(item);
			}
			context.SharedItems = this.SharedItems;
			context.HttpContext = this.HttpContext;
			context.Callback = this.Callback;
			context.DebugMode = this.DebugMode;
			context.ShowErrorDetails = this.ShowErrorDetails;
			context.SystemSink = this.SystemSink;
			context.UserSink = this.UserSink;
			// Clone actors - they share the same sinks but channel handlers are per-context
			context.UserActor = this.UserActor;
			context.SystemActor = this.SystemActor;
			context.ServiceActor = this.ServiceActor;
			// Clone module registry - inherit enabled/disabled state
			context.Modules = (this.Modules as ModuleRegistry)?.Clone() ?? runtimeEngine.CloneDefaultModuleRegistry();

			return context;
		}

		public void AddVariable<T>(T? value, Func<Task>? func = null, string? variableName = null)
		{
			CallStack.CurrentFrame.AddVariable(value, func, variableName);
		}

		public object? GetVariable(string variableName, int level = 0)
		{
			return CallStack.CurrentFrame.GetVariable(variableName, level);
		}
		public T? GetVariable<T>(string? variableName = null, int level = 0)
		{
			return CallStack.CurrentFrame.GetVariable<T>(variableName, level);
		}

		internal void RemoveVariable(string variableName)
		{
			CallStack.CurrentFrame.RemoveVariable(variableName);
		}

		public List<EventBinding> EventBindings = new List<EventBinding>();
		internal void AddEvent(EventBinding eventBinding)
		{
			EventBindings.Add(eventBinding);
		}
	}
}
