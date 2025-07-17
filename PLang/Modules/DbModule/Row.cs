using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules.DbModule
{
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

	public class Row : Dictionary<string, object?>
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
		/*
DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(System.Linq.Expressions.Expression parameter)
	=> new RowMetaObject(parameter, this);

private class RowMetaObject : DynamicMetaObject
{
	public RowMetaObject(System.Linq.Expressions.Expression parameter, Row value)
		: base(parameter, System.Dynamic.BindingRestrictions.Empty, value) { }

	public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
	{
		var self = System.Linq.Expressions.Expression.Convert(Expression, LimitType);
		var keyExpr = System.Linq.Expressions.Expression.Constant(binder.Name);
		var tryGetValue = typeof(Row).GetMethod(nameof(TryGetValue));
		var valueVar = System.Linq.Expressions.Expression.Variable(typeof(object), "value");

		var body = System.Linq.Expressions.Expression.Block(
			new[] { valueVar },
			System.Linq.Expressions.Expression.Condition(
				System.Linq.Expressions.Expression.Call(self, tryGetValue, keyExpr, valueVar),
				valueVar,
				System.Linq.Expressions.Expression.Throw(
					System.Linq.Expressions.Expression.New(
						typeof(KeyNotFoundException).GetConstructor(new[] { typeof(string) }),
						System.Linq.Expressions.Expression.Constant($"Column '{binder.Name}' does not exist.")
					),
					typeof(object)
				)
			)
		);

		return new DynamicMetaObject(body, BindingRestrictions.GetTypeRestriction(Expression, LimitType));
	}
}*/
	}

}
