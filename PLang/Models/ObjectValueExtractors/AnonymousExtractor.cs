using Nethereum.Contracts.QueryHandlers.MultiCall;
using Newtonsoft.Json.Linq;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors
{
	internal class AnonymousExtractor : IExtractor
	{
		private object dynamic;
		private readonly ObjectValue parent;

		public AnonymousExtractor(dynamic dynamic, ObjectValue parent)
		{
			this.dynamic = dynamic;
			this.parent = parent;
		}


		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			if (dynamic == null) return null;

			if (segment.Type == SegmentType.Index) throw new NotImplementedException("Is Index on AnonymousExtractor");

			var property = dynamic.GetType().GetProperties()
				.FirstOrDefault(p => p.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase));
			if (property == null)
			{
				if (dynamic is not Array array) return null;

				if (array.Length == 0 || array.GetValue(0) == null) return null;

				List<object?> list = new();
				property = array.GetValue(0).GetType().GetProperties()
					.FirstOrDefault(p => p.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase));
				if (property == null) return null;

				foreach (var item in array)
				{
					list.Add(property.GetValue(item));
				}
				return new ObjectValue(property.Name, list, parent: parent, properties: parent.Properties);
			}

			var newObj = property.GetValue(dynamic);
			return new ObjectValue(property.Name, newObj, parent: parent, properties: parent.Properties);
		}
	}
}
