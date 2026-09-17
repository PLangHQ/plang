namespace PLang.Building.Model
{
	// Probabilities are kept because two options can mean the same outcome: leaving an optional
	// parameter unset and choosing its default value are the same instruction, so the engine
	// splitting its probability between them is not uncertainty and must not read as low confidence.
	public record ParameterChoice(string Choice, double Confidence, Dictionary<string, double>? Probabilities = null);
}
