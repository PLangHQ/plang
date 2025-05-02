using PLang.Models;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Runtime;


public class ObjectValue
{
	private object? value;

	public static ObjectValue Null { get { return new ObjectValue("", null, null, null, false); } }
	public ObjectValue(string name, object? value, Type? type, ObjectValue? parent = null, bool Initiated = true, IProperties? properties = null)
	{
		Name = name.ToLower();
		this.value = value;
		Type = type;
		this.Initiated = Initiated;
		Parent = parent;
		Created = DateTime.Now;
		Updated = DateTime.Now;
	}

	public List<VariableEvent> Events = new List<VariableEvent>();
	public string Name { get; set; }
	public object? Value
	{
		get { return this.value; }
		set
		{
			this.value = value;
			Updated = DateTime.Now;

			if (Properties == null) return;

			Task.Run(() =>
			{
				foreach (var item in Properties)
				{
					if (item.Value is IDisposable disposable)
					{
						disposable.Dispose();
					}
				}
			});

		}
	}
	public Type? Type { get; set; }
	public bool Initiated { get; set; }
	public ObjectValue? Parent { get; set; }
	public IProperties? Properties { get; set; }
	public DateTime Created { get; private set; }
	public DateTime Updated { get; set; }

	public override string? ToString()
	{
		return Value?.ToString();
	}

	public T? To<T>(string path)
	{
		//user.name => find best way to retrieve it, is it Json, Dictionary, etc..
		return (T?)Value;
	}

	public object? Math(string math)
	{
		// using NCalc, use could say % user.age * 2 / 4 ^ 16 %, it would translate to user.Math("* 2 / 4 ^ 16")
		return null;
	}
}
