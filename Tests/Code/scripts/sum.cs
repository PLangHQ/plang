public class Sum
{
    public async System.Threading.Tasks.Task<int> Start()
    {
        await System.Threading.Tasks.Task.Yield();
        return 42;
    }

    public async System.Threading.Tasks.Task<int> Add(int x, int y)
    {
        await System.Threading.Tasks.Task.Yield();
        return x + y;
    }
}
