﻿using Microsoft.IdentityModel.Tokens;
using Namotion.Reflection;
using Newtonsoft.Json.Linq;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PLang.Models.ObjectValueExtractors
{


	public class JsonExtractor : IExtractor
	{
		private JToken jToken;
		private readonly ObjectValue parent;

		public JsonExtractor(JToken jToken, ObjectValue parent)
		{
			this.jToken = jToken;
			this.parent = parent;
		}

		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			if (jToken is JObject jObject)
			{
				var token = jObject.Properties()
					   .FirstOrDefault(p => string.Equals(p.Name, segment.Value, StringComparison.OrdinalIgnoreCase))
					   ?.Value;
				if (token == null) return null;
			
				var ov = new ObjectValue(segment.Value, token, parent: parent, properties: parent.Properties);
				return ov;
			}
			else if (jToken is JArray jArray)
			{
				if (segment.Type == SegmentType.Index)
				{
					int index = 0;
					if (!int.TryParse(segment.Value, out index))
					{
						if (memoryStack == null) throw new Exception("MemoryStack cannot be null when searching for variable in " + segment.Value);

						index = memoryStack.Get<int>(segment.Value);
					}

					if (jArray.Count > index)
					{
						return new ObjectValue(segment.Value, jArray[index], parent: parent, properties: parent.Properties);
					}
					return null;
				}

				int counter = 0;
				List<JToken> tokens = new();
				foreach (var item in jArray)
				{
					var token = (item as JObject)?.Properties()
					   .FirstOrDefault(p => string.Equals(p.Name, segment.Value, StringComparison.OrdinalIgnoreCase))
					   ?.Value;
					if (token == null) continue;
					
					if (token is JArray || jArray.Count > 1)
					{
						tokens.Add(token);
					} else
					{					
						var parentOfToken = new ObjectValue($"{parent.Name}[{counter}]", item, parent: parent, properties: parent.Properties);
						return new ObjectValue(segment.Value, token, parent: parentOfToken, properties: parentOfToken.Properties);
					}
					counter++;


				}
				object? obj = null;
				if (segment.Value.Equals("first", StringComparison.OrdinalIgnoreCase)) obj = jArray.FirstOrDefault();
				if (segment.Value.Equals("random", StringComparison.OrdinalIgnoreCase)) obj = jArray.OrderBy(x => Guid.NewGuid()).ToList();
				if (segment.Value.Equals("last", StringComparison.OrdinalIgnoreCase)) obj = jArray.LastOrDefault();
				if (segment.Value.Equals("count", StringComparison.OrdinalIgnoreCase)) obj = jArray.Count();
				if (obj != null)
				{
					return new ObjectValue(segment.Value, obj, parent: parent, properties: parent.Properties);
				}
				if (tokens.Count == 0)
				{
					var objectExtractor = new ObjectExtractor(jArray, parent);
					var ov = objectExtractor.Extract(segment, memoryStack);
					return ov;
				}

				return new ObjectValue(segment.Value, tokens, parent: parent, properties: parent.Properties);
			} else
			{
				string str = jToken?.ToString() ?? string.Empty;
				return new ObjectValue(segment.Value, str, parent: parent, properties: parent.Properties);
			}

		}
	}
}
