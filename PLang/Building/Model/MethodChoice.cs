namespace PLang.Building.Model
{
	public record MethodChoice(string Method, double Confidence, Dictionary<string, double> Probabilities);
}
