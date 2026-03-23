using System.Text.Json;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Events;
using Scriban;

namespace PLang.Runtime2.Engine.Goals.Goal;

public sealed partial class @this
{
    public async Task<Data> Load(PLangContext context)
    {
        var lifecycle = context.LifecycleFor(this);
        var before = await lifecycle.Before.Run(context, EventType.OnBeforeGoalLoad);
        if (!before.Success) return before;

        var stepsResult = await Steps.Load(context);
        if (!stepsResult.Success) return stepsResult;

        var after = await lifecycle.After.Run(context, EventType.OnAfterGoalLoad);
        return after;
    }

    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        var savedGoal = context.Goal;
        var savedStep = context.Step;
        var savedConfigScope = context.ConfigScope;

        context.Goal = this;
        context.ConfigScope = null; // new goal starts with no local settings; inherits via Parent chain

        if (cancellationToken.IsCancellationRequested)
            return Data.FromError(GoalError.Cancelled(context));

        var lifecycle = context.LifecycleFor(this);

        Data beforeResult;
        try
        {
            beforeResult = await lifecycle.Before.Run(context, EventType.BeforeGoal);
        }
        catch (Exception ex)
        {
            var eventError = new GoalError($"BeforeGoal event failed: {ex.Message}", context, "EventError", 500) { Exception = ex };
            context.CallStack?.AddError(eventError);
            return Data.FromError(eventError);
        }
        if (!beforeResult) return beforeResult;
        if (beforeResult.Handled) return beforeResult;

        context.CallStack?.Push(this);

        try
        {
            var stepsResult = await Steps.RunAsync(engine, context, cancellationToken);
            if (!stepsResult) return stepsResult;

            try
            {
                var afterResult = await lifecycle.After.Run(context, EventType.AfterGoal);
                if (!afterResult) return afterResult;
            }
            catch (Exception ex)
            {
                var eventError = new GoalError($"AfterGoal event failed: {ex.Message}", context, "EventError", 500) { Exception = ex };
                context.CallStack?.AddError(eventError);
                return Data.FromError(eventError);
            }

            return stepsResult;
        }
        finally
        {
            if (context.CallStack != null) await context.CallStack.PopAsync();
            context.ConfigScope = savedConfigScope;
            context.Goal = savedGoal;
            context.Step = savedStep;
        }
    }


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
		var jsonOpts = new JsonSerializerOptions { WriteIndented = false };

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
		var jsonOpts = new JsonSerializerOptions { WriteIndented = false };

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
