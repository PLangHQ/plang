using PLang.Errors;
using PLang.Runtime;
using System;
using System.Collections;

namespace PLang.Models.ObjectValueConverters
{
	internal class ListConverter
	{

		public static IList? GetList(IList? list, Type typeTo)
		{
			Type baseType = typeTo.GenericTypeArguments[0] ?? typeof(object);
			if (baseType == typeof(ObjectValue))
			{
				return list;
			}

			if (list == null) throw new Exception($"list is empty.{ErrorReporting.CreateIssueShouldNotHappen}");
			if (list.Count == 0) return Activator.CreateInstance(typeTo) as IList;

			IList? newList;
			if (typeTo.Name.StartsWith("IList") && typeTo.FullName.Contains("ObjectValue"))
			{
				var objectValueList = list as List<ObjectValue>;
				ObjectValue? firstItem = objectValueList.FirstOrDefault(p => p.Value != null);
				if (firstItem == null || firstItem.Value == null) return Activator.CreateInstance(typeTo) as IList;

				var listType = typeof(List<>).MakeGenericType(firstItem.Value.GetType());
				newList = Activator.CreateInstance(listType) as IList;

				baseType = firstItem.Value.GetType();
			}
			else
			{
				newList = Activator.CreateInstance(typeTo) as IList;
			}

			if (newList == null) throw new Exception("Could not create instance of list");

			var addMethod = newList.GetType().GetMethod("Add");
			if (addMethod == null) throw new Exception("Could find Add method on list instance");

			for (int i = 0; list != null && i < list.Count; i++)
			{
				if (baseType == typeof(ObjectValue))
				{
					addMethod.Invoke(newList, new object[] { list[i] });
					continue;
				}

				object? obj;
				if (list[i] is ObjectValue ov)
				{
					obj = ObjectValueConverter.Convert(ov, baseType);
					if (obj == null) continue;
				}
				else
				{
					if (baseType == list[i].GetType())
					{
						obj = list[i];
					}
					else
					{
						obj = Convert.ChangeType(list[i], baseType);
					}
				}

				addMethod.Invoke(newList, new object[] { obj });

				/*if (obj != null && typeTo.GenericTypeArguments.Count() > 0 && typeTo.GenericTypeArguments[0] == typeof(string))
				{
					addMethod.Invoke(newList, new object[] { obj.ToString() });
				}
				else
				{
					
				}*/
			}
			return newList;
		}
	}
}
