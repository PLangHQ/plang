using System.Text;

namespace PLang.Runtime.Actors;

public class ChannelSettings
{
	public string? ContentType { get; set; }
	public Encoding? Encoding { get; set; }
}
