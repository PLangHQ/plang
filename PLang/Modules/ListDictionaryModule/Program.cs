using Newtonsoft.Json;
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

		[Description("Method always returns instance of listInstance, it creates a new instance if it is null")]
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

		[Description("Method always returns instance of dictionaryInstance, it creates a new instance if it is null")]
		public async Task<Dictionary<string, object>> AddToDictionary(string key, object value, Dictionary<string, object>? dictionaryInstance = null, bool updateIfExists = false)
		{
			if (value == null) return new();

			if (dictionaryInstance == null) dictionaryInstance = new Dictionary<string, object>();
			
			if (updateIfExists)
			{
				dictionaryInstance.AddOrReplace(key, value);
			} else
			{
				dictionaryInstance.Add(key, value);
			}
			return dictionaryInstance;
		}
	}
}

