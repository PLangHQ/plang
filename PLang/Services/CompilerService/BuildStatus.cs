namespace PLang.Services.CompilerService
{
	public class BuildStatus
	{
		public BuildStatus() { }
		public BuildStatus(Implementation? implementation, string? error = null) {
			this.Implementation = implementation;
			this.Error = error;
		}
		public Implementation? Implementation { get; set; }
		public string? Error { get; set; } = null;
	}
}
