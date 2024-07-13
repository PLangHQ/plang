namespace PLang.Errors.AskUser
{

	public record AskUserPrivateKeyError : AskUserError
    {

        public AskUserPrivateKeyError(string message, Func<string, Task>? callback = null) : base(message, CreateAdapter(callback))
        {

        }

    }
}
