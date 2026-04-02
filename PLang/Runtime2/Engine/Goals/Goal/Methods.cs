using System.Text.Json;
using Scriban;

namespace PLang.Runtime2.Engine.Goals.Goal;

public sealed partial class @this
{
	public async Task<string> FormatForLlm(PLang.Interfaces.PLangContext? context = null)
	{
		var fs = context?.Engine?.FileSystem;
		if (fs == null)
			return FormatForLlmFallback();

		var templatePath = fs.Path.Combine(
			fs.RootDirectory, "system", "builder", "templates", "goalFormatForLlm.template");

		if (!fs.File.Exists(templatePath))
			return FormatForLlmFallback();

		var templateText = await fs.File.ReadAllTextAsync(templatePath);
		var scribanTemplate = Template.Parse(templateText);
		if (scribanTemplate.HasErrors)
			return FormatForLlmFallback();

		var data = BuildFormatData();
		var result = await scribanTemplate.RenderAsync(data);
		return result.Trim();
	}

	private object BuildFormatData()
	{
		var jsonOpts = JsonSerializerOptions.Default;

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
					? JsonSerializer.Serialize(
						new
						{
							actions = s.Actions.Select(a => new
							{
								module = a.Module,
								action = a.ActionName,
								parameters = a.Parameters.Select(p => new { name = p.Name, value = p.Value }),
								@return = a.Return?.Select(r => new { name = r.Name })
							})
						}, jsonOpts)
					: null
			}).ToList(),
			has_errors = Errors.Count > 0,
			errors = Errors.Select(e => new { key = e.Key, message = e.Message }).ToList(),
			has_warnings = Warnings.Count > 0,
			warnings = Warnings.Select(w => new { key = w.Key, message = w.Message }).ToList()
		};
	}

	private string FormatForLlmFallback()
	{
		var sb = new System.Text.StringBuilder();
		var jsonOpts = JsonSerializerOptions.Default;

		if (!string.IsNullOrEmpty(Comment))
			sb.AppendLine($"/ {Comment}");
		sb.AppendLine(Name);

		foreach (var step in Steps)
		{
			if (!string.IsNullOrEmpty(step.Comment))
				sb.AppendLine($"/ {step.Comment}");

			var prefix = new string(' ', step.Indent) + "- ";
			var prInfo = step.Actions.Count > 0
				? JsonSerializer.Serialize(
					new
					{
						actions = step.Actions.Select(a => new
						{
							module = a.Module,
							action = a.ActionName,
							parameters = a.Parameters.Select(p => new { name = p.Name, value = p.Value }),
							@return = a.Return?.Select(r => new { name = r.Name })
						})
					}, jsonOpts)
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
