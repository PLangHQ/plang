using Newtonsoft.Json;
using PLang.Runtime;
using System.Runtime.CompilerServices;

namespace PLang.Utils
{
	public static class ObjectExtension
	{
		public static string ToJson(this object obj)
		{
			return JsonConvert.SerializeObject(obj);
		}

		public static ObjectValue? GetObjectValue()
		{
			// would allow c# methods to access objectvalue of an object
			/*
			 * ValidateGoal(Goal goal, PrGoal prGoal) {
			 *		goal.Name = "Hello";
			 *		goal.GetObjectValue() 
			 *		goal.UpdateMemory(); this would update the memorystack with new goal.Name
			 * }
			 * */
			throw new NotImplementedException();
		}
	}


	public static class RuntimeExtensions
	{
		private static readonly ConditionalWeakTable<object, ObjectValue> Table = new();

		public static ObjectValue GetObjectValue(this object obj)
			=> Table.GetOrCreateValue(obj);

		public static void SetObjectValue(this object obj, ObjectValue value)
			=> Table.AddOrUpdate(obj, value);
	}
}
