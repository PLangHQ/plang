using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules.DbModule
{
	using Newtonsoft.Json;
	using System.Dynamic;

	public class Table : List<Row>
	{
		private List<string> cols;

		public Table(List<string> cols)
		{
			this.cols = cols;
		}

		public List<string> ColumnNames
		{
			get
			{
				return cols;
			}
			set
			{
				cols = value;
			}
		}

		public object this[string columnName]
		{
			get
			{
				if (this[0].TryGetValue(columnName, out var value)) return value;

				var actualColumnName = this[0].Columns.FirstOrDefault(p => p.Equals(columnName, StringComparison.OrdinalIgnoreCase));
				if (actualColumnName == null) throw new KeyNotFoundException($"Column '{columnName}' does not exist.");
				return this[0][actualColumnName];
			}
		}
	}

	public interface IPlangComparer
	{
		bool Contains(string str);

	}

	public class Row : Dictionary<string, object?>, IPlangComparer
	{
		private readonly Table table;

		public Row(Table table) : base(StringComparer.OrdinalIgnoreCase)
		{
			this.table = table;
		}

		public IEnumerable<string> Columns
		{
			get { return this.Keys.Select(p => p); }
		}

		public new object? this[string key]
		{
			get
			{
				if (TryGetValue(key, out var value)) return value;

				if (key.Equals("!row", StringComparison.OrdinalIgnoreCase))
				{
					return JsonConvert.SerializeObject(this);
				}

				var actualColumnName = table.ColumnNames.FirstOrDefault(p => p.Equals(key, StringComparison.OrdinalIgnoreCase));
				if (actualColumnName == null) throw new KeyNotFoundException($"Column '{key}' does not exist.");
				return this[actualColumnName];
				
			}
			set => base[key] = value;
		}

		internal T? Get<T>(string v)
		{
			return (T?)this[v];
		}

		public bool Contains(string str)
		{
			foreach (var column in Columns)
			{
				if (TryGetValue(column, out var value))
				{
					if (value?.ToString()?.Contains(str, StringComparison.OrdinalIgnoreCase) == true) return true;
				}
			}
			return false;
		}
	}

}
