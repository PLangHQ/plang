using System.Security.Cryptography;
using System.Text;
using PLang.Runtime2.Engine.Goals.Goal;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Represents the .goal file format. Parses .goal text into Runtime2 Goal and Step objects.
/// Instance class — a GoalFile IS the file format, holding parse state during parsing.
/// </summary>
public sealed class GoalFile
{
    /// <summary>
    /// Parses .goal file text into a list of Goals.
    /// All goals share the same Path (derived from the file they came from).
    /// First goal is Public, rest are Private.
    /// </summary>
    public List<Goal> Parse(string text, string path)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<Goal>();

        // Tabs → 4 spaces
        text = text.Replace("\t", "    ");

        var lines = text.Split('\n');
        var goals = new List<Goal>();
        var currentGoal = (Goal?)null;
        var currentStep = (Step?)null;
        var pendingComment = new StringBuilder();
        var inBlockComment = false;
        var stepIndex = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1; // 1-based
            var raw = lines[i].TrimEnd('\r');

            // Handle block comments
            if (inBlockComment)
            {
                var endIdx = raw.IndexOf("*/");
                if (endIdx >= 0)
                {
                    var closePart = raw[..endIdx].Trim();
                    if (closePart.Length > 0)
                    {
                        if (pendingComment.Length > 0) pendingComment.Append('\n');
                        pendingComment.Append(closePart);
                    }
                    inBlockComment = false;
                    // Process rest of line after */
                    var rest = raw[(endIdx + 2)..].Trim();
                    if (!string.IsNullOrEmpty(rest))
                    {
                        // Rare case — treat as continuation or new content
                    }
                }
                else
                {
                    if (pendingComment.Length > 0) pendingComment.Append('\n');
                    pendingComment.Append(raw.Trim());
                }
                continue;
            }

            // Check for block comment start
            var trimmed = raw.TrimStart();
            if (trimmed.StartsWith("/*"))
            {
                var afterOpen = trimmed[2..];
                var closeIdx = afterOpen.IndexOf("*/");
                if (closeIdx >= 0)
                {
                    // Single-line block comment
                    if (pendingComment.Length > 0) pendingComment.Append('\n');
                    pendingComment.Append(afterOpen[..closeIdx].Trim());
                }
                else
                {
                    inBlockComment = true;
                    if (pendingComment.Length > 0) pendingComment.Append('\n');
                    pendingComment.Append(afterOpen.Trim());
                }
                continue;
            }

            // Blank line — boundary for comment attribution
            if (string.IsNullOrWhiteSpace(raw))
            {
                pendingComment.Clear();
                currentStep = null;
                continue;
            }

            // Line comment: starts with /
            if (trimmed.StartsWith("/") && !trimmed.StartsWith("//"))
            {
                var commentText = trimmed[1..].TrimStart();
                if (pendingComment.Length > 0) pendingComment.Append('\n');
                pendingComment.Append(commentText);
                continue;
            }

            // Step line: starts with -
            if (trimmed.StartsWith("- ") || trimmed == "-")
            {
                if (currentGoal == null)
                {
                    // Step before any goal header — create implicit goal
                    currentGoal = new Goal
                    {
                        Name = "Start",
                        Visibility = goals.Count == 0 ? Visibility.Public : Visibility.Private,
                        Path = path
                    };
                    goals.Add(currentGoal);
                    stepIndex = 0;
                }

                // Calculate indent: count leading spaces before the dash
                var leadingSpaces = raw.Length - raw.TrimStart().Length;
                var indent = leadingSpaces / 4;

                var stepText = trimmed.Length > 2 ? trimmed[2..] : "";
                var comment = pendingComment.Length > 0 ? pendingComment.ToString() : null;
                pendingComment.Clear();

                currentStep = new Step
                {
                    Index = stepIndex,
                    Text = stepText,
                    LineNumber = lineNumber,
                    Indent = indent,
                    Comment = comment
                };

                currentGoal.Steps.Add(currentStep);
                stepIndex++;
                continue;
            }

            // Continuation line: indented, no dash, after a step
            if (currentStep != null && raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t'))
            {
                // Not a step (no dash) but indented — continuation of previous step
                currentStep = new Step
                {
                    Index = currentStep.Index,
                    Text = currentStep.Text + "\n" + trimmed,
                    LineNumber = currentStep.LineNumber,
                    Indent = currentStep.Indent,
                    Comment = currentStep.Comment
                };
                // Replace the last step in the goal
                currentGoal!.Steps[currentGoal.Steps.Count - 1] = currentStep;
                continue;
            }

            // Goal header — everything else
            var goalName = trimmed;
            var goalComment = pendingComment.Length > 0 ? pendingComment.ToString() : null;
            pendingComment.Clear();

            currentGoal = new Goal
            {
                Name = goalName,
                Comment = goalComment,
                Visibility = goals.Count == 0 ? Visibility.Public : Visibility.Private,
                Path = path
            };
            goals.Add(currentGoal);
            stepIndex = 0;
            currentStep = null;
        }

        // Populate SubGoals on the first (public) goal
        if (goals.Count > 1)
        {
            for (int i = 1; i < goals.Count; i++)
                goals[0].SubGoals.Add(goals[i].Name);
        }

        // Compute hash for each goal
        for (int i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];
            goals[i] = new Goal
            {
                Name = goal.Name,
                Description = goal.Description,
                Comment = goal.Comment,
                Steps = goal.Steps,
                SubGoals = goal.SubGoals,
                Visibility = goal.Visibility,
                Path = goal.Path,
                Hash = ComputeHash(goal),
                IsSetup = goal.Name.Equals("Setup", StringComparison.OrdinalIgnoreCase),
                InputParameters = goal.InputParameters,
                Errors = goal.Errors,
                Warnings = goal.Warnings
            };
        }

        return goals;
    }

    private static string ComputeHash(Goal goal)
    {
        var sb = new StringBuilder();
        sb.Append(goal.Name);
        foreach (var step in goal.Steps)
            sb.Append(step.Text);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
