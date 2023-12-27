namespace PLang.Interfaces
{
	public record PermissionValue(object Permissions, string? Hash = null);

	public interface IPermission
	{
		public PermissionValue Permission { get; set; }
	}
}
