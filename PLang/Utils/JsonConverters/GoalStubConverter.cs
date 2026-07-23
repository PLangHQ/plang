using Newtonsoft.Json;
using PLang.Building.Model;
using System;

namespace PLang.Utils.JsonConverters
{
	// Serializes Goal / GoalStep as a small stub instead of the full recursive tree.
	// A GoalStep back-references its Goal (only [System.Text.Json] JsonIgnore, not
	// Newtonsoft's), and carries NextStep/PreviousStep/LlmRequest/Instruction, so
	// Newtonsoft otherwise walks the whole goal graph -> %!memoryStack% balloons to
	// megabytes on error. We keep the identifying info and drop the rest.
	// Write-only: goals/steps only appear as variable values (object), so on the
	// round-trip deserialize they come back as plain JObject stubs.
	public class GoalStubConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(Goal).IsAssignableFrom(objectType) || typeof(GoalStep).IsAssignableFrom(objectType);
		}

		public override bool CanRead => false;

		public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteStartObject();
			if (value is Goal goal)
			{
				writer.WritePropertyName("GoalName");
				writer.WriteValue(goal.GoalName);
				writer.WritePropertyName("RelativeGoalPath");
				writer.WriteValue(goal.RelativeGoalPath);
			}
			else if (value is GoalStep step)
			{
				writer.WritePropertyName("Step");
				writer.WriteValue(step.Text);
				writer.WritePropertyName("Number");
				writer.WriteValue(step.Number);
			}
			writer.WriteEndObject();
		}

		public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
		{
			throw new NotSupportedException("GoalStubConverter is write-only.");
		}
	}
}
