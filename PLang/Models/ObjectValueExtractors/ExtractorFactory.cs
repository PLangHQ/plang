using AngleSharp.Common;
using Fizzler;
using Newtonsoft.Json.Linq;
using PLang.Models.ObjectTypes;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors
{
	internal static class ExtractorFactory
	{
		public static IExtractor GetExtractor(ObjectValue objectValue, PathSegment segment)
		{
			if (objectValue.Value == null) throw new Exception("objectValue is null, it cannot be null");

			object obj = objectValue.Value;
			if (segment.Type == SegmentType.Method)
			{
				return new MethodExtractor(obj, objectValue);
			}

			if (segment.Type == SegmentType.Math)
			{
				return new MathExpressionExtractor(segment.Value, objectValue);
			}

			var type = obj.GetType();
			if (obj is HtmlType htmlType || type.FullName.Contains("HtmlNode"))
			{
				return new HtmlExtractor(obj, objectValue);
			}
			if (obj is JToken jToken)
			{
				return new JsonExtractor(jToken, objectValue);
			}
			
			if (TypeHelper.ImplementsDict(obj, out IDictionary? dict) && dict != null)
			{				
				return new DictionaryExtractor(dict, objectValue);				
			}			
			
			if (ImplementsInterface("IList`1", type))
			{
				var list = obj as IList;
				if (list != null)
				{
					return new ListExtractor(list.Cast<object>(), objectValue);
				}
			}			
			
			if (type.Name.StartsWith("<>f__Anonymous"))
			{
				return new AnonymousExtractor(obj as dynamic, objectValue);
			}
			else
			{
				return new ObjectExtractor(obj, objectValue);
			}

			throw new NotImplementedException($"Not extractor for {type}");
		}

		private static bool ImplementsInterface(string interfaceName, Type type)
		{
			return (type.GetInterfaces().FirstOrDefault(p => p.Name.Equals(interfaceName)) != null);
		}

		public static IExtractor? GetListExtractor(object? obj, PathSegment segment, ObjectValue parent)
		{
			if (obj is not System.Collections.IEnumerable enumerable)
			{
				throw new Exception("This is not list");
			}

			var list = enumerable.Cast<object>();

			if (!list.Any()) return new ObjectExtractor(obj, parent);


			var supportedOps = new List<string>
			{
				"sum", "count", "avg", "average","mean", "max",  "min", "first", "last", "range","median","mode","percentile:<value>","stddev","variance"
			};
			var op = supportedOps.FirstOrDefault(p => p.Equals(segment.Value, StringComparison.OrdinalIgnoreCase));
			if (op != null) return new MathExtractor(op, list, parent);

			return new ListObjectExtractor(list, parent);
		}



		
	}
}
