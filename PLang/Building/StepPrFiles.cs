using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Building
{
	// Keeps a step in 00. Goal.pr and the step's own .pr file describing the same build.
	public static class StepPrFiles
	{
		// The .pr file holds the function and the module it was built and validated for. When the goal lists
		// another module for the same step text, the instruction wins, as it does at runtime in
		// PrParser.ParseInstructionFile: the function only exists on the module it was built for.
		// Returns true when the step was changed.
		public static bool AlignModuleWithInstruction(GoalStep step, Instruction? instruction, ILogger? logger = null)
		{
			if (instruction == null || string.IsNullOrEmpty(instruction.ModuleType)) return false;
			if (string.Equals(instruction.ModuleType, step.ModuleType, StringComparison.Ordinal)) return false;
			if (instruction.Text != step.Text) return false;

			logger?.LogWarning($"  - Step '{step.Text?.Trim().MaxLength(60)}' is listed as {step.ModuleType} but {step.PrFileName} is built for {instruction.ModuleType}, the step uses {instruction.ModuleType}");
			step.ModuleType = instruction.ModuleType;
			return true;
		}

		public static string Prefix(int stepIndex) => (stepIndex + 1).ToString().PadLeft(2, '0');

		// A step whose text did not change but whose position did (a line was added or removed above it)
		// keeps its previous build: the .pr file is copied to the name for its new position, so the step is
		// not sent to the llm again for an answer it already has. Returns the indexes of the steps moved.
		//
		// Every file is read before any is written, so a step moving onto the name another moving step is
		// leaving cannot overwrite it first.
		public static List<int> MoveRenumberedSteps(Goal goal, IPLangFileSystem fileSystem, ILogger? logger = null)
		{
			var moves = new List<(int Index, GoalStep Step, string NewName, string Content, Instruction Instruction)>();
			for (int i = 0; i < goal.GoalSteps.Count; i++)
			{
				var step = goal.GoalSteps[i];
				if (string.IsNullOrEmpty(step.PrFileName) || string.IsNullOrEmpty(step.AbsolutePrFilePath)) continue;
				if (step.PrFileName.StartsWith(Prefix(i) + ".")) continue;

				var separator = step.PrFileName.IndexOf(". ", StringComparison.Ordinal);
				if (separator <= 0 || !step.PrFileName.Substring(0, separator).All(char.IsDigit)) continue;
				if (!fileSystem.File.Exists(step.AbsolutePrFilePath)) continue;

				var content = fileSystem.File.ReadAllText(step.AbsolutePrFilePath);
				var (instruction, error) = InstructionCreator.Create(step.AbsolutePrFilePath, fileSystem);
				if (error != null || instruction?.Function == null || string.IsNullOrEmpty(instruction.Function.Name)) continue;
				if (instruction.Text != step.Text) continue;

				moves.Add((i, step, Prefix(i) + step.PrFileName.Substring(separator), content, instruction));
			}

			if (moves.Count == 0) return new();

			if (!fileSystem.Directory.Exists(goal.AbsolutePrFolderPath))
			{
				fileSystem.Directory.CreateDirectory(goal.AbsolutePrFolderPath);
			}

			foreach (var move in moves)
			{
				var step = move.Step;
				logger?.LogInformation($"{step.LineNumber}: Step moved from {step.PrFileName} to {move.NewName}, its build is kept");

				step.PrFileName = move.NewName;
				step.AbsolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, move.NewName);
				step.RelativePrPath = Path.Join(goal.RelativePrFolderPath, move.NewName);
				step.Number = move.Index;
				AlignModuleWithInstruction(step, move.Instruction, logger);

				fileSystem.File.WriteAllText(step.AbsolutePrFilePath, move.Content);
			}
			return moves.Select(m => m.Index).ToList();
		}
	}
}
