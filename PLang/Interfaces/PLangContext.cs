using Microsoft.AspNetCore.Mvc.Rendering;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Events;
using PLang.Events.Types;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using System.Collections.Concurrent;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.MockModule.Program;
using static PLang.Utils.StepHelper;

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
		public CallStack CallStack { get; set; }
		public ExecutionMode ExecutionMode { get; set; }
		public IPLangFileSystem FileSystem { get; internal set; }
		public DataSource DataSource { get; internal set; }
		public DataSource SystemDataSource { get; internal set; }

		public IOutputSink GetSink(string actor)
		{
			if (string.IsNullOrWhiteSpace(actor)) return SystemSink;

			return actor.Equals("user", StringComparison.OrdinalIgnoreCase) ? UserSink : SystemSink;
		}

		public PLangContext(MemoryStack memoryStack, IEngine engine, ExecutionMode executionMode)
		{
			ExecutionMode = executionMode;
			MemoryStack = memoryStack;
			Engine = engine;
			CallStack = new();
			SystemSink = engine.SystemSink;
			UserSink = engine.UserSink;
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
