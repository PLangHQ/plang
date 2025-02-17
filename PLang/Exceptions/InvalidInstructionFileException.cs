namespace PLang.Exceptions
{
	public class InvalidInstructionFileException(string message, Exception ex) : Exception(message, ex)
	{
	}
}
