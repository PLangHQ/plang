﻿using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Interfaces;
using System.ComponentModel;
using System.Text;
using System.Web;

namespace PLang.Modules.LocalOrGlobalVariableModule
{
	[Description("Set & Get local and static variables. Bind onCreate, onChange, onRemove events to variable.")]
	public class Program : BaseProgram
	{
		private readonly ISettings settings;

		public Program(ISettings settings) : base()
		{
			this.settings = settings;
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnCreateVariableListener([HandlesVariable] string key, string goalName, [HandlesVariable] Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnCreateEvent(key, goalName, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnChangeVariableListener([HandlesVariable] string key, string goalName, bool notifyWhenCreated = true, [HandlesVariable] Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnChangeEvent(key, goalName, false, notifyWhenCreated, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
			if (notifyWhenCreated)
			{
				memoryStack.AddOnCreateEvent(key, goalName, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);

			}
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnRemoveVariableListener([HandlesVariable] string key, string goalName, [HandlesVariable] Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnRemoveEvent(key, goalName, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnCreateStaticVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnCreateEvent(key, goalName, true, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnChangeStaticVariableListener([HandlesVariable] string key, string goalName, bool notifyWhenCreated = true, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnChangeEvent(key, goalName, true, notifyWhenCreated, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnRemoveStaticVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnRemoveEvent(key, goalName, true, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}

		public async Task<string?> GetEnvironmentVariable(string key)
		{
			return Environment.GetEnvironmentVariable(key);
		}
		public async Task<object?> LoadVariables([HandlesVariable] string key)
		{
			var content = memoryStack.Get(key);
			if (content == null) return null;

			return variableHelper.LoadVariables(content);
		}
		[Description(@"Set string variable. Developer might use single/double quote to indicate the string value, the wrapped quote should not be included in the value. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetStringVariable([HandlesVariable] string key, string? value = null, bool urlDecode = false, bool htmlDecode = false)
		{
			if (urlDecode) value = HttpUtility.UrlDecode(value);
			if (htmlDecode) value = HttpUtility.HtmlDecode(value);

			memoryStack.Put(key, variableHelper.LoadVariables(value));
		}
		[Description(@"Set int/long variable.")]
		public async Task SetNumberVariable([HandlesVariable] string key, long? value = null)
		{
			memoryStack.Put(key, variableHelper.LoadVariables(value));
		}
		[Description(@"Set double variable.")]
		public async Task SetDoubleVariable([HandlesVariable] string key, double? value = null)
		{
			memoryStack.Put(key, variableHelper.LoadVariables(value));
		}
		[Description(@"Set float variable.")]
		public async Task SetFloatVariable([HandlesVariable] string key, float? value = null)
		{
			memoryStack.Put(key, variableHelper.LoadVariables(value));
		}
		[Description(@"Set bool variable.")]
		public async Task SetBoolVariable([HandlesVariable] string key, bool? value = null)
		{
			memoryStack.Put(key, variableHelper.LoadVariables(value));
		}

		[Description(@"Set variable. Developer might use single/double quote to indicate the string value. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetVariable([HandlesVariable] string key, object? value = null)
		{
			memoryStack.Put(key, variableHelper.LoadVariables(value));
		}
		[Description(@"Set multiple variables. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues)
		{
			foreach (var key in keyValues)
			{
				memoryStack.Put(key.Key, variableHelper.LoadVariables(key.Value));
			}
		}
		[Description(@"Set value on variables. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetValuesOnVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues)
		{
			foreach (var key in keyValues)
			{				
				memoryStack.Put(key.Key, variableHelper.LoadVariables(key.Value));	
			}

		}
		[Description(@"Set default value on variables if not set. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetDefaultValueOnVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues)
		{
			foreach (var key in keyValues)
			{
				var objectValue = memoryStack.GetObjectValue2(key.Key, false);
				if (!objectValue.Initiated)
				{
					memoryStack.Put(key.Key, variableHelper.LoadVariables(key.Value));
				}
			}

		}

		[Description("Append to variable. valueLocation=postfix|prefix seperatorLocation=end|start")]
		public async Task<object?> AppendToVariable([HandlesVariableAttribute] string key, object? value = null, char seperator = '\n', string valueLocation = "postfix", string seperatorLocation = "end", bool shouldBeUnique = false)
		{
			if (value == null) return value;

			object? val = memoryStack.Get(key);
			if (val != null && val is string && (value is JObject || value is JProperty || value is JValue))
			{
				value = value.ToString();
			}

			if ((val == null || val is string) && value is string)
			{
				if (val == null) val = "";

				string appendingValue = (seperatorLocation == "start") ? seperator.ToString() + value.ToString() : value.ToString() + seperator.ToString();
				val = (valueLocation == "postfix") ? val + appendingValue : appendingValue + val;
			}
			else if (val is System.Collections.IList list)
			{
                if (!shouldBeUnique || (shouldBeUnique && !list.Contains(val)))
                {
					list.Add(value);
				}                
			}
			else
			{
				val = new List<object>();
				((List<object>) val).Add(value);
				//throw new Exception("Cannot append to an object");
			}
			memoryStack.Put(key, val);
			return val;
		}

		public async Task<object> GetVariable([HandlesVariableAttribute] string key)
		{
			return memoryStack.Get(key);
		}


		public async Task RemoveVariable([HandlesVariableAttribute] string key)
		{
			memoryStack.Remove(key);
		}


		public async Task SetStaticVariable([HandlesVariableAttribute] string key, object value)
		{
			if (value.ToString().StartsWith("%") && value.ToString().EndsWith("%"))
			{
				value = memoryStack.Get(value.ToString());
			}
			memoryStack.PutStatic(key, value);
		}

		public async Task<object> GetStaticVariable([HandlesVariableAttribute] string key)
		{
			return memoryStack.GetStatic(key);
		}
		public async Task RemoveStaticVariable([HandlesVariableAttribute] string key)
		{
			memoryStack.RemoveStatic(key);
		}

		[Description("Sets a value to %Settings.XXXX% variable")]
		public async Task SetSettingValue([HandlesVariableAttribute] string key, object value)
		{
			var settingKey = key.Substring(key.IndexOf('.')+1).Replace("%", "");
			settings.Set(typeof(PLang.Services.SettingsService.Settings), settingKey, value);
		}

		[Description("Sets a value to %Settings.XXXX% variable but only if it is not set before")]
		public async Task SetDefaultSettingValue([HandlesVariableAttribute] string key, object value)
		{
			var settingKey = key.Substring(key.IndexOf('.')+1).Replace("%", "");
			var settingValue = settings.GetOrDefault(typeof(PLang.Services.SettingsService.Settings), settingKey, value);
			if (value == settingValue || settingValue == null || string.IsNullOrEmpty(settingValue.ToString()))
			{
				settings.Set(typeof(PLang.Services.SettingsService.Settings), settingKey, value);
			}
		}

		public async Task<string> ConvertToBase64([HandlesVariableAttribute] string key)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(memoryStack.Get(key).ToString()));
		}
	}



}

