using Newtonsoft.Json.Linq;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors
{
	internal class ListObjectExtractor : IExtractor
	{
		private IEnumerable<object> list;
		private readonly ObjectValue parent;

		public ListObjectExtractor(IEnumerable<object> list, ObjectValue parent)
		{
			this.list = list;
			this.parent = parent;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			if (list == null) return ObjectValue.Nullable(segment.Value);

			var property = list.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase));
			if (property != null)
			{
				return new ObjectValue(segment.Value, property.GetValue(list), parent: parent, properties: parent.Properties);
			}

			int idx = 0;
			List<object> newList = new();
			foreach (var item in list)
			{
				ObjectValue? ov = item as ObjectValue;
				
				if (ov != null && ov.Name.Equals(segment.Value, StringComparison.OrdinalIgnoreCase))
				{
					newList.Add(ov);
				}
				else
				{
					var extractor = ExtractorFactory.GetExtractor(new ObjectValue($"[{idx++}]", item, parent: parent, properties: parent.Properties), segment);
					var obj = extractor.Extract(segment, memoryStack);
					if (obj == null) continue;
					
					newList.Add(obj);
					
				}
			}

			if (newList.Count == 1)
			{
				return new ObjectValue(segment.Value, newList[0], parent: parent, properties: parent?.Properties);
			}
			return new ObjectValue(segment.Value, newList, parent: parent, properties: parent?.Properties);
		}
	}
}
