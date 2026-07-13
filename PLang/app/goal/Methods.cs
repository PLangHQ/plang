using System.Text.Json;
using Scriban;

namespace app.goal;

public sealed partial class @this
{
	public async Task<string> FormatForLlm(actor.context.@this? context = null)
	{
		if (context?.App == null)
			return FormatForLlmFallback();

		var templatePath = global::app.type.item.path.@this.Resolve(
			"/system/builder/templates/goalFormatForLlm.template", context);
		var exists = await templatePath.ExistsAsync();
		if (!exists.Success || (await exists.Value())?.Value != true)
			return FormatForLlmFallback();

		var read = await templatePath.ReadText();
		if (!read.Success) return FormatForLlmFallback();
		var templateText = (await read.Value())?.ToString() ?? "";
		var scribanTemplate = Scriban.Template.Parse(templateText);
		if (scribanTemplate.HasErrors)
			return FormatForLlmFallback();

		var data = BuildFormatData(context);
		var result = await scribanTemplate.RenderAsync(data);
		return result.Trim();
	}

	private object BuildFormatData(actor.context.@this? context)
	{
		return new
		{
			name = Name,
			comment = Comment,
			steps = Steps.Select(s => new
			{
				indent = s.Indent,
				text = s.Text,
				comment = s.Comment,
				has_actions = s.Actions.Count > 0,
				actions_json = s.Actions.Count > 0
					? FormatActionsJson(s.Actions, context)
					: null
			}).ToList(),
			has_errors = Errors.Count > 0,
			errors = Errors.Select(e => new { key = e.Key, message = e.Message }).ToList(),
			has_warnings = Warnings.Count > 0,
			warnings = Warnings.Select(w => new { key = w.Key, message = w.Message }).ToList()
		};
	}

	// The pr preview a step shows the builder LLM: {actions:[{module, action, parameters:[{name, value}]}]}.
	// Each param value writes ITSELF through the one json writer — dict→{}, list→[], leaf→its own form,
	// [Sensitive] masked via the value's own Output — instead of raw STJ reflecting the item's C# surface.
	private string FormatActionsJson(global::app.goal.steps.step.actions.@this actions, actor.context.@this? context)
	{
		using var buffer = new System.IO.MemoryStream();
		using (var utf8 = new Utf8JsonWriter(buffer))
		{
			var w = new global::app.channel.serializer.json.Writer(
				utf8, view: global::app.View.Out, renderers: context?.App?.Type.Renderer, emitsSchema: false);
			w.BeginObject();
			w.Name("actions");
			w.BeginArray(actions.Count);
			foreach (var a in actions)
			{
				w.BeginObject();
				w.Name("module"); w.String(a.Module);
				w.Name("action"); w.String(a.ActionName);
				w.Name("parameters");
				w.BeginArray(a.Parameters.Count);
				foreach (var p in a.Parameters)
				{
					w.BeginObject();
					w.Name("name"); w.String(p.Name);
					w.Name("value"); w.Value(p.Peek());
					w.EndObject();
				}
				w.EndArray();
				w.EndObject();
			}
			w.EndArray();
			w.EndObject();
		}
		return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
	}

	private string FormatForLlmFallback()
	{
		var sb = new System.Text.StringBuilder();

		if (!string.IsNullOrEmpty(Comment))
			sb.AppendLine($"/ {Comment}");
		sb.AppendLine(Name);

		foreach (var step in Steps)
		{
			if (!string.IsNullOrEmpty(step.Comment))
				sb.AppendLine($"/ {step.Comment}");

			var prefix = new string(' ', step.Indent) + "- ";
			var prInfo = step.Actions.Count > 0
				? FormatActionsJson(step.Actions, null)
				: "null";
			sb.AppendLine($"{prefix}{step.Text}  <= pr: {prInfo}");
		}

		if (Errors.Count > 0)
		{
			sb.AppendLine();
			sb.AppendLine("errors:");
			foreach (var e in Errors)
				sb.AppendLine($"- {{key: \"{e.Key}\", message: \"{e.Message}\"}}");
		}

		if (Warnings.Count > 0)
		{
			sb.AppendLine();
			sb.AppendLine("warnings:");
			foreach (var w in Warnings)
				sb.AppendLine($"- {{key: \"{w.Key}\", message: \"{w.Message}\"}}");
		}

		return sb.ToString().TrimEnd();
	}
}
