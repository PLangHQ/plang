using Newtonsoft.Json.Linq;
using PLang.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueConverters
{
	public static class ObjectValueConverter
	{

		public static T? Convert<T>(ObjectValue? objectValue)
		{
			var obj = Convert(objectValue, typeof(T));
			if (obj == null) return default;
			return (T?)obj;
		}

		public static object? Convert(ObjectValue? objectValue, Type type)
		{
			if (objectValue == null || objectValue.Value == null) return null;

			if (objectValue.Value is JToken jToken)
			{
				return jToken.ToObject(type);
			}
			if (objectValue.Value is IList && (type.Name.StartsWith("List`") || type.Name.StartsWith("IList")))
			{
				return ListConverter.GetList(objectValue.Value as IList, type);
			}
			
			if (objectValue.Value is IList list && list.Count > 0 && !type.Name.StartsWith("List`") && !type.Name.StartsWith("IList"))
			{
				if (list[0] is ObjectValue ov)
				{
					return System.Convert.ChangeType(ov.Value, type);
				}
				else
				{
					return System.Convert.ChangeType(list[0], type);
				}
			}
			

			if (type != typeof(ObjectValue) && objectValue.Value is ObjectValue ov2)
			{
				if (type.IsInstanceOfType(ov2.Value))
				{
					return ov2.Value;
				}
				return System.Convert.ChangeType(ov2.Value, type);
			}

			if (type.IsInstanceOfType(objectValue.Value))
			{
				return objectValue.Value;
			}

			return System.Convert.ChangeType(objectValue.Value, type);
		}

	}
}
