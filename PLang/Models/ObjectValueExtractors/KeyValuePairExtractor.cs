using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors
{
	internal class KeyValuePairExtractor<TKey, TValue> : IExtractor

	{
		TKey key;
		TValue value;
		private readonly ObjectValue parent;

		public KeyValuePairExtractor(TKey key, TValue value, ObjectValue parent)
		{
			this.key = key;
			this.value = value;
			this.parent = parent;
		}


		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			if (segment.Value.Equals("key", StringComparison.OrdinalIgnoreCase))
			{
				return new ObjectValue("key", key, parent: parent, properties: parent.Properties);
			}

			if (segment.Value.Equals("items", StringComparison.OrdinalIgnoreCase))
			{
				return new ObjectValue("items", value, parent: parent, properties: parent.Properties);
			}
			if (segment.Value.Equals("value", StringComparison.OrdinalIgnoreCase))
			{
				return new ObjectValue("value", value, parent: parent, properties: parent.Properties);
			}

			if (value is IList list)
			{
				var extractor = new ListExtractor(list.Cast<object>(), parent);
				return extractor.Extract(segment, memoryStack);
			}

			throw new Exception($@"KeyValuePairExtractor is only implemented for Dictionary. {ErrorReporting.CreateIssueNotImplemented}.
This is the value that is being extracted:
Type: {value?.GetType()}
Value:{JsonConvert.SerializeObject(value)}");
			
		}
	}
}
