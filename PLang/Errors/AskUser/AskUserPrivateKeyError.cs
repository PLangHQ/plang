namespace PLang.Errors.AskUser
{

	public record AskUserPrivateKeyError : AskUserError
    {

        public AskUserPrivateKeyError(string Message, Func<string, Task>? callback = null, string Actor = "system", string Channel = "default") : base(Actor, Channel, Message, CreateAdapter(callback))
        {

        }

    }
}
