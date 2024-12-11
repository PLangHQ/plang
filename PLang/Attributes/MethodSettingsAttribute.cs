namespace PLang.Attributes
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class MethodSettingsAttribute : Attribute
	{
		public bool CanBeCached { get; set; }
		public bool CanHaveErrorHandling { get; set; }
		public bool CanBeAsync { get; set; }
		public bool CanBeCancelled { get; set; }
		public bool ExcludeFromBuild { get; set; }

		public MethodSettingsAttribute(bool canBeCached = true, bool canHaveErrorHandling = true, bool canBeAsync = true, bool canBeCancelled = true, bool excludeFromBuild = false)
		{
			CanBeCached = canBeCached;
			CanHaveErrorHandling = canHaveErrorHandling;
			CanBeAsync = canBeAsync;
			CanBeCancelled = canBeCancelled;
			ExcludeFromBuild = excludeFromBuild;
		}
	}
}
