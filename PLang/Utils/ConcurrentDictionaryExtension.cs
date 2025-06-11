using PLang.Errors;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public static class ConcurrentDictionaryExtension
	{
		private static readonly Lock _lock = new();

		public static void AddOrReplaceDict<TKey, TValue>(this ConcurrentDictionary<TKey, TValue?>? dict, ConcurrentDictionary<TKey, TValue?>? dict2)
		{
			if (dict2 is null || dict is null) return;

			foreach (var item in dict2)
			{
				dict.AddOrReplace(item.Key, item.Value);
			}
		}
		public static void AddOrReplace<TKey, TValue>(this ConcurrentDictionary<TKey, TValue?>? dict, TKey key, TValue? value)
		{
			if (dict is null) return;

			dict.AddOrUpdate(key, addValueFactory: key => value, updateValueFactory: (key, existing) => value);
		}

		

	}
}
