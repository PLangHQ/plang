namespace PLang.Utils
{
	/// <summary>
	/// The start mode of this process, set from the start flags by RegisterStartupParameters.
	/// --no-scheduler: `start scheduler` does nothing until it is called with force or `enable scheduler` runs.
	/// --standby: implies --no-scheduler and skips the setup goals at start; `run setup` runs them later.
	/// Both are AppContext switches, so they are process wide and read the same from every engine.
	/// </summary>
	public static class RuntimeMode
	{
		public const string NoSchedulerSwitch = "!NoScheduler";
		public const string StandbySwitch = "!Standby";

		public const string DrainingSwitch = "!Draining";

		/// <summary>True from the start of `drain app`: the app is handing over to another process.</summary>
		public static bool Draining => IsSet(DrainingSwitch);
		public static void SetDraining(bool value) => AppContext.SetSwitch(DrainingSwitch, value);

		public static bool Standby => IsSet(StandbySwitch);
		public static bool SchedulerEnabled => !IsSet(NoSchedulerSwitch);

		public static void SetStandby(bool value) => AppContext.SetSwitch(StandbySwitch, value);
		public static void SetSchedulerEnabled(bool value) => AppContext.SetSwitch(NoSchedulerSwitch, !value);

		/// <summary>Reads the start flags. --standby implies --no-scheduler.</summary>
		public static void Register(string[] args)
		{
			bool standby = args.Any(p => p.Equals("--standby", StringComparison.OrdinalIgnoreCase));
			bool noScheduler = standby || args.Any(p => p.Equals("--no-scheduler", StringComparison.OrdinalIgnoreCase));
			SetStandby(standby);
			SetSchedulerEnabled(!noScheduler);
		}

		private static bool IsSet(string name) => AppContext.TryGetSwitch(name, out var value) && value;
	}
}
