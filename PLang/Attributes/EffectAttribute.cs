namespace PLang.Attributes
{
	[Flags]
	public enum Effects
	{
		None = 0,
		WritesData = 1,
		WritesFiles = 2,
		External = 4,
		Costs = 8,
		Opaque = 16,
		FromSql = 32
	}

	// Declares what a module method changes outside the running goal. Read by the route documentation.
	// On a class it is the default for every method in it, a method level attribute overrides it.
	// FromSql means the effect depends on the SQL text the step runs, so the SQL has to be inspected.
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class EffectAttribute(Effects effects) : Attribute
	{
		public Effects Effects { get; } = effects;
	}
}
