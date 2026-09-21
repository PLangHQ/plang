namespace PLang.Building.Model
{
	public record ModuleChoice(string Module, double Confidence, Dictionary<string, double> Probabilities);
}
