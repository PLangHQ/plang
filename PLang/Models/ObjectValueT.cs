using ADRaffy.ENSNormalize;
using Newtonsoft.Json.Linq;
using PLang.Runtime;

namespace PLang.Models
{
	public class ObjectValue<T> : ObjectValue
	{
		public T Data
		{
			get => (T)Value;
			set => Value = value;
		}

		public ObjectValue(object data) : base(data.GetHashCode().ToString(), data)
		{
			Data = (T)data;
		}

		public ObjectValue(string name, T data) : base(name, data)
		{
			Data = data;
		}
	}
}
