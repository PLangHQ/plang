﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nostr.Client.Json;
using PLang.Attributes;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using System.Collections;
using System.ComponentModel;
using System.Text;
using System.Web;

namespace PLang.Modules.LocalOrGlobalVariableModule
{
	[Description("Set, Get & return local and static variables. Set on variable includes condition such as empty or null. Bind onCreate, onChange, onRemove events to variable.")]
	public class Program : BaseProgram
	{
		private readonly ISettings settings;

		public Program(ISettings settings) : base()
		{
			this.settings = settings;
		}


		public async Task<IError?> Return([HandlesVariable] string[] variables)
		{
			var returnDict = new ReturnDictionary<string, object?>();
			foreach (var variable in variables)
			{
				returnDict.Add(variable, memoryStack.Get(variable));
			}

			return new Return(returnDict);
		}

		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnCreateVariablesListener([HandlesVariable] string[] keys, string goalName, [HandlesVariable] Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			foreach (var key in keys)
			{
				memoryStack.AddOnCreateEvent(key, goalName, goal.Hash, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
			}
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnChangeVariablesListener([HandlesVariable] string[] keys, string goalName, bool notifyWhenCreated = true, [HandlesVariable] Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			foreach (var key in keys)
			{
				memoryStack.AddOnChangeEvent(key, goalName, goal.Hash, false, notifyWhenCreated, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
				if (notifyWhenCreated)
				{
					memoryStack.AddOnCreateEvent(key, goalName, goal.Hash, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);

				}
			}
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnRemoveVariablesListener([HandlesVariable] string[] keys, string goalName, [HandlesVariable] Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			foreach (var key in keys)
			{
				memoryStack.AddOnRemoveEvent(key, goalName, goal.Hash, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
			}
		}


		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnCreateVariableListener([HandlesVariable] string key, string goalName, [HandlesVariable] Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnCreateEvent(key, goalName, goal.Hash, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnChangeVariableListener([HandlesVariable] string key, string goalName, bool notifyWhenCreated = true, [HandlesVariable] Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnChangeEvent(key, goalName, goal.Hash, false, notifyWhenCreated, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
			if (notifyWhenCreated)
			{
				memoryStack.AddOnCreateEvent(key, goalName, goal.Hash, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);

			}
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnRemoveVariableListener([HandlesVariable] string key, string goalName, [HandlesVariable] Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnRemoveEvent(key, goalName, goal.Hash, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}

		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnCreateStaticVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnCreateEvent(key, goalName, goal.Hash, true, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnChangeStaticVariableListener([HandlesVariable] string key, string goalName, bool notifyWhenCreated = true, Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnChangeEvent(key, goalName, goal.Hash, true, notifyWhenCreated, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnRemoveStaticVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object?>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnRemoveEvent(key, goalName, goal.Hash, true, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
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
		public async Task SetStringVariable([HandlesVariable] string key, [HandlesVariable] string? value = null, bool urlDecode = false, bool htmlDecode = false, bool doNotLoadVariablesInValue = false)
		{
			if (urlDecode) value = HttpUtility.UrlDecode(value);
			if (htmlDecode) value = HttpUtility.HtmlDecode(value);

			object? content = (doNotLoadVariablesInValue) ? value : variableHelper.LoadVariables(value);
			memoryStack.Put(key, content);
		}

		[Description(@"Set json variable.")]
		public async Task SetJsonObjectVariable([HandlesVariable] string key, [HandlesVariable] object? value = null, bool doNotLoadVariablesInValue = false)
		{

			object? content = (doNotLoadVariablesInValue) ? value : variableHelper.LoadVariables(value);
			if (content == null)
			{
				memoryStack.Put(key, content);
				return;
			}

			if (content is JToken)
			{
				memoryStack.Put(key, content);
				return;
			}

			try
			{
				var str = content.ToString().TrimStart();
				if (str.StartsWith("["))
				{
					var jobject = JArray.Parse(content.ToString());
					memoryStack.Put(key, jobject);
					return;
				} else if (str.StartsWith("{"))
				{
					JObject jobject = JObject.Parse(content.ToString());
					memoryStack.Put(key, jobject);
					return;
				}

				str = JsonConvert.SerializeObject(str);
				var jobj = JsonConvert.DeserializeObject(str);

				memoryStack.Put(key, jobj);
			} catch
			{
				var str = JsonConvert.SerializeObject(content.ToString());
				var jobj = JsonConvert.DeserializeObject(str);

				memoryStack.Put(key, jobj);
			}
			
		}

		[Description(@"Set int/long variable.")]
		public async Task SetNumberVariable([HandlesVariable] string key, long? value = null)
		{
			memoryStack.Put(key, value);
		}
		[Description(@"Set double variable.")]
		public async Task SetDoubleVariable([HandlesVariable] string key, double? value = null)
		{
			memoryStack.Put(key, value);
		}
		[Description(@"Set float variable.")]
		public async Task SetFloatVariable([HandlesVariable] string key, float? value = null)
		{
			memoryStack.Put(key, value);
		}
		[Description(@"Set bool variable.")]
		public async Task SetBoolVariable([HandlesVariable] string key, bool? value = null)
		{
			memoryStack.Put(key, value);
		}

		[Description(@"Set variable. Developer might use single/double quote to indicate the string value. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetVariable([HandlesVariable] string key, [HandlesVariable]  object? value = null, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			object? content = (doNotLoadVariablesInValue) ? value : variableHelper.LoadVariables(value);

			if (onlyIfValueIsNot?.ToString() == "null" && value == null) return;
			if (onlyIfValueIsNot?.ToString() == "empty" && (value == null || IsEmpty(value))) return;
			if (onlyIfValueIsNot != null && onlyIfValueIsNot == value) return;

			if (key.Contains("%") && keyIsDynamic)
			{
				var newKey = variableHelper.LoadVariables(key);
				if (!string.IsNullOrWhiteSpace(newKey.ToString()))
				{
					key = newKey.ToString();
				}
			}
			memoryStack.Put(key, content);
		}

		private bool IsEmpty(object? value)
		{
			if (value == null) return true;
			if (value is string str && string.IsNullOrWhiteSpace(str)) return true;
			if (value is JToken token && (
				   token.Type == JTokenType.Null || // JSON null
				   (token.Type == JTokenType.Object && !token.HasValues) || 
				   (token.Type == JTokenType.Array && !token.HasValues) || 
				   (token.Type == JTokenType.String && string.IsNullOrEmpty(token.ToString())) || 
				   (token.Type == JTokenType.Property && ((JProperty)token).Value == null))) return true;
			if (value is IList list && list.Count == 0) return true;
			if (value is IDictionary dict && dict.Count == 0) return true;
			
			return false;
		}

		[Description(@"Set multiple variables. If value is json, make sure to format it as valid json, use double quote("") by escaping it. onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
		public async Task SetVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var key in keyValues)
			{				
				await SetVariable(key.Key, key.Value, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
			}
		}
		[Description(@"Set value on variables. If value is json, make sure to format it as valid json, use double quote("") by escaping it.  onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
		public async Task SetValuesOnVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var key in keyValues)
			{
				await SetVariable(key.Key, key.Value, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
			}

		}
		[Description(@"Set default value on variables if not set. If value is json, make sure to format it as valid json, use double quote("") by escaping it.  onlyIfValueIsSet can be define by user, null|""null""|""empty"" or value a user defines. Be carefull, there is difference between null and ""null"", to be ""null"" is must be defined by user.")]
		public async Task SetDefaultValueOnVariables([HandlesVariableAttribute] Dictionary<string, object?> keyValues, bool doNotLoadVariablesInValue = false, bool keyIsDynamic = false, object? onlyIfValueIsNot = null)
		{
			foreach (var key in keyValues)
			{
				var objectValue = memoryStack.GetObjectValue2(key.Key, false);
				if (!objectValue.Initiated)
				{
					await SetVariable(key.Key, key.Value, doNotLoadVariablesInValue, keyIsDynamic, onlyIfValueIsNot);
				}
			}

		}

		[Description("Append to variable. valueLocation=postfix|prefix seperatorLocation=end|start")]
		public async Task<object?> AppendToVariable([HandlesVariableAttribute] string key, [HandlesVariable] object? value = null, char seperator = '\n', 
			string valueLocation = "postfix", string seperatorLocation = "end", bool shouldBeUnique = false, bool doNotLoadVariablesInValue = false)
		{
			if (value == null) return value;

			value = (doNotLoadVariablesInValue) ? value : variableHelper.LoadVariables(value);

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
				((List<object>)val).Add(value);
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
			var settingKey = key.Substring(key.IndexOf('.') + 1).Replace("%", "");
			settings.Set(typeof(PLang.Services.SettingsService.Settings), settingKey, value);
		}

		[Description("Sets a value to %Settings.XXXX% variable but only if it is not set before")]
		public async Task SetDefaultSettingValue([HandlesVariableAttribute] string key, object value)
		{
			var settingKey = key.Substring(key.IndexOf('.') + 1).Replace("%", "");
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

