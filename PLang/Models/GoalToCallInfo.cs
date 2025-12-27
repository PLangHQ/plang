using AngleSharp.Attributes;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static PLang.Modules.BaseBuilder;

namespace PLang.Models
{
	[Description("Name of goal to bind to, name can be * to bind to all, name can be regex")]
	public class GoalToBindTo
	{
		private string name;
		public GoalToBindTo(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) throw new Exception("goal name cannot be empty");

			// bad for regex, 
			if (name.Contains("\\"))
			{
				name = name.Replace("\\", "/");
			}
			this.name = name.Replace("!", "");
		}
		public string Name { get { return name; } set { name = value; } }

		public override string? ToString() => Name;

		// Implicit conversion from string to GoalToCall
		public static implicit operator GoalToBindTo(string? value) => new GoalToBindTo(value);

		// Implicit conversion from GoalToCall to string
		public static implicit operator string?(GoalToBindTo? goalToCall) => goalToCall?.Name;

		public override bool Equals(object? obj)
		{
			if (obj is GoalToBindTo other)
			{
				return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		public override int GetHashCode() => Name?.GetHashCode() ?? 0;
	}

	public class AppToCallInfo : GoalToCallInfo
	{
		private string appName;
		public AppToCallInfo(string appName, string goalName = "Start", Dictionary<string, object?>? parameters = null) :
			base(goalName, parameters)
		{
			if (string.IsNullOrWhiteSpace(appName))
			{
				throw new Exception("app name cannot be empty");
			}
			this.appName = appName;
		}
		public string AppName { get { return appName; } set { appName = value; } }

		public static (string? appName, string? goalName, IError?) GetAppAndGoalName(string name)
		{
			string appFolder = "/apps/";
			int appFolderIndex = name.IndexOf("apps");
			if (appFolderIndex != -1)
			{
				appFolder = name.Substring(0, 5);
				name = name.Remove(5).TrimStart('/');
			}

			int appNameIndex = name.IndexOf('/');
			if (appNameIndex == -1) return (null, null, new Error($"Could not determine appName for '{name}'"));

			string appName = name.Substring(0, appNameIndex).TrimStart('/').TrimEnd('/');
			string goalName = name.Remove(appNameIndex).TrimStart('/');
			if (string.IsNullOrEmpty(goalName.Trim('/'))) goalName = "Start.goal";

			return ($"{appFolder}/{appName}", goalName, null);





		}
	}
	[Description("Name of goal and parameters that is called, e.g. in condition, loops, run goal. Keep Name as user defines it with path")]
	public class GoalToCallInfo
	{
		private string name;
		private Dictionary<string, object?> parameters;
		private GenericFunction? function = null;
		public GoalToCallInfo(string name, Dictionary<string, object?>? parameters = null)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new Exception("goal name cannot be empty");
			}

			if (name.Contains("\\"))
			{
				name = name.Replace("\\", "/");
			}
			this.name = name.Replace("!", "");
			this.parameters = parameters ?? new();
		}

		[LlmIgnore]
		public string Path { get; set; }

		public string Name
		{
			get { return name; }
			set
			{

				if (value.Contains("\\"))
				{
					value = value.Replace("\\", "/");
				}
				name = value;
			}
		}
		public Dictionary<string, object?> Parameters { get { return parameters; } set { parameters = value ?? new(); } }

		[Description("Doesn't wait for goal response(run and forget, dont wait)")]
		public bool IsAsync { get; set; } = false;
		public int WaitBeforeExecutingInMs { get; set; } = 0;
		public GoalToCallInfo? AfterExecution { get; set; } = null;
		public IGenericFunction GetFunction(PLangContext context)
		{

			if (function != null) return function;

			var instruction = JsonHelper.ParseFilePath<Instruction>(context.FileSystem, Path);
			return instruction.Function;

		}

		public override string? ToString() => Name;

		// Implicit conversion from string to GoalToCall
		public static implicit operator GoalToCallInfo(string? value) => new GoalToCallInfo(value);

		// Implicit conversion from GoalToCall to string
		public static implicit operator string?(GoalToCallInfo? goalToCall) => goalToCall?.Name;

		public override bool Equals(object? obj)
		{
			if (obj is GoalToCallInfo other)
			{
				return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		public override int GetHashCode() => Name?.GetHashCode() ?? 0;


		public GoalToCallInfo Clone()
		{
			var clone = new GoalToCallInfo(this.name, this.parameters?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
			{
				Path = this.Path,
				IsAsync = this.IsAsync,
				WaitBeforeExecutingInMs = this.WaitBeforeExecutingInMs,
				AfterExecution = this.AfterExecution?.Clone()
			};
			return clone;
		}
	}
	/*
	public class GoalToCall
	{
		public string? Value { get; }

		public GoalToCall(string? value)
		{

			if (!string.IsNullOrWhiteSpace(value))
			{
				if (value.Contains("\\"))
				{
					value = value.Replace("\\", "/");
				}

				Value = value.Replace("!", "");
			}

		}

		public override string? ToString() => Value;

		// Implicit conversion from string to GoalToCall
		public static implicit operator GoalToCall(string? value) => new GoalToCall(value);

		// Implicit conversion from GoalToCall to string
		public static implicit operator string?(GoalToCall? goalToCall) => goalToCall?.Value;

		public override bool Equals(object? obj)
		{
			if (obj is GoalToCall other)
			{
				return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		public override int GetHashCode() => Value?.GetHashCode() ?? 0;
	}*/
}
