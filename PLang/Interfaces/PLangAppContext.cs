using PLang.Services.OutputStream;
using PLang.Utils;
using System.Collections.Concurrent;

namespace PLang.Interfaces
{
	public class PLangAppContext : ConcurrentDictionary<string, object?>
	{
		public Output Output { get; set; }
		public string? Environment { get; set; }

		public new object? this[string key]
		{
			get
			{
				if (TryGetValue(key, out var value)) return value;
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


			this.AddOrUpdate(key, value, (str, obj1) =>
			{
				return value;
			});

		}

		public void Remove(string key)
		{
			this.Remove(key, out var _);
		}

		public T? GetOrDefault<T>(string key, T? defaultValue = default(T))
		{
			if (key == null) return defaultValue;

			if (ContainsKey(key))
			{
				return (T?)base[key];
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
				return this.FirstOrDefault(p => p.Key.ToLower() == key.ToLower()).Key != null;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public bool ContainsKey(string key, out object? obj)
		{
			key = key.Replace("%", "");
			var keyValue = this.FirstOrDefault(p => p.Key.ToLower() == key.ToLower());
			if (keyValue.Key == null)
			{
				obj = null;
				return false;
			}
			obj = keyValue;
			return true;
		}

		public Dictionary<string, object?> GetReserverdKeywords()
		{
			var dict = new Dictionary<string, object?>();
			var keywords = ReservedKeywords.Keywords;
			foreach (var keyword in keywords)
			{
				if (this.ContainsKey(keyword))
				{
					dict.Add(keyword, base[keyword]);
				}
			}
			return dict;
		}
	}
}
