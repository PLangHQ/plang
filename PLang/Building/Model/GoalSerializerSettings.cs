using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace PLang.Building.Model
{
	internal class GoalSerializer
	{

		public static JsonSerializerSettings Settings
		{
			get
			{
				var settings = new JsonSerializerSettings
				{
					ContractResolver = new IgnoreWhenInstructedResolver(true),
					Formatting = Formatting.Indented
				};
				return settings;
			}
		}

		public static JsonSerializerOptions JsonSettings
		{
			get
			{
				var TypeInfoResolver = new DefaultJsonTypeInfoResolver
				{
					Modifiers =
					{
						typeInfo =>
						{
							foreach (var prop in typeInfo.Properties)
							{
								var pi = prop.AttributeProvider as System.Reflection.PropertyInfo;
								if (pi?.GetCustomAttributes(typeof(LlmIgnoreAttribute), true).Any() == true)
									prop.ShouldSerialize = (obj, value) => false;
							}
						}
					}
				};

				var options = new JsonSerializerOptions
				{
					WriteIndented = true ,
					TypeInfoResolver = TypeInfoResolver
				};
				return options;
			}
		}
	}
}
