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
		public async Task OnChangeVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{

			memoryStack.AddOnChangeEvent(key, goalName, false, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
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
		public async Task OnChangeStaticVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnChangeEvent(key, goalName, true, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}
		[Description("goalName should be prefix with !, it can whole word only but can contain dot(.)")]
		public async Task OnRemoveStaticVariableListener([HandlesVariable] string key, string goalName, Dictionary<string, object>? parameters = null, bool waitForResponse = true, int delayWhenNotWaitingInMilliseconds = 50)
		{
			memoryStack.AddOnRemoveEvent(key, goalName, true, parameters, waitForResponse, delayWhenNotWaitingInMilliseconds);
		}

		public async Task<object?> LoadVariables([HandlesVariable] string key)
		{
			var content = memoryStack.Get(key);
			if (content == null) return null;
			
			return variableHelper.LoadVariables(content);
		}

		[Description("Set string local variable. If value is json, keep valid json format")]
		public async Task SetStringVariable([HandlesVariableAttribute] string key, string value)
		{
			object val = variableHelper.LoadVariables(value);
			memoryStack.Put(key, val, convertToJson: false);
		}

		[Description("Set local variable. If value is json, keep valid json format")]
		public async Task SetVariable([HandlesVariableAttribute] string key, object value)
		{
			object val = variableHelper.LoadVariables(value);
			memoryStack.Put(key, val);
		}

		[Description("Set default value on variables if not set. keys and values length MUST be equal. If value is json, keep valid json format")]
		public async Task SetDefaultValueOnVariables([HandlesVariableAttribute] Dictionary<string, object> keyValues)
		{
			foreach (var key in keyValues) 
			{
				var objectValue = memoryStack.GetObjectValue(key.Key, false);
				if (!objectValue.Initiated)
				{
					memoryStack.Put(key.Key, key.Value);
				}				
			}			
		}

		[Description("Set local variable.")]
		public async Task AppendToVariable([HandlesVariableAttribute] string key, object value, char seperator = '\n')
		{
			object val = memoryStack.Get(key);
			if (val == null && value is string)
			{
				val = "";
			} else if (val is string)
			{
				val = val + seperator.ToString() + value;
			} else
			{
				//todo: wrong thing 
				val = value;
			}
			
			memoryStack.Put(key, val);
		}
		public async Task<object> GetVariable([HandlesVariableAttribute] string key)
		{
			return memoryStack.Get(key);
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

		public async Task RemoveVariable([HandlesVariableAttribute] string key)
		{
			memoryStack.Remove(key);
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

