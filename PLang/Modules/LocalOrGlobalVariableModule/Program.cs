using Newtonsoft.Json;
using PLang.Attributes;
using System.ComponentModel;
using System.Dynamic;
using System.Security.Policy;
using System.Text;

namespace PLang.Modules.LocalOrGlobalVariableModule
{
	[Description("Set & Get local and static variables. Bind onCreate, onChange, onRemove events to variable.")]
	public class Program : BaseProgram
	{
		public Program() : base()
		{
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnCreateVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnCreateEvent(key, goalName, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnChangeVariableListener([HandlesVariable] string key, string goalName, bool notifyWhenCreated = true, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnChangeEvent(key, goalName, false, notifyWhenCreated, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
			if (notifyWhenCreated)
			{
				memoryStack.AddOnCreateEvent(key, goalName, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);

			}
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnRemoveVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
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
		[Description(@"Set string variable. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
		public async Task SetStringVariable([HandlesVariable] string key, string? value = null)
		{
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

		[Description(@"Set variable. If value is json, make sure to format it as valid json, use double quote("") by escaping it")]
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

		[Description("Append to variable.")]
		public async Task<object?> AppendToVariable([HandlesVariableAttribute] string key, object? value = null, char seperator = '\n')
		{
			if (value == null) return value;

			object? val = memoryStack.Get(key);
			if (val == null && value is string)
			{
				val = value.ToString();
			}
			else if (val is string)
			{
				val = val + seperator.ToString() + value;
			}
			else if (val is System.Collections.IList list)
			{
				list.Add(value);
			}
			else
			{
				throw new Exception("Cannot append to an object");
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
	

		public async Task<string> ConvertToBase64([HandlesVariableAttribute] string key)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(memoryStack.Get(key).ToString()));
		}
	}



}

