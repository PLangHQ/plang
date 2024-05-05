using System.Text;

namespace PLang.Errors
{
	public class ErrorList<T> : List<T> where T : Error
	{

		public object ToFormat()
		{
			StringBuilder sb = new StringBuilder();
			foreach (var error in this)
			{
				sb.Append(error.ToFormat() + Environment.NewLine);
			}
			return sb.ToString();
		}
	}
}
