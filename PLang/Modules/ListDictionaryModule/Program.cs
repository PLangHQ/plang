using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;

namespace PLang.Modules.ListDictionaryModule
{
	[Description("Add, update, delete and retrieve list or dictionary. It can be stored as local list/directory or as static/global")]
	public class Program : BaseProgram
	{

		public Program() : base()
		{
		}

		public async Task<bool> DeleteFromList(object item, List<object> listInstance)
		{
			return listInstance.Remove(item);
		}


		public async Task<bool> DeleteKeyFromDictionary(string key, Dictionary<string, object> dictionary)
		{
			return dictionary.Remove(key);
		}

		[Description("Method always returns instance of listInstance, it creates a new instance if it is null. ReturnValue should always be used with AddToList")]
		public async Task<List<object>> AddToList(object? value, List<object>? listInstance = null)
		{
			if (value == null) return new();

			if (listInstance == null) listInstance = new List<object>();

			if (JsonHelper.IsJson(value.ToString()))
			{
				if (value.ToString().TrimStart().StartsWith("["))
				{
					List<dynamic> jsonList = JsonConvert.DeserializeObject<List<dynamic>>(value.ToString());
					listInstance.AddRange(jsonList);
				}
				else
				{
					var item = JsonConvert.DeserializeObject<dynamic>(value.ToString());
					listInstance.Add(item);
				}
			}
			else
			{
				listInstance.Add(value);
			}
			return listInstance;
		}

		[Description("Gets an item from a list by position")]
		public async Task<object?> GetFromList(int position, List<object>? listInstance = null)
		{
			if (position < 1) position = 1;
			if (listInstance == null) listInstance = new List<object>();
			return (listInstance.Count >= position) ? listInstance[position - 1] : null;
		}

		[Description("Method always returns instance of dictionaryInstance, it creates a new instance if it is null. ReturnValue should always be used with AddToDictionary")]
		public async Task<Dictionary<string, object>> AddToDictionary(string key, object value, Dictionary<string, object>? dictionaryInstance = null, bool updateIfExists = true)
		{
			if (value == null) return new();
			if (function != null && function.ReturnValue != null)
			{
				dictionaryInstance = memoryStack.Get(function.ReturnValue[0].VariableName) as Dictionary<string, object>;
			}
			if (dictionaryInstance == null) dictionaryInstance = new Dictionary<string, object>();

			if (updateIfExists)
			{
				dictionaryInstance.AddOrReplace(key, value);
			}
			else
			{
				if (!dictionaryInstance.ContainsKey(key))
				{
					dictionaryInstance.Add(key, value);
				}
			}
			return dictionaryInstance;
		}
		[Description("Gets an object from dictionary. ReturnValue should always be used with GetFromDictionary")]
		public async Task<object?> GetFromDictionary(string key, Dictionary<string, object>? dictionaryInstance = null)
		{
			if (dictionaryInstance == null) dictionaryInstance = new Dictionary<string, object>();

			if (dictionaryInstance.ContainsKey(key))
			{
				return dictionaryInstance[key];
			}
			return null;
		}
	}
}

