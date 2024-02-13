using PLang.Modules;

namespace ComplexAlgorithm;

public class Program : BaseProgram
{
	public async Task<int> ComplexCalculate(int a, int b)
	{
		int result = (int)(a + b) * new Random().Next(1, 900);
		
		return result;
	}
}
