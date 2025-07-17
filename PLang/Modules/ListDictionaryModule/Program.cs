using Microsoft.CodeAnalysis.CSharp.Syntax;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Tls;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Runtime;
using PLang.Utils;
using System.Collections;
using System.ComponentModel;
using System.Linq;

namespace PLang.Modules.ListDictionaryModule
{
	[Description(@"
get first|last|random|position| item from list or dictionary.
Add, update, delete and retrieve list or dictionary. Group by key, merge two lists
")]
	public class Program : BaseProgram
	{

		public Program() : base()
		{
		}

		public async Task ClearList(List<object> listInstance)
		{
			if (listInstance == null) return;

			listInstance.Clear();
		}
		public async Task ClearDictionary(Dictionary<string, object> dictionary)
		{
			if (dictionary == null) return;

			dictionary.Clear();
		}

		public async Task<bool> DeleteFromList(object item, List<object> listInstance)
		{
			return listInstance.Remove(item);
		}


		public async Task<bool> DeleteKeyFromDictionary(string key, Dictionary<string, object> dictionary)
		{
			return dictionary.Remove(key);
		}
		[Description("Method always returns instance of listInstance, it creates a new instance if it is null. ReturnValue should always be used with AddItemsToList")]
		public async Task<List<object>> AddItemsToList(List<object?> value, List<object>? listInstance = null)
		{
			if (value == null) return new();

			if (listInstance == null) listInstance = new List<object>();

			listInstance.AddRange(value);

			return listInstance;
		}

		[Description("Method always returns instance of listInstance, it creates a new instance if it is null. ReturnValue MUST always be defined")]
		public async Task<List<object>> AddToList(object? value, List<object>? listInstance = null, bool uniqueValue = false, bool caseSensitive = false)
		{
			if (value == null) return new();

			if (listInstance == null) listInstance = new List<object>();

			if (JsonHelper.IsJson(value.ToString()))
			{
				if (value.ToString().TrimStart().StartsWith("["))
				{
					List<dynamic> jsonList = JsonConvert.DeserializeObject<List<dynamic>>(value.ToString());
					if (jsonList == null) return listInstance;

					if (!uniqueValue || (uniqueValue && !ContainsItemInListInstance(listInstance, jsonList, caseSensitive)))
					{
						listInstance.AddRange(jsonList);
					}
				}
				else
				{

					var item = JsonConvert.DeserializeObject<dynamic>(value.ToString());
					if (!uniqueValue || (uniqueValue && !ContainsItemInListInstance(listInstance, item, caseSensitive)))
					{
						if (item is IList list)
						{
							if (list.Count > 0)
							{
								listInstance.AddRange(list);
							}
						}
						else
						{
							listInstance.Add(item);
						}
					}
				}
			}
			else
			{
				if (!uniqueValue || (uniqueValue && !ContainsItemInListInstance(listInstance, value, caseSensitive)))
				{
					if (value is IList list)
					{
						if (list.Count > 0)
						{
							listInstance.AddRange(value);
						}
					}
					else
					{
						listInstance.Add(value);
					}
				}
			}
			return listInstance;
		}

		private bool ContainsItemInListInstance(List<object?> listInstance, object value, bool caseSensitive)
		{
			if (caseSensitive) return listInstance.Contains(value);
			var item = listInstance.FirstOrDefault(p => p != null && p.ToString().Equals(value.ToString(), StringComparison.OrdinalIgnoreCase));
			return item != null;
		}

		public async Task<object?> GetItem(string @operator = "first", List<object>? listInstance = null, List<string>? sortColumns = null, List<string>? sortOperator = null)
		{
			if (listInstance == null || listInstance.Count == 0) return null;

			if (sortColumns != null && sortColumns.Count > 0)
			{
				throw new NotImplementedException();
			}
			object? obj = null;
			switch (@operator)
			{
				case "first":
					obj = listInstance[0];
					break;
				case "last":
					obj = listInstance[listInstance.Count - 1];
					break;
				case "random":
					obj = listInstance[new Random().Next(0, listInstance.Count)];
					break;
				case "position":
					obj = listInstance[memoryStack.Get<int>("position")];
					break;
			}
			return obj;


		}

		[Description("Gets an item from a list by position")]
		public async Task<object?> GetFromList(int position, List<object>? listInstance = null)
		{
			if (position < 1) position = 1;
			if (listInstance == null) listInstance = new List<object>();
			return (listInstance.Count >= position) ? listInstance[position - 1] : null;
		}
		[Description("Method always returns instance of dictionaryInstance, it creates a new instance if it is null. ReturnValue should always be used with AddItemsToDictionary")]
		public async Task<Dictionary<string, object>> AddItemsToDictionary(string key, Dictionary<string, object> value, Dictionary<string, object>? dictionaryInstance = null, bool updateIfExists = true)
		{
			if (value == null) return new();
			if (function != null && function.ReturnValues != null)
			{
				dictionaryInstance = memoryStack.Get(function.ReturnValues[0].VariableName) as Dictionary<string, object>;
			}
			if (dictionaryInstance == null) dictionaryInstance = new Dictionary<string, object>();
			foreach (var item in value)
			{
				dictionaryInstance.Add(item.Key, item.Value);
			}
			return dictionaryInstance;

		}
		[Description("Method always returns instance of dictionaryInstance, it creates a new instance if it is null. ReturnValue should always be used with AddToDictionary")]
		public async Task<(Dictionary<string, object>?, IError?)> AddToDictionary(string key, object value, Dictionary<string, object>? dictionaryInstance = null, bool updateIfExists = true)
		{
			if (value == null) return new();
			if (function != null && function.ReturnValues != null)
			{
				var obj = memoryStack.Get(function.ReturnValues[0].VariableName);
				if (obj is JObject jObject)
				{
					dictionaryInstance = jObject.ToDictionary();
				}
				else
				{
					dictionaryInstance = obj as Dictionary<string, object>;
				}
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
			return (dictionaryInstance, null);
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

		private (JArray? Array, IError? Error) ToJArray(object? value)
		{
			JArray? array = null;
			if (value is string str1)
			{
				if (str1.TrimStart().StartsWith("["))
				{
					array = JArray.Parse(str1);
				}
				else if (str1.TrimStart().StartsWith("{"))
				{

					var obj = JObject.Parse(str1);
					array = new JArray(obj);
				}
				else
				{
					return (null, new ProgramError($"Dont know how to convert value: {value}"));
				}
				return (array, null);
			}
			return (null, new ProgramError($"Dont know how to convert value: {JsonConvert.SerializeObject(value)}"));
		}


		[Description("Group a %variable% by key")]
		public async Task<(object?, IError?)> GroupBy(object? obj, string key)
		{
			if (obj is IList list && list.Count > 0)
			{
				var dict = TypeHelper.AsDictionary(list[0]);
				if (dict == null) return (null, new Error(ErrorReporting.CreateIssueNotImplemented));
				
				Dictionary<object, List<object>> groupedDict = new();
				foreach (var item in list)
				{
					var dictItem = TypeHelper.AsDictionary(item);

					if (dictItem == null) return (null, new ProgramError($"Only dictionary in list objects are supported {ErrorReporting.CreateIssueNotImplemented}"));
					if (!dictItem.ContainsKey(key)) continue;

					object keyValue = dictItem[key];

					groupedDict.TryGetValue(keyValue, out List<object>? groupedList);
					if (groupedList == null) groupedList = new List<object>();

					groupedList.Add(dictItem);
					groupedDict.AddOrReplace(keyValue, groupedList);
				}

				return (groupedDict, null);
			}
			
			return (null, new Error(ErrorReporting.CreateIssueNotImplemented));
		}

		[Description("Merges two objects or lists according to primary key")]
		public async Task<(object?, IError?)> MergeLists(object? list1, object? list2, string key)
		{
			if (list1.GetType() == list2.GetType() && list1 is IList iList1 && list2 is IList iList2)
			{
				if (iList1.Count > iList2.Count)
				{
					foreach (var item in iList2)
					{
						iList1.Add(item);
					}
					return (iList1, null);
				} else
				{
					for (int i= iList1.Count-1;i>=0;i--)
					{
						iList2.Insert(0, iList1[i]);
					}

					return (iList2, null);
				}
			}

			if (list1 != null && list1 is not JArray)
			{
				var result = ToJArray(list1);
				if (result.Error != null) return (null, result.Error);
				list1 = result.Array;
			}
			if (list2 != null && list2 is not JArray)
			{
				var result = ToJArray(list2);
				if (result.Error != null) return (null, result.Error);
				list2 = result.Array;
			}

			if (list1 == null) list1 = new JArray();
			if (list2 == null) list2 = new JArray();

			if (list1 is JArray jArray1 && list2 is JArray jArray2)
			{
				JArray mergedArray = new JArray(jArray1.Concat(jArray2)
					.Select(token => token.ToString())  
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Select(str => (JToken)str));
				return (mergedArray, null);
			}
			return (null, new ProgramError("Could not merge objects"));

		}
	}
}

