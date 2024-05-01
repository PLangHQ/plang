using System.Text;

namespace PLang.Errors
{
	public class ErrorList<T> : List<T> where T : Error
	{

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			foreach (var error in this)
			{
				sb.Append(error.ToString() + Environment.NewLine);
			}
			return sb.ToString();
		}
	}
}
