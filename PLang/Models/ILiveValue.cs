namespace PLang.Models
{
	/// <summary>
	/// A value that is a handle to something running: a process, a connection. A goal called without waiting gets a
	/// deep copy of the caller's variables, and a copy of a handle copies its pipes and sockets; when the copy is
	/// finalized it closes them under the original. A live value is handed on as it is, never copied.
	/// </summary>
	public interface ILiveValue
	{
	}
}
